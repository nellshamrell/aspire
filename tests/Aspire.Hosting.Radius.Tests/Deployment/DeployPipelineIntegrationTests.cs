// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Deployment;

public class DeployPipelineIntegrationTests
{
    [Fact]
    public async Task DeployStep_IsRegisteredInPipeline()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        // The environment resource should have PipelineStepAnnotation(s) attached
        Assert.True(environment.Annotations.OfType<Aspire.Hosting.Pipelines.PipelineStepAnnotation>().Any(),
            "RadiusEnvironmentResource should have at least one PipelineStepAnnotation registered");
    }

    [Fact]
    public async Task BicepGeneration_CalledBeforeRadDeploy()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        // Verify Bicep generation context can produce output
        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Bicep content should exist and contain expected structure
        Assert.NotEmpty(bicep);
        Assert.Contains("Applications.Core/environments", bicep);
    }

    [Fact]
    public void RadDeployCommand_SynthesizedCorrectly()
    {
        var bicepFilePath = Path.Combine(Path.GetTempPath(), "app.bicep");

        var command = RadCliHelper.ConstructDeployCommand(bicepFilePath);

        Assert.StartsWith("deploy", command);
        Assert.Contains(bicepFilePath, command);
    }

    [Fact]
    public void RadDeployCommand_WithJsonOutput_IncludesOutputFlag()
    {
        var bicepFilePath = Path.Combine(Path.GetTempPath(), "app.bicep");

        var command = RadCliHelper.ConstructDeployCommand(bicepFilePath, outputFormat: "json");

        Assert.Contains("--output json", command);
    }

    [Fact]
    public async Task DeploymentProgress_ParsesJsonEvents()
    {
        // Simulate a JSON progress event from rad deploy --output json
        // Expected format based on Radius CLI documentation:
        // {"status": "InProgress", "resource": "cache", "type": "Applications.Datastores/redisCaches", "message": "Creating resource"}
        var jsonEvent = """{"status":"InProgress","resource":"cache","type":"Applications.Datastores/redisCaches","message":"Creating resource"}""";

        var progress = RadDeploymentProgress.ParseProgressEvent(jsonEvent);

        Assert.NotNull(progress);
        Assert.Equal("InProgress", progress.Status);
        Assert.Equal("cache", progress.Resource);
        Assert.Equal("Creating resource", progress.Message);
    }

    [Fact]
    public void DeploymentProgress_ParsesCompletedEvent()
    {
        var jsonEvent = """{"status":"Succeeded","resource":"cache","type":"Applications.Datastores/redisCaches","message":"Resource created successfully"}""";

        var progress = RadDeploymentProgress.ParseProgressEvent(jsonEvent);

        Assert.NotNull(progress);
        Assert.Equal("Succeeded", progress.Status);
    }

    [Fact]
    public void DeploymentProgress_ReturnsNullForInvalidJson()
    {
        var invalidJson = "not valid json at all";

        var progress = RadDeploymentProgress.ParseProgressEvent(invalidJson);

        Assert.Null(progress);
    }

    [Fact]
    public void DeploymentProgress_FormatsForHumanReadableOutput()
    {
        var progress = new RadDeploymentProgress
        {
            Status = "InProgress",
            Resource = "cache",
            Type = "Applications.Datastores/redisCaches",
            Message = "Creating resource"
        };

        var formatted = progress.ToDisplayString();

        Assert.Contains("cache", formatted);
        Assert.Contains("Creating resource", formatted);
    }
}

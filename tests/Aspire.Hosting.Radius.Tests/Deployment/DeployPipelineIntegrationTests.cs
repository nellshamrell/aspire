// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // Type is for evaluation purposes only
#pragma warning disable ASPIREPIPELINES001 // Pipeline API is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Deployment;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Deployment;

public class DeployPipelineIntegrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersPipelineStepAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var env = model.Resources.OfType<RadiusEnvironmentResource>().Single();

        var annotations = env.Annotations.OfType<PipelineStepAnnotation>().ToList();

        // Should have both publish and deploy step annotations
        Assert.True(annotations.Count >= 2, $"Expected at least 2 PipelineStepAnnotations, got {annotations.Count}");
    }

    [Fact]
    public void DeployStep_HasCorrectName()
    {
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.Equal("radius-deploy-testenv", step.Name);
    }

    [Fact]
    public void DeployStep_DependsOnPublish()
    {
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.Contains(WellKnownPipelineSteps.Publish, step.DependsOnSteps);
    }

    [Fact]
    public void DeployStep_DoesNotDependOnPush()
    {
        // T049a: Deploy should NOT depend on Push (kind clusters don't need a registry)
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.DoesNotContain(WellKnownPipelineSteps.Push, step.DependsOnSteps);
    }

    [Fact]
    public void DeployStep_RequiredByDeploy()
    {
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.Contains(WellKnownPipelineSteps.Deploy, step.RequiredBySteps);
    }

    [Fact]
    public void DeployStep_HasDescription()
    {
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.NotNull(step.Description);
        Assert.Contains("testenv", step.Description);
    }

    [Fact]
    public void DeployStep_AssociatedWithResource()
    {
        var resource = new RadiusEnvironmentResource("testenv");
        var step = RadiusDeploymentPipelineStep.Create(resource);

        Assert.Same(resource, step.Resource);
    }

    [Fact]
    public void ConstructDeployCommand_IncludesFullBicepPath()
    {
        var bicepPath = "/tmp/output/app.bicep";
        var args = RadCliHelper.ConstructDeployCommand(bicepPath);

        Assert.StartsWith("deploy", args);
        Assert.Contains(bicepPath, args);
        Assert.Contains("--output json", args);
    }

    [Fact]
    public void RadDeploymentProgress_ParsesValidJson()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory()
            .CreateLogger("Test");
        var progress = new RadDeploymentProgress(logger);

        var json = """{"timestamp":"2024-01-15T10:30:00Z","status":"InProgress","resource":{"type":"Applications.Core/containers","name":"api"},"message":"Deploying container..."}""";

        var evt = progress.ProcessLine(json);

        Assert.NotNull(evt);
        Assert.Equal("InProgress", evt.Status);
        Assert.Equal("api", evt.Resource?.Name);
        Assert.Equal("Applications.Core/containers", evt.Resource?.Type);
    }

    [Fact]
    public void RadDeploymentProgress_HandlesInvalidJson()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory()
            .CreateLogger("Test");
        var progress = new RadDeploymentProgress(logger);

        var evt = progress.ProcessLine("not json at all");

        Assert.Null(evt);
    }

    [Fact]
    public void RadDeploymentProgress_HandlesEmptyLine()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory()
            .CreateLogger("Test");
        var progress = new RadDeploymentProgress(logger);

        var evt = progress.ProcessLine("");

        Assert.Null(evt);
    }

    [Fact]
    public void RadDeploymentProgress_FormatEvent_Succeeded()
    {
        var evt = new DeploymentEvent
        {
            Status = "Succeeded",
            Resource = new DeploymentResourceInfo { Type = "Applications.Core/containers", Name = "api" },
            Message = "Container deployed"
        };

        var formatted = RadDeploymentProgress.FormatEvent(evt);

        Assert.Contains("api", formatted);
        Assert.Contains("✅", formatted);
    }

    [Fact]
    public void RadDeploymentProgress_FormatEvent_Failed()
    {
        var evt = new DeploymentEvent
        {
            Status = "Failed",
            Resource = new DeploymentResourceInfo { Type = "Applications.Datastores/redisCaches", Name = "cache" },
            Message = "Recipe timed out"
        };

        var formatted = RadDeploymentProgress.FormatEvent(evt);

        Assert.Contains("cache", formatted);
        Assert.Contains("❌", formatted);
    }
}

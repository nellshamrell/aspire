// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Deployment;

/// <summary>
/// Integration tests for the Radius deployment pipeline steps (mocked rad CLI).
/// </summary>
public class DeployPipelineIntegrationTests
{
    [Fact]
    public async Task RadiusEnvironment_RegistersDeployPipelineSteps()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotations = environment.Annotations
            .Where(a => a.GetType().Name == "PipelineStepAnnotation")
            .ToList();

        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task DeployStep_DoesNotDependOnPush()
    {
        // T049a: Kind clusters don't need a container registry.
        // The deploy step should NOT depend on WellKnownPipelineSteps.Push.
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotation = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .First();

        var factoryContext = new PipelineStepFactoryContext
        {
            PipelineContext = CreateMinimalPipelineContext(model),
            Resource = environment
        };

        var steps = await annotation.CreateStepsAsync(factoryContext);
        var deployStep = steps.FirstOrDefault(s => s.Name.StartsWith("deploy-radius-", StringComparison.Ordinal));

        Assert.NotNull(deployStep);
        Assert.DoesNotContain(WellKnownPipelineSteps.Push, deployStep.DependsOnSteps);
        Assert.DoesNotContain(WellKnownPipelineSteps.PushPrereq, deployStep.DependsOnSteps);
    }

    [Fact]
    public async Task DeployStep_DependsOnPublishStep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotation = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .First();

        var factoryContext = new PipelineStepFactoryContext
        {
            PipelineContext = CreateMinimalPipelineContext(model),
            Resource = environment
        };

        var steps = await annotation.CreateStepsAsync(factoryContext);
        var deployStep = steps.FirstOrDefault(s => s.Name.StartsWith("deploy-radius-", StringComparison.Ordinal));

        Assert.NotNull(deployStep);
        Assert.Contains("publish-radius", deployStep.DependsOnSteps);
    }

    [Fact]
    public async Task ValidateRadCliStep_IsRegistered()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotation = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .First();

        var factoryContext = new PipelineStepFactoryContext
        {
            PipelineContext = CreateMinimalPipelineContext(model),
            Resource = environment
        };

        var steps = await annotation.CreateStepsAsync(factoryContext);
        var validateStep = steps.FirstOrDefault(s => s.Name.StartsWith("validate-rad-cli-", StringComparison.Ordinal));

        Assert.NotNull(validateStep);
        Assert.Contains(WellKnownPipelineSteps.DeployPrereq, validateStep.DependsOnSteps);
    }

    [Fact]
    public async Task PublishStep_IsRequiredByPublish()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotation = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .First();

        var factoryContext = new PipelineStepFactoryContext
        {
            PipelineContext = CreateMinimalPipelineContext(model),
            Resource = environment
        };

        var steps = await annotation.CreateStepsAsync(factoryContext);
        var publishStep = steps.FirstOrDefault(s => s.Name.StartsWith("publish-", StringComparison.Ordinal));

        Assert.NotNull(publishStep);
        Assert.Contains(WellKnownPipelineSteps.Publish, publishStep.RequiredBySteps);
    }

    [Fact]
    public async Task AllThreeSteps_AreRegistered()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotation = environment.Annotations
            .OfType<PipelineStepAnnotation>()
            .First();

        var factoryContext = new PipelineStepFactoryContext
        {
            PipelineContext = CreateMinimalPipelineContext(model),
            Resource = environment
        };

        var steps = (await annotation.CreateStepsAsync(factoryContext)).ToList();

        Assert.Equal(3, steps.Count);
        Assert.Contains(steps, s => s.Name == "publish-radius");
        Assert.Contains(steps, s => s.Name == "validate-rad-cli-radius");
        Assert.Contains(steps, s => s.Name == "deploy-radius-radius");
    }

    [Fact]
    public void RadDeploymentProgress_ParsesJsonEvent()
    {
        var json = """{"timestamp":"2024-01-15T10:00:01Z","type":"ResourceProvisioning","resource":"redis","message":"Provisioning redis..."}""";

        var evt = RadDeploymentProgress.TryParseEvent(json);

        Assert.NotNull(evt);
        Assert.Equal(RadProgressEventType.ResourceProvisioning, evt.Type);
        Assert.Equal("redis", evt.Resource);
        Assert.Equal("Provisioning redis...", evt.Message);
    }

    [Fact]
    public void RadDeploymentProgress_ParsesNonJsonAsInfoEvent()
    {
        var plainText = "Some warning message from rad";

        var evt = RadDeploymentProgress.TryParseEvent(plainText);

        Assert.NotNull(evt);
        Assert.Equal(RadProgressEventType.Info, evt.Type);
        Assert.Equal(plainText, evt.Message);
    }

    [Fact]
    public void RadDeploymentProgress_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(RadDeploymentProgress.TryParseEvent(null));
        Assert.Null(RadDeploymentProgress.TryParseEvent(""));
        Assert.Null(RadDeploymentProgress.TryParseEvent("   "));
    }

    [Fact]
    public void RadDeploymentProgress_FormatEvent_IncludesResourceName()
    {
        var evt = new RadProgressEvent
        {
            Type = RadProgressEventType.ResourceReady,
            Resource = "redis",
            Message = "redis is ready"
        };

        var formatted = RadDeploymentProgress.FormatEvent(evt);

        Assert.Contains("redis", formatted);
        Assert.Contains("redis is ready", formatted);
    }

    [Fact]
    public void RadCliHelper_ConstructDeployCommand_IncludesBicepPath()
    {
        var args = RadCliHelper.ConstructDeployCommand("/output/radius/app.bicep");

        Assert.StartsWith("deploy", args, StringComparison.Ordinal);
        Assert.Contains("/output/radius/app.bicep", args);
        Assert.Contains("--output json", args);
    }

    private static PipelineContext CreateMinimalPipelineContext(DistributedApplicationModel model)
    {
        return new PipelineContext(
            model,
            new DistributedApplicationExecutionContext(new DistributedApplicationExecutionContextOptions(DistributedApplicationOperation.Publish)),
            new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
            CancellationToken.None);
    }
}

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable ASPIREPIPELINES001

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Deployment;

/// <summary>
/// Integration tests for the Radius deployment pipeline step registration.
/// These tests verify pipeline wiring without executing <c>rad deploy</c>.
/// </summary>
public class DeployPipelineIntegrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersPublishPipelineStep()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var env = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var pipelineAnnotations = env.Annotations.OfType<PipelineStepAnnotation>().ToList();

        // Should have at least a publish step and a deploy step
        Assert.True(pipelineAnnotations.Count >= 2,
            $"Expected at least 2 PipelineStepAnnotations (publish + deploy), found {pipelineAnnotations.Count}");
    }

    [Fact]
    public void AddRadiusEnvironment_RegistersDeployPipelineStep()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("myenv");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var env = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var pipelineAnnotations = env.Annotations.OfType<PipelineStepAnnotation>().ToList();

        // Verify both publish and deploy annotations exist
        Assert.Equal(2, pipelineAnnotations.Count);
    }

    [Fact]
    public void DeployStep_DependsOnPublishStep()
    {
        var publishStepName = "publish-radius-test";
        var deployStep = RadiusDeploymentPipelineStep.Create("test", publishStepName);

        Assert.Contains(publishStepName, deployStep.DependsOnSteps);
    }

    [Fact]
    public void DeployStep_DependsOnPush()
    {
        var deployStep = RadiusDeploymentPipelineStep.Create("test", "publish-radius-test");

        Assert.Contains(WellKnownPipelineSteps.Push, deployStep.DependsOnSteps);
    }

    [Fact]
    public void DeployStep_RequiredByDeploy()
    {
        var deployStep = RadiusDeploymentPipelineStep.Create("test", "publish-radius-test");

        Assert.Contains(WellKnownPipelineSteps.Deploy, deployStep.RequiredBySteps);
    }

    [Fact]
    public void DeployStep_HasCorrectName()
    {
        var deployStep = RadiusDeploymentPipelineStep.Create("myenv", "publish-radius-myenv");

        Assert.Equal("deploy-radius-myenv", deployStep.Name);
    }

    [Fact]
    public void DeployStep_HasDescription()
    {
        var deployStep = RadiusDeploymentPipelineStep.Create("myenv", "publish-radius-myenv");

        Assert.NotNull(deployStep.Description);
        Assert.Contains("myenv", deployStep.Description);
    }

    [Fact]
    public void RadDeployCommand_IsSynthesizedCorrectly()
    {
        var command = RadCliHelper.ConstructDeployCommand("/output/path/app.bicep");

        Assert.Equal("deploy \"/output/path/app.bicep\" --output json", command);
    }

    [Fact]
    public void DeployStep_HasAction()
    {
        var deployStep = RadiusDeploymentPipelineStep.Create("test", "publish-radius-test");

        Assert.NotNull(deployStep.Action);
    }
}

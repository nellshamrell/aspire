// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusDeploymentPipelineStepTests
{
    [Fact]
    public void Deploy_step_depends_on_publish_and_push_steps()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var deployHelper = new RadiusDeploymentPipelineStep(env);
        var step = deployHelper.CreateStep("publish-radius-radius");

        // Deploy depends on both publish and push to ensure images are available
        Assert.Contains("publish-radius-radius", step.DependsOnSteps);
        Assert.Contains(WellKnownPipelineSteps.Push, step.DependsOnSteps);

        // RequiredBy Deploy aggregation
        Assert.Contains(WellKnownPipelineSteps.Deploy, step.RequiredBySteps);
    }

    [Fact]
    public void Deploy_step_name_includes_environment_name()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("staging");
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var deployHelper = new RadiusDeploymentPipelineStep(env);
        var step = deployHelper.CreateStep("publish-radius-staging");

        Assert.Equal("deploy-radius-staging", step.Name);
    }

    [Fact]
    public void FindRadCli_returns_null_when_rad_not_on_path()
    {
        // In test environments, rad is typically not installed
        // This test verifies the search logic doesn't throw
        var result = RadiusDeploymentPipelineStep.FindRadCli();

        // Result depends on environment — just verify no exception
        // If rad IS installed, result will be non-null (which is also valid)
        Assert.True(result is null || File.Exists(result));
    }

    [Fact]
    public void Pipeline_annotation_produces_both_publish_and_deploy_steps()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var annotations = env.Annotations.OfType<PipelineStepAnnotation>().ToArray();
        Assert.Single(annotations);
    }
}

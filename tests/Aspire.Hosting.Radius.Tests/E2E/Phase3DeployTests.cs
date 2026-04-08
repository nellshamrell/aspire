// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.E2E;

public class Phase3DeployTests
{
    [Fact]
    public void Full_pipeline_produces_pipeline_annotation()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api:latest");

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        // Verify pipeline annotation exists
        var annotation = env.Annotations.OfType<PipelineStepAnnotation>().Single();
        Assert.NotNull(annotation);
    }

    [Fact]
    public void Deploy_step_depends_on_publish_and_push_for_ordering()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        // Directly create deploy step to verify wiring
        var deployHelper = new RadiusDeploymentPipelineStep(env);
        var publishStepName = $"publish-radius-{env.Name}";
        var deployStep = deployHelper.CreateStep(publishStepName);

        // Deploy depends on publish
        Assert.Contains(publishStepName, deployStep.DependsOnSteps);

        // Deploy is required by the Deploy aggregation step
        Assert.Contains(WellKnownPipelineSteps.Deploy, deployStep.RequiredBySteps);

        // Deploy depends on Push to ensure container images are available
        Assert.Contains(WellKnownPipelineSteps.Push, deployStep.DependsOnSteps);
    }

    [Fact]
    public void Bicep_generation_produces_valid_output_for_full_app()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache");
        builder.AddPostgres("db");

        builder.AddContainer("api", "myapp/api:latest")
            .WithReference(redis);

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model, env, NullLogger.Instance);

        var bicep = infraBuilder.Build();

        // Verify complete Bicep structure
        Assert.StartsWith("extension radius", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/postgresDatabases", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("connections:", bicep);
        Assert.Contains("recipes:", bicep);
    }
}

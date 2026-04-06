// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Tests.TestHosts;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class RadiusInfrastructureInitializationTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");
        builder.AddContainer("worker", "myregistry/worker:latest");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var webfrontend = model.Resources.First(r => r.Name == "webfrontend");
        var worker = model.Resources.First(r => r.Name == "worker");

        Assert.NotNull(webfrontend.GetDeploymentTargetAnnotation());
        Assert.NotNull(worker.GetDeploymentTargetAnnotation());
    }

    [Fact]
    public async Task DeploymentTargetAnnotation_PointsToRadiusEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var radiusEnv = builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var webfrontend = model.Resources.First(r => r.Name == "webfrontend");
        var annotation = webfrontend.GetDeploymentTargetAnnotation();

        Assert.NotNull(annotation);
        Assert.Same(radiusEnv.Resource, annotation.ComputeEnvironment);
    }

    [Fact]
    public async Task DeploymentTargetAnnotation_HasRadiusDeploymentResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var webfrontend = model.Resources.First(r => r.Name == "webfrontend");
        var annotation = webfrontend.GetDeploymentTargetAnnotation();

        Assert.NotNull(annotation);
        Assert.IsType<RadiusDeploymentResource>(annotation.DeploymentTarget);
    }

    [Fact]
    public async Task RadiusEnvironment_IsNotAnnotated()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().Single();

        // The Radius environment itself should NOT have a deployment target
        Assert.Null(radiusEnv.GetDeploymentTargetAnnotation());
    }

    [Fact]
    public async Task SimpleRadiusAppHost_AllComputeResourcesAnnotated()
    {
        var builder = SimpleRadiusAppHost.CreateBuilder();

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var computeResources = model.GetComputeResources().ToList();
        Assert.NotEmpty(computeResources);

        foreach (var resource in computeResources)
        {
            Assert.NotNull(resource.GetDeploymentTargetAnnotation());
        }
    }

    [Fact]
    public async Task MultiResourceAppHost_AllComputeResourcesAnnotated()
    {
        var builder = MultiResourceAppHost.CreateBuilder();

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var computeResources = model.GetComputeResources().ToList();
        Assert.NotEmpty(computeResources);

        foreach (var resource in computeResources)
        {
            var annotation = resource.GetDeploymentTargetAnnotation();
            Assert.NotNull(annotation);
            Assert.IsType<RadiusEnvironmentResource>(annotation.ComputeEnvironment);
        }
    }

    [Fact]
    public async Task PublishAsRadiusResource_WithoutEnvironment_ThrowsOnStart()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // Register the infrastructure subscriber but NOT a RadiusEnvironmentResource
        builder.AddRadiusInfrastructureCore();

        builder.AddContainer("api", "myimage")
            .PublishAsRadiusResource(_ => { });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => RadiusTestHelper.BuildAndGetModelAsync(builder));
    }
}

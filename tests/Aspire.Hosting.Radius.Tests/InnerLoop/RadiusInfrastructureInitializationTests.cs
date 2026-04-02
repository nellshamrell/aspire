// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class RadiusInfrastructureInitializationTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotation_ToComputeResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("myapp", "mcr.microsoft.com/dotnet/samples:aspnetapp");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource).ToList();
        var annotation = Assert.Single(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotation.DeploymentTarget);
        Assert.Equal("radius", annotation.DeploymentTarget.Name);
    }

    [Fact]
    public async Task BeforeStartEvent_SetsComputeEnvironment_OnDeploymentTargetAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var annotation = Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource));
        Assert.NotNull(annotation.ComputeEnvironment);
        Assert.IsType<RadiusEnvironmentResource>(annotation.ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_DoesNotAnnotate_RadiusEnvironmentResourceItself()
    {
        var builder = DistributedApplication.CreateBuilder();

        var env = builder.AddRadiusEnvironment("radius");
        builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(env.Resource);
        Assert.Empty(annotations);
    }

    [Fact]
    public async Task BeforeStartEvent_CreatesDashboardResource_WhenDashboardEnabled()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());
        Assert.Equal("radius-dashboard", dashboard.Name);
    }

    [Fact]
    public async Task BeforeStartEvent_NoDashboardResource_WhenDashboardDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(false);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Empty(model.Resources.OfType<RadiusDashboardResource>());
    }

    [Fact]
    public async Task BeforeStartEvent_AnnotatesAllResources_NotJustContainers()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container1 = builder.AddContainer("app1", "image1:latest");
        var container2 = builder.AddContainer("app2", "image2:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(container1.Resource));
        Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(container2.Resource));
    }

    [Fact]
    public async Task BeforeStartEvent_SetsDashboardEndpoint_OnEnvironmentResource()
    {
        var builder = DistributedApplication.CreateBuilder();

        var env = builder.AddRadiusEnvironment("radius")
                         .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        Assert.NotNull(env.Resource.DashboardEndpoint);
    }

    [Fact]
    public async Task BeforeStartEvent_DashboardEndpointIsNull_WhenDashboardDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();

        var env = builder.AddRadiusEnvironment("radius")
                         .WithDashboard(false);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        Assert.Null(env.Resource.DashboardEndpoint);
    }
}

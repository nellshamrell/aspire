// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class DashboardContainerTests
{
    [Fact]
    public async Task DashboardContainer_UsesCorrectImage()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());

        var imageAnnotation = Assert.Single(dashboard.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io/radius-project/dashboard", imageAnnotation.Image);
        Assert.Equal("latest", imageAnnotation.Tag);
    }

    [Fact]
    public async Task DashboardContainer_ExposedOnPort7007()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());

        var endpoint = Assert.Single(dashboard.Annotations.OfType<EndpointAnnotation>());
        Assert.Equal(7007, endpoint.Port);
        Assert.Equal(7007, endpoint.TargetPort);
        Assert.Equal("http", endpoint.UriScheme);
    }

    [Fact]
    public async Task DashboardContainer_RegisteredInAppModel()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Contains(model.Resources, r => r is RadiusDashboardResource);
    }

    [Fact]
    public async Task NoDashboard_WhenDisabled()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(false);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.DoesNotContain(model.Resources, r => r is RadiusDashboardResource);
    }

    [Fact]
    public async Task DashboardResource_IsNotAnnotatedWithDeploymentTarget()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);
        builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());

        var deploymentAnnotations = RadiusTestHelper.GetDeploymentTargetAnnotations(dashboard);
        Assert.Empty(deploymentAnnotations);
    }

    [Fact]
    public async Task DashboardResource_HasCorrectDisplayName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());

        Assert.Equal("Radius Dashboard", dashboard.DisplayName);
    }
}

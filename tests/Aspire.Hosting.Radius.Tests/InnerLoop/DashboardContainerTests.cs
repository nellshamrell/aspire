// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class DashboardContainerTests
{
    [Fact]
    public async Task Dashboard_UsesCorrectImage()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().Single();
        Assert.True(dashboard.TryGetContainerImageName(out var imageName));
        Assert.Equal($"{RadiusDashboardResource.DefaultImage}:{RadiusDashboardResource.DefaultTag}", imageName);
    }

    [Fact]
    public async Task Dashboard_ExposesHttpEndpoint()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().Single();
        var endpoints = dashboard.Annotations.OfType<EndpointAnnotation>().ToArray();

        var httpEndpoint = endpoints.SingleOrDefault(e => e.Name == "http");
        Assert.NotNull(httpEndpoint);
        Assert.Equal(RadiusDashboardResource.DefaultPort, httpEndpoint.TargetPort);
        Assert.True(httpEndpoint.IsExternal);
    }

    [Fact]
    public async Task Dashboard_RegisteredInModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToArray();
        Assert.Single(dashboards);
    }

    [Fact]
    public async Task Dashboard_Disabled_NoDashboardCreated()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToArray();
        Assert.Empty(dashboards);
    }

    [Fact]
    public async Task Dashboard_PrimaryEndpoint_IsAccessible()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().Single();
        var endpoint = dashboard.PrimaryEndpoint;
        Assert.NotNull(endpoint);
        Assert.Equal("http", endpoint.EndpointName);
    }

    [Fact]
    public async Task Dashboard_NameIsDerivedFromEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("myenv")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().Single();
        Assert.Equal("myenv-dashboard", dashboard.Name);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class RadiusInfrastructureInitializationTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();

        Assert.Single(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotations[0].ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_AllComputeResources_GetAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        builder.AddContainer("worker", "mcr.microsoft.com/dotnet/runtime:8.0");
        builder.AddRedis("cache");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var computeResources = model.GetComputeResources().ToArray();
        foreach (var resource in computeResources)
        {
            var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(resource).ToArray();
            Assert.Single(annotations);
        }
    }

    [Fact]
    public async Task BeforeStartEvent_DashboardEnabled_CreatesDashboardResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().SingleOrDefault();
        Assert.NotNull(dashboard);
        Assert.Equal("radius-dashboard", dashboard.Name);
    }

    [Fact]
    public async Task BeforeStartEvent_DashboardDisabled_NoDashboardResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboard = model.Resources.OfType<RadiusDashboardResource>().SingleOrDefault();
        Assert.Null(dashboard);
    }

    [Fact]
    public async Task BeforeStartEvent_DashboardEnabled_SetsDashboardEndpoint()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var environment = RadiusTestHelper.GetRadiusEnvironment(model);
        Assert.NotNull(environment.DashboardEndpoint);
    }

    [Fact]
    public async Task BeforeStartEvent_NoRadiusEnvironment_DoesNothing()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // No Radius environment - should not throw
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();
        Assert.Empty(annotations);
    }
}

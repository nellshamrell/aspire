#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class DashboardContainerTests
{
    [Fact]
    public void DashboardEnabled_DefaultsToTrue()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env = builder.AddRadiusEnvironment("radius");

        Assert.True(env.Resource.DashboardEnabled);
    }

    [Fact]
    public void WithDashboard_False_DisablesDashboard()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env = builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);

        Assert.False(env.Resource.DashboardEnabled);
    }

    [Fact]
    public async Task DashboardDisabled_NoDashboardResourceInModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // When dashboard is disabled, no resource named "radius-dashboard" should exist
        var dashboardResource = model.Resources
            .FirstOrDefault(r => r.Name.Contains("dashboard", StringComparison.OrdinalIgnoreCase));

        Assert.Null(dashboardResource);
    }

    [Fact]
    public async Task DashboardDisabled_ContainersStillGetAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");

        // Annotations should still be applied regardless of dashboard state
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);
        Assert.NotEmpty(annotations);
    }

    [Fact]
    public void DashboardEndpoint_IsNullByDefault()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env = builder.AddRadiusEnvironment("radius");

        // DashboardEndpoint is populated only when the dashboard container is actually created
        Assert.Null(env.Resource.DashboardEndpoint);
    }

    [Fact]
    public void DashboardEnabled_CanBeToggledMultipleTimes()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env = builder.AddRadiusEnvironment("radius")
            .WithDashboard(false)
            .WithDashboard(true)
            .WithDashboard(false);

        Assert.False(env.Resource.DashboardEnabled);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

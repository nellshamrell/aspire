// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class MultiEnvironmentTests
{
    [Fact]
    public async Task MultipleRadiusEnvironments_CanCoexist()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
        Assert.Equal(2, environments.Length);
        Assert.Contains(environments, e => e.Name == "radius-dev" && e.Namespace == "dev");
        Assert.Contains(environments, e => e.Name == "radius-staging" && e.Namespace == "staging");
    }

    [Fact]
    public async Task MultipleEnvironments_DifferentNamespaces_NoDashboardCollisions()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev")
            .WithDashboard(true);
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging")
            .WithDashboard(true);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToArray();
        Assert.Equal(2, dashboards.Length);
        Assert.NotEqual(dashboards[0].Name, dashboards[1].Name);
    }

    [Fact]
    public async Task MultipleEnvironments_ResourcesGetAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();

        // Each environment attaches its own annotation
        Assert.Equal(2, annotations.Length);
    }

    [Fact]
    public async Task MultipleEnvironments_OneDashboardDisabled()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius-dev")
            .WithDashboard(true);
        builder.AddRadiusEnvironment("radius-staging")
            .WithDashboard(false);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToArray();
        Assert.Single(dashboards);
        Assert.Equal("radius-dev-dashboard", dashboards[0].Name);
    }
}

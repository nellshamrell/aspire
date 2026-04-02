// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class MultiEnvironmentTests
{
    [Fact]
    public async Task MultipleRadiusEnvironments_CanCoexist()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius-dev")
               .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
               .WithRadiusNamespace("staging");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();
        Assert.Equal(2, environments.Count);
        Assert.Contains(environments, e => e.Namespace == "dev");
        Assert.Contains(environments, e => e.Namespace == "staging");
    }

    [Fact]
    public async Task MultipleEnvironments_AllAnnotateResources()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius-dev")
               .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
               .WithRadiusNamespace("staging");
        var container = builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource).ToList();
        Assert.Equal(2, annotations.Count);
        Assert.Contains(annotations, a => a.DeploymentTarget.Name == "radius-dev");
        Assert.Contains(annotations, a => a.DeploymentTarget.Name == "radius-staging");
    }

    [Fact]
    public async Task MultipleEnvironments_EachGetOwnDashboard()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius-dev")
               .WithRadiusNamespace("dev")
               .WithDashboard(true);
        builder.AddRadiusEnvironment("radius-staging")
               .WithRadiusNamespace("staging")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToList();
        Assert.Equal(2, dashboards.Count);
        Assert.Contains(dashboards, d => d.Name == "radius-dev-dashboard");
        Assert.Contains(dashboards, d => d.Name == "radius-staging-dashboard");
    }

    [Fact]
    public async Task MultipleEnvironments_DifferentNamespaces_NamingCollisionFree()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("env-a")
               .WithRadiusNamespace("namespace-a");
        builder.AddRadiusEnvironment("env-b")
               .WithRadiusNamespace("namespace-b");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Environment resources should have distinct names
        var envNames = model.Resources.OfType<RadiusEnvironmentResource>().Select(e => e.Name).ToList();
        Assert.Equal(envNames.Count, envNames.Distinct().Count());

        // Dashboard resources should have distinct names
        var dashNames = model.Resources.OfType<RadiusDashboardResource>().Select(d => d.Name).ToList();
        Assert.Equal(dashNames.Count, dashNames.Distinct().Count());
    }

    [Fact]
    public async Task MixedDashboardConfig_OnlyEnabledEnvironmentsGetDashboards()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius-dev")
               .WithDashboard(true);
        builder.AddRadiusEnvironment("radius-staging")
               .WithDashboard(false);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToList();
        var dashboard = Assert.Single(dashboards);
        Assert.Equal("radius-dev-dashboard", dashboard.Name);
    }
}

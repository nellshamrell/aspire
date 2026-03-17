// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class GracefulDegradationTests
{
    [Fact]
    public async Task RadiusEnvironment_WorksWithoutKubernetes()
    {
        // Radius should work even when Kubernetes is not available
        // Annotations are attached regardless of cluster availability
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();

        Assert.Single(annotations);
    }

    [Fact]
    public async Task RadiusEnvironment_WorksWithoutRadCLI()
    {
        // Phase 3 (aspire run) should not require rad CLI
        // Annotations and dashboard should still work
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        // Annotations should be attached
        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();
        Assert.Single(annotations);

        // Dashboard should be created
        var dashboard = model.Resources.OfType<RadiusDashboardResource>().SingleOrDefault();
        Assert.NotNull(dashboard);
    }

    [Fact]
    public async Task DashboardDisabled_NoErrorsOrWarnings()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should not throw
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        // Compute resources should still get annotations
        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();
        Assert.Single(annotations);

        // No dashboard resource
        var dashboards = model.Resources.OfType<RadiusDashboardResource>().ToArray();
        Assert.Empty(dashboards);
    }

    [Fact]
    public async Task RadiusInfrastructure_CanBeInvokedDirectly()
    {
        // Test that RadiusInfrastructure.OnBeforeStartAsync works without a full app host
        var logger = NullLogger<RadiusInfrastructure>.Instance;
        var infrastructure = new RadiusInfrastructure(logger);

        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var @event = new BeforeStartEvent(app.Services, model);

        // Should not throw
        await infrastructure.OnBeforeStartAsync(@event);

        var container = model.Resources.Single(r => r.Name == "webapi");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container).ToArray();
        Assert.Single(annotations);
    }

    [Fact]
    public async Task EmptyModel_NoRadiusEnvironment_DoesNotThrow()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        // No Radius environment, no resources
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Should complete without errors
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
    }
}

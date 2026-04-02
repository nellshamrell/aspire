// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class GracefulDegradationTests
{
    [Fact]
    public async Task AnnotationsAttached_WithoutKubernetes()
    {
        // Phase 3 is visualization-only — no Kubernetes cluster required
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        // Annotations should still be attached regardless of Kubernetes availability
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource).ToList();
        Assert.Single(annotations);
    }

    [Fact]
    public async Task AnnotationsAttached_WithoutRadCli()
    {
        // Phase 3 does not invoke rad CLI — annotations work without it
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("app1", "image1:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource).ToList();
        var annotation = Assert.Single(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotation.DeploymentTarget);
    }

    [Fact]
    public async Task DashboardEnabled_ResourceCreated_WithoutExternalDependencies()
    {
        // Dashboard resource creation doesn't require external services
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var dashboard = Assert.Single(model.Resources.OfType<RadiusDashboardResource>());
        Assert.NotNull(dashboard);
    }

    [Fact]
    public async Task MultipleResources_AllAnnotated_InVisualizationOnlyMode()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var c1 = builder.AddContainer("app1", "image1:latest");
        var c2 = builder.AddContainer("app2", "image2:latest");
        var c3 = builder.AddContainer("app3", "image3:latest");

        using var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        // All resources get annotations even without any external infrastructure
        Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(c1.Resource));
        Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(c2.Resource));
        Assert.Single(RadiusTestHelper.GetDeploymentTargetAnnotations(c3.Resource));
    }

    [Fact]
    public async Task IdempotentAnnotation_CallingEventTwice_DoesNotDuplicate()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("myapp", "myimage:latest");

        using var app = builder.Build();

        // Publish event twice to simulate repeated calls
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(container.Resource).ToList();
        Assert.Single(annotations);
    }

    [Fact]
    public async Task IdempotentDashboard_CallingEventTwice_DoesNotDuplicateDashboard()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();

        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        Assert.Single(model.Resources.OfType<RadiusDashboardResource>());
    }
}

#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class GracefulDegradationTests
{
    [Fact]
    public async Task AspireRun_CompletesWithoutErrors_WhenKubernetesUnavailable()
    {
        // In the test environment there is no Kubernetes cluster — this simulates
        // the "no K8s" scenario. The subscriber should still annotate resources.
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();

        // Should not throw even without Kubernetes
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task AspireRun_CompletesWithoutErrors_WhenRadCliNotOnPath()
    {
        // The `rad` CLI is not installed in the test environment.
        // The subscriber should still work — it only attaches annotations.
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("worker", "myimage", "latest");

        var app = builder.Build();

        // Should not throw
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var worker = model.Resources.First(r => r.Name == "worker");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(worker);

        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task Annotations_AttachedInVisualizationOnlyMode()
    {
        // Even when Kubernetes and rad are unavailable, annotations are attached
        // to enable visualization in the Aspire Dashboard.
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");
        builder.AddContainer("worker", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Exclude dashboard container — it's infrastructure, not a user workload
        foreach (var resource in model.GetComputeResources().Where(r => r is not RadiusDashboardResource))
        {
            var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(resource);
            Assert.NotEmpty(annotations);
            Assert.IsType<RadiusEnvironmentResource>(annotations[0].ComputeEnvironment);
        }
    }

    [Fact]
    public async Task MultipleResources_AllAnnotated_RegardlessOfInfraState()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("frontend", "img1", "latest");
        builder.AddContainer("backend", "img2", "latest");
        builder.AddContainer("cache", "redis", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        // Exclude dashboard container — it's infrastructure, not a user workload
        var workloadResources = model.GetComputeResources()
            .Where(r => r is not RadiusDashboardResource)
            .ToList();

        // All 3 user container resources should be annotated
        Assert.Equal(3, workloadResources.Count);
        Assert.All(workloadResources, resource =>
        {
            var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(resource);
            Assert.NotEmpty(annotations);
        });
    }

    [Fact]
    public async Task DashboardContainerFailure_HandledGracefully()
    {
        // When dashboard is enabled but the container fails to start,
        // the app should not crash. Currently the subscriber only sets annotations
        // and does not start containers, so this verifies the no-crash baseline.
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();

        // Should complete without throwing
        var exception = await Record.ExceptionAsync(
            () => ExecuteBeforeStartHooksAsync(app, default));

        Assert.Null(exception);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

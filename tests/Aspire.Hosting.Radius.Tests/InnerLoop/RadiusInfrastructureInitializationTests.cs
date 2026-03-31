#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class RadiusInfrastructureInitializationTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotation_ToContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");
        builder.AddContainer("worker", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var worker = model.Resources.First(r => r.Name == "worker");

        var apiAnnotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);
        var workerAnnotations = RadiusTestHelper.GetDeploymentTargetAnnotations(worker);

        Assert.Single(apiAnnotations);
        Assert.Single(workerAnnotations);
        Assert.IsType<RadiusEnvironmentResource>(apiAnnotations[0].ComputeEnvironment);
        Assert.IsType<RadiusEnvironmentResource>(workerAnnotations[0].ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_AllComputeResources_GetRadiusAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env = builder.AddRadiusEnvironment("radius");
        builder.AddContainer("c1", "img", "latest");
        builder.AddContainer("c2", "img", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        // Exclude the dashboard container — it's infrastructure, not a user workload
        var workloadResources = model.GetComputeResources()
            .Where(r => r is not RadiusDashboardResource)
            .ToList();

        // All user compute resources should have DeploymentTargetAnnotation pointing to the Radius environment
        foreach (var resource in workloadResources)
        {
            var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(resource);
            Assert.NotEmpty(annotations);
            Assert.Equal(env.Resource, annotations[0].ComputeEnvironment);
        }
    }

    [Fact]
    public async Task BeforeStartEvent_RadiusEnvironmentResource_NotAnnotatedItself()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        // The environment resource itself is not a compute resource and should not get annotated
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(radiusEnv);
        Assert.Empty(annotations);
    }

    [Fact]
    public async Task BeforeStartEvent_NoErrors_WhenDashboardDisabled()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();

        // Should complete without throwing
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task BeforeStartEvent_NoRadiusEnvironment_NoAnnotationsAdded()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        Assert.Empty(annotations);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

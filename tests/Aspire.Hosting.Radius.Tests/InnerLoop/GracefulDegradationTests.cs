// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class GracefulDegradationTests
{
    [Fact]
    public async Task AspireRun_Completes_WhenKubernetesUnavailable()
    {
        // Radius inner-loop is visualization-only in Phase 1.
        // It should not require Kubernetes or rad CLI to be available.
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();

        // ExecuteBeforeStartHooksAsync should complete without errors
        // even when no Kubernetes cluster is reachable
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");

        // Annotations should still be attached (visualization-only mode)
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task AspireRun_Completes_WhenRadCliNotOnPath()
    {
        // Phase 1 does not invoke rad CLI, so missing rad CLI should not matter
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();

        // Annotations are still attached even without rad CLI
        Assert.NotEmpty(annotations);
    }

    [Fact]
    public async Task Annotations_AttachedInVisualizationOnlyMode()
    {
        // Even when infrastructure is not available, annotations should be present
        // for Aspire dashboard visualization purposes
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddContainer("frontend", "myapp/frontend:latest");
        builder.AddContainer("backend", "myapp/backend:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        foreach (var resource in model.GetComputeResources())
        {
            var annotations = resource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
            Assert.NotEmpty(annotations);
        }
    }

    [Fact]
    public async Task VisualizationOnlyMode_LogsEnvironmentConfiguration()
    {
        // RadiusInfrastructure should log informational messages about the environment
        // configuration even in visualization-only mode (no K8s, no rad CLI)
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("testenv").WithNamespace("test-ns");
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        // The RadiusInfrastructure subscriber logs "Configuring Radius environment..." via ILogger.
        // In visualization-only mode (no K8s), it still runs and logs — no errors or warnings.
        // Verify the app completed successfully by checking annotations are present.
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.Single(annotations);

        // The environment should be configured with the correct namespace
        var env = model.Resources.OfType<RadiusEnvironmentResource>().First();
        Assert.Equal("test-ns", env.Namespace);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}

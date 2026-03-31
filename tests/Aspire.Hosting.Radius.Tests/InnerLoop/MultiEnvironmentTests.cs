#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class MultiEnvironmentTests
{
    [Fact]
    public async Task MultipleEnvironments_CanCoexist()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();

        Assert.Equal(2, environments.Count);
        Assert.Contains(environments, e => e.Namespace == "dev");
        Assert.Contains(environments, e => e.Namespace == "staging");
    }

    [Fact]
    public async Task MultipleEnvironments_DifferentNamespaces_NoColl()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var envDev = model.Resources.OfType<RadiusEnvironmentResource>()
            .First(e => e.Name == "radius-dev");
        var envStaging = model.Resources.OfType<RadiusEnvironmentResource>()
            .First(e => e.Name == "radius-staging");

        // Different environments should have distinct namespaces
        Assert.NotEqual(envDev.Namespace, envStaging.Namespace);
    }

    [Fact]
    public async Task ResourcesWithNoExplicitTarget_GetFirstEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius-first")
            .WithRadiusNamespace("first");
        builder.AddRadiusEnvironment("radius-second")
            .WithRadiusNamespace("second");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        // The first environment encountered annotates the resource
        Assert.NotEmpty(annotations);
        var targetEnv = annotations[0].ComputeEnvironment as RadiusEnvironmentResource;
        Assert.NotNull(targetEnv);
        Assert.Equal("radius-first", targetEnv.Name);
    }

    [Fact]
    public async Task ResourcePreTargeted_NotOverriddenBySecondEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var env1 = builder.AddRadiusEnvironment("radius-first")
            .WithRadiusNamespace("first");
        builder.AddRadiusEnvironment("radius-second")
            .WithRadiusNamespace("second");

        var container = builder.AddContainer("api", "myimage", "latest");
        // Pre-target the container to the first environment
        container.Resource.Annotations.Add(new DeploymentTargetAnnotation(env1.Resource)
        {
            ComputeEnvironment = env1.Resource
        });

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        // Should not have been overridden by second environment
        var envTargets = annotations
            .Select(a => a.ComputeEnvironment)
            .OfType<RadiusEnvironmentResource>()
            .ToList();

        // The second environment should not have added a duplicate annotation
        Assert.All(envTargets, e => Assert.Equal("radius-first", e.Name));
    }

    [Fact]
    public async Task MultipleEnvironments_EachAnnotatesOnlyUntargetedResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        // Only the first environment that finds the resource untargeted should annotate it
        // The second environment should skip since it was already targeted
        var envNames = annotations
            .Select(a => a.ComputeEnvironment)
            .OfType<RadiusEnvironmentResource>()
            .Select(e => e.Name)
            .Distinct()
            .ToList();

        Assert.Single(envNames);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable ASPIRECOMPUTE003

using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusInfrastructureTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotation_ToContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var env = builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var annotation = container.Resource.GetDeploymentTargetAnnotation();
        Assert.NotNull(annotation);
        Assert.Same(env.Resource, annotation.ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotation_ToMultipleResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var env = builder.AddRadiusEnvironment("radius");
        var container1 = builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0");
        var container2 = builder.AddContainer("worker", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        Assert.Same(env.Resource, container1.Resource.GetDeploymentTargetAnnotation()?.ComputeEnvironment);
        Assert.Same(env.Resource, container2.Resource.GetDeploymentTargetAnnotation()?.ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_SkipsResources_TargetedToDifferentEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var radiusEnv1 = builder.AddRadiusEnvironment("radius1");
        var radiusEnv2 = builder.AddRadiusEnvironment("radius2");

        // Explicitly target container to radius2 only
        var container = builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithComputeEnvironment(radiusEnv2);

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        // Should only have annotation from radius2, not radius1
        var annotations = container.Resource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.Single(annotations);
        Assert.Same(radiusEnv2.Resource, annotations[0].ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_NoRadiusEnvironment_NoAnnotationsAdded()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // No AddRadiusEnvironment() call
        var container = builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var annotation = container.Resource.GetDeploymentTargetAnnotation();
        Assert.Null(annotation);
    }

    [Fact]
    public void IdempotentSubscriberRegistration()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // Call AddRadiusEnvironment twice
        builder.AddRadiusEnvironment("env1");
        builder.AddRadiusEnvironment("env2");

        var app = builder.Build();

        // Verify only one RadiusInfrastructure subscriber is registered
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .ToArray();

        Assert.Single(subscribers);
    }

    [Fact]
    public void RunMode_DoesNotAddResourceToModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        var env = builder.AddRadiusEnvironment("radius");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // In run mode, the resource should NOT be in the top-level model resources
        var radiusResources = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
        Assert.Empty(radiusResources);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}

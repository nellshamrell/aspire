#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusInfrastructureDiRegistrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersEventingSubscriber()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var app = builder.Build();
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();

        Assert.Contains(subscribers, s => s is RadiusInfrastructure);
    }

    [Fact]
    public void AddRadiusEnvironment_CalledMultipleTimes_RegistersSubscriberOnce()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("env1");
        builder.AddRadiusEnvironment("env2");

        var app = builder.Build();
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();

        var radiusSubscribers = subscribers.Where(s => s is RadiusInfrastructure).ToList();
        Assert.Single(radiusSubscribers);
    }

    [Fact]
    public void RadiusInfrastructure_ResolvesFromServiceProvider()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var app = builder.Build();
        var subscriber = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .FirstOrDefault();

        Assert.NotNull(subscriber);
    }

    [Fact]
    public async Task RadiusInfrastructure_SubscribesToBeforeStartEvent()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();

        // Execute the before start hooks to trigger the subscriber
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        Assert.NotEmpty(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotations[0].ComputeEnvironment);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

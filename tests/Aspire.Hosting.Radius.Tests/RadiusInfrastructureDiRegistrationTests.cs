// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusInfrastructureDiRegistrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersRadiusInfrastructureSubscriber()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();
        Assert.Contains(subscribers, s => s is RadiusInfrastructure);
    }

    [Fact]
    public void AddRadiusEnvironment_CalledMultipleTimes_RegistersSubscriberOnce()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("env1");
        builder.AddRadiusEnvironment("env2");

        using var app = builder.Build();
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();
        var radiusSubscribers = subscribers.Where(s => s is RadiusInfrastructure).ToList();
        Assert.Single(radiusSubscribers);
    }

    [Fact]
    public void RadiusInfrastructure_ResolvesFromServiceProvider()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var subscriber = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .FirstOrDefault();
        Assert.NotNull(subscriber);
    }

    [Fact]
    public async Task RadiusInfrastructure_SubscribeAsync_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var subscriber = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .Single();

        // SubscribeAsync should not throw — it registers event handlers
        var eventing = app.Services.GetRequiredService<Aspire.Hosting.Eventing.IDistributedApplicationEventing>();
        var executionContext = app.Services.GetRequiredService<DistributedApplicationExecutionContext>();
        await subscriber.SubscribeAsync(eventing, executionContext, CancellationToken.None);
    }
}

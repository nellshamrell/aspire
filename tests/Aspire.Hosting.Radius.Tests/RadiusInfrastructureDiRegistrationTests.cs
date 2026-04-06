// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusInfrastructureDiRegistrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersRadiusInfrastructureSubscriber()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();

        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();
        Assert.Contains(subscribers, s => s is RadiusInfrastructure);
    }

    [Fact]
    public void AddRadiusEnvironment_CalledMultipleTimes_RegistersSubscriberOnlyOnce()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("env1");
        builder.AddRadiusEnvironment("env2");

        using var app = builder.Build();

        var radiusSubscribers = app.Services
            .GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .ToList();

        Assert.Single(radiusSubscribers);
    }

    [Fact]
    public void RadiusInfrastructure_ResolvesFromServiceProvider()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();

        var subscriber = app.Services
            .GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .FirstOrDefault();

        Assert.NotNull(subscriber);
    }

    [Fact]
    public async Task RadiusInfrastructure_SubscribesAsyncToEventing()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("myservice", "nginx");

        // Build and execute hooks — if SubscribeAsync fails, this will throw
        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        // If we got here without throwing, the subscriber was successfully registered and invoked
        Assert.NotNull(model);
    }
}

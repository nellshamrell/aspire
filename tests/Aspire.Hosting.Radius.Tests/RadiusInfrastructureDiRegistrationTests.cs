// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

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

        builder.AddRadiusEnvironment();

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

        builder.AddRadiusEnvironment();

        var app = builder.Build();
        var subscriber = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .SingleOrDefault();

        Assert.NotNull(subscriber);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusInfrastructureDiRegistrationTests
{
    [Fact]
    public void AddRadiusEnvironment_RegistersRadiusInfrastructure()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment();

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

        // TryAddEventingSubscriber ensures only one RadiusInfrastructure is registered
        Assert.Single(subscribers, s => s is RadiusInfrastructure);
    }

    [Fact]
    public void RadiusInfrastructure_ResolvesFromServiceProvider()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment();

        using var app = builder.Build();
        var subscriber = app.Services.GetServices<IDistributedApplicationEventingSubscriber>()
            .OfType<RadiusInfrastructure>()
            .FirstOrDefault();

        Assert.NotNull(subscriber);
    }
}

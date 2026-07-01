// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-004, SC-005, US3 — a <c>WithReference</c> whose target lives in another group emits the
/// connection <c>source</c> as the target's fully-qualified UCP ID, while an in-group reference
/// keeps the bare identifier exactly as today.
/// </summary>
public class CrossGroupReferenceBicepTests
{
    [Fact]
    public void CrossGroupReference_EmitsFullUcpId_AsConnectionSource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // shared-data owns the cache; orders owns the api that references it across the boundary.
        builder.AddRadiusEnvironment("env-shared").WithRadiusResourceGroup("shared-data");
        var cache = builder.AddRedis("cache").WithRadiusResourceGroup("shared-data");

        builder.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(cache)
            .WithRadiusResourceGroup("orders");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var bicepByGroup = RadiusBicepPublishingContext.GenerateGroupedBicep(model);
        var ordersBicep = bicepByGroup["orders"];

        // The cross-group connection source is the cache's full UCP ID in the shared-data group.
        Assert.Contains(
            "source: '/planes/radius/local/resourceGroups/shared-data/providers/Applications.Datastores/redisCaches/cache'",
            ordersBicep);

        // It must NOT be emitted as a bare in-group identifier reference.
        Assert.DoesNotContain("source: cache.id", ordersBicep);
    }

    [Fact]
    public void InGroupReference_KeepsBareIdentifier_WhileCrossGroupUsesUcpId()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("env-shared").WithRadiusResourceGroup("shared-data");
        var sharedCache = builder.AddRedis("sharedcache").WithRadiusResourceGroup("shared-data");

        builder.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
        var localCache = builder.AddRedis("localcache").WithRadiusResourceGroup("orders");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(sharedCache)
            .WithReference(localCache)
            .WithRadiusResourceGroup("orders");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var ordersBicep = RadiusBicepPublishingContext.GenerateGroupedBicep(model)["orders"];

        // Cross-group reference → full UCP ID.
        Assert.Contains(
            "source: '/planes/radius/local/resourceGroups/shared-data/providers/Applications.Datastores/redisCaches/sharedcache'",
            ordersBicep);

        // In-group reference → bare identifier, unchanged.
        Assert.Contains("source: localcache.id", ordersBicep);
    }
}

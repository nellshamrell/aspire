// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-007, US1 — resources are partitioned by group so publish emits one
/// <c>groups/&lt;group&gt;/app.bicep</c> per group carrying only that group's resources.
/// This exercises the group-keyed <see cref="RadiusGroupOrchestrator"/> partition that the
/// per-group Bicep emission (T013/T014) consumes.
/// </summary>
public class PerGroupArtifactLayoutTests
{
    [Fact]
    public void TwoGroups_PartitionsResourcesAndEnvironmentsByGroup()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("env-shared").WithRadiusResourceGroup("shared-data");
        builder.AddContainer("cache", "redis", "latest").WithRadiusResourceGroup("shared-data");
        builder.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
        builder.AddContainer("orders", "orders-img", "latest").WithRadiusResourceGroup("orders");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var orchestrator = RadiusGroupOrchestrator.Create(model);
        var partitions = orchestrator.Partitions.ToDictionary(p => p.Group);

        Assert.Equal(2, partitions.Count);

        // shared-data carries only the cache and its environment.
        Assert.Contains(partitions["shared-data"].Resources, r => r.Name == "cache");
        Assert.DoesNotContain(partitions["shared-data"].Resources, r => r.Name == "orders");
        Assert.Contains(partitions["shared-data"].Environments, e => e.Name == "env-shared");

        // orders carries only the orders service and its environment.
        Assert.Contains(partitions["orders"].Resources, r => r.Name == "orders");
        Assert.DoesNotContain(partitions["orders"].Resources, r => r.Name == "cache");
        Assert.Contains(partitions["orders"].Environments, e => e.Name == "env-orders");
    }

    [Fact]
    public void GroupWithNoRoutedResources_IsNotEmittedAsAPartition()
    {
        // "empty" is only named as a cross-group environment target; it carries no resources
        // of its own, so it must not produce a partition.
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
        builder.AddContainer("api", "img", "latest").WithRadiusResourceGroup("platform");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var orchestrator = RadiusGroupOrchestrator.Create(model);

        Assert.Single(orchestrator.Partitions);
        Assert.Equal("platform", orchestrator.Partitions[0].Group);
    }
}

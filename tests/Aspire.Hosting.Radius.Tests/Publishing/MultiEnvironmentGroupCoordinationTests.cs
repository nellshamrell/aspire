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
/// FR-002, FR-007, research Decision 10 — multiple environments routed to groups are
/// coordinated by the single group-keyed orchestrator so each group is materialized exactly
/// once (a group with two environments is one artifact; two groups are one artifact each).
/// </summary>
public class MultiEnvironmentGroupCoordinationTests
{
    [Fact]
    public void TwoEnvironments_SameGroup_SinglePartition_MaterializesBoth()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("env-a").WithRadiusResourceGroup("shared");
        builder.AddRadiusEnvironment("env-b").WithRadiusResourceGroup("shared");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var orchestrator = RadiusGroupOrchestrator.Create(model);
        var shared = Assert.Single(orchestrator.Partitions, p => p.Group == "shared");

        Assert.Collection(
            shared.Environments.OrderBy(e => e.Name, StringComparer.Ordinal),
            e => Assert.Equal("env-a", e.Name),
            e => Assert.Equal("env-b", e.Name));

        // The first-declared environment is the deterministic, inert --environment default (FR-011).
        Assert.Equal("env-a", shared.FirstDeclaredEnvironmentName);
    }

    [Fact]
    public void TwoEnvironments_DistinctGroups_OneArtifactEach()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("env-a").WithRadiusResourceGroup("group-a");
        builder.AddRadiusEnvironment("env-b").WithRadiusResourceGroup("group-b");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var orchestrator = RadiusGroupOrchestrator.Create(model);

        Assert.Equal(2, orchestrator.Partitions.Count);
        Assert.Single(orchestrator.Partitions, p => p.Group == "group-a" && p.Environments.Count == 1);
        Assert.Single(orchestrator.Partitions, p => p.Group == "group-b" && p.Environments.Count == 1);
    }
}

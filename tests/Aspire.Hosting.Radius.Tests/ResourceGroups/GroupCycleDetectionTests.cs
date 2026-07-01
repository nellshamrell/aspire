// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.ResourceGroups;

/// <summary>
/// FR-006, SC-003, US3 — the unioned group dependency graph (cross-group references ∪
/// cross-group environment targets) must be acyclic. A cycle is rejected at configuration time
/// (before any publish/deploy) with <c>ASPIRERADIUS035</c>.
/// </summary>
public class GroupCycleDetectionTests
{
    private static DistributedApplicationModel BuildModel(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        configure(builder);
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    [Fact]
    public void ReferenceCycle_Throws_ASPIRERADIUS035_AtConfigTime()
    {
        // group-a's service references group-b's backing resource, and group-b's service
        // references group-a's backing resource → a reference cycle a ↔ b.
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-a").WithRadiusResourceGroup("group-a");
            b.AddRadiusEnvironment("env-b").WithRadiusResourceGroup("group-b");

            var backingA = b.AddRedis("backing-a").WithRadiusResourceGroup("group-a");
            var backingB = b.AddRedis("backing-b").WithRadiusResourceGroup("group-b");

            b.AddContainer("svc-a", "img", "latest")
                .WithReference(backingB)
                .WithRadiusResourceGroup("group-a");
            b.AddContainer("svc-b", "img", "latest")
                .WithReference(backingA)
                .WithRadiusResourceGroup("group-b");
        });

        var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
        Assert.Contains("ASPIRERADIUS035", ex.Message);
    }

    [Fact]
    public void MixedReferenceAndEnvironmentCycle_Throws_ASPIRERADIUS035_AtConfigTime()
    {
        // Union graph cycle from one reference edge and one environment-target edge. group-b owns no
        // environment (so its cross-group environment target is valid under ASPIRERADIUS036/037); all
        // of its resources deploy against group-a's environment:
        //   svc-a (group-a) references backing-b (group-b)          → edge a → b
        //   group-b resources deploy against group-a's environment  → edge b → a
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-a").WithRadiusResourceGroup("group-a");

            var backingB = b.AddRedis("backing-b").WithRadiusResourceGroup("group-b", environmentGroup: "group-a");

            b.AddContainer("svc-a", "img", "latest")
                .WithReference(backingB)
                .WithRadiusResourceGroup("group-a");
            b.AddContainer("svc-b", "img", "latest")
                .WithRadiusResourceGroup("group-b", environmentGroup: "group-a");
        });

        var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
        Assert.Contains("ASPIRERADIUS035", ex.Message);
    }
}

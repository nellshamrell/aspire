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
/// FR-009, SC-002, US2 — the deploy order is a topological sort of the unioned group dependency
/// graph (cross-group references ∪ cross-group environment targets): every group is deployed
/// after the groups it depends on.
/// </summary>
public class GroupDeployOrderTests
{
    private static DistributedApplicationModel BuildModel(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        configure(builder);
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    [Fact]
    public void ReferenceEdge_OrdersProviderBeforeConsumer()
    {
        // orders' api references a db owned by shared-data → shared-data must deploy first.
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-shared").WithRadiusResourceGroup("shared-data");
            var db = b.AddRedis("db").WithRadiusResourceGroup("shared-data");

            b.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
            b.AddContainer("api", "img", "latest")
                .WithReference(db)
                .WithRadiusResourceGroup("orders");
        });

        var order = RadiusGroupOrchestrator.Create(model).DeployOrder;

        Assert.Equal(new[] { "shared-data", "orders" }, order);
    }

    [Fact]
    public void EnvironmentTargetEdge_OrdersEnvOwnerBeforeConsumer()
    {
        // orders owns no environment; its api deploys against platform's environment
        // (a cross-group environment-target edge) → platform must deploy first.
        var model = BuildModel(b =>
        {
            b.AddContainer("api", "img", "latest")
                .WithRadiusResourceGroup("orders", environmentGroup: "platform");

            b.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
            b.AddRedis("platformcache").WithRadiusResourceGroup("platform");
        });

        var order = RadiusGroupOrchestrator.Create(model).DeployOrder.ToList();

        Assert.True(
            order.IndexOf("platform") < order.IndexOf("orders"),
            $"Expected 'platform' before 'orders' but got: {string.Join(", ", order)}");
    }
}

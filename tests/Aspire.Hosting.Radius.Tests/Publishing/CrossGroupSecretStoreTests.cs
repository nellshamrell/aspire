// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is used to route resources.
#pragma warning disable ASPIRERADIUS006 // Experimental: the secret-store APIs are under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class CrossGroupSecretStoreTests
{
    [Fact]
    public void CrossGroupConsumer_EmitsUcpId_AndOrdersStoreGroupFirst()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var user = builder.AddParameter("u", secret: true);
        var pass = builder.AddParameter("p", secret: true);

        // A shared group owns an environment and the secret store.
        builder.AddRadiusEnvironment("shared-env").WithRadiusResourceGroup("shared");
        var store = builder.AddRadiusSecretStore("registry-creds", RadiusSecretStoreType.BasicAuthentication)
            .WithData(d => { d.Add("username", user); d.Add("password", pass); });
        store.WithRadiusResourceGroup("shared");

        // A separate orders group owns another environment that consumes the shared store.
        var ordersEnv = builder.AddRadiusEnvironment("orders-env").WithRadiusResourceGroup("orders");
        ordersEnv.WithBicepRegistryAuthentication("myregistry.azurecr.io", store);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Deploy order: the store's group ('shared') deploys before the consuming group ('orders').
        var orchestrator = RadiusGroupOrchestrator.Create(model);
        var order = orchestrator.DeployOrder.ToList();
        Assert.True(order.IndexOf("shared") < order.IndexOf("orders"),
            $"Expected 'shared' before 'orders' in deploy order [{string.Join(", ", order)}].");

        // The cross-group consumer references the store by its fully-qualified UCP ID.
        var grouped = RadiusBicepPublishingContext.GenerateGroupedBicep(model);
        Assert.Contains(
            "/planes/radius/local/resourceGroups/shared/providers/Applications.Core/secretStores/registry-creds",
            grouped["orders"]);
    }
}

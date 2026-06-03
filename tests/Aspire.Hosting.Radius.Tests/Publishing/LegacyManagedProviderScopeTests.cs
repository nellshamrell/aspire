// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS001 // WithLegacyContainers is a transitional experimental API.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// A pure-legacy publish (only legacy <c>Applications.*</c> backing resources and
/// legacy containers, no UDT chain) must still emit the cloud provider scopes on the
/// legacy environment so a cloud-managed legacy resource can be resolved at deploy time.
/// </summary>
public class LegacyManagedProviderScopeTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string AzureRedisRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";

    private static string GenerateBicep(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        return new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);
    }

    [Fact]
    public void LegacyOnlyPublish_WithManagedResource_EmitsProviderScopeOnLegacyEnv()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client))
            .WithLegacyContainers();

        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });

        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Only the legacy chain is emitted (no UDT environment).
        Assert.Contains("Applications.Core/environments@", bicep);
        Assert.DoesNotContain("Radius.Core/environments@", bicep);

        // The Azure provider scope is emitted on the legacy environment.
        Assert.Contains($"/subscriptions/{Sub}/resourceGroups/{Rg}", bicep);
        Assert.Contains(AzureRedisRecipe, bicep);
    }
}

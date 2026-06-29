// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ManagedRecipePackEmissionTests
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
    public void ManagedResource_BindsCloudRecipe_NotLocalDevDefault()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // The redis type resolves to the cloud-targeting recipe, not local-dev.
        Assert.Contains(AzureRedisRecipe, bicep);
        Assert.DoesNotContain("local-dev/rediscaches", bicep);
    }

    [Fact]
    public void ManagedResource_ComputeStaysKubernetesWorkload()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        Assert.Contains("Radius.Compute/containers", bicep);
    }

    [Fact]
    public void ManagedResource_ConsumingWorkload_ReferencesItById()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Same .id connection as an in-cluster resource (no special-casing).
        Assert.Contains("source: cache.id", bicep);
    }

    // A cloud-managed recipe's intrinsic parameters on a native (Radius.*) type flow to the recipe
    // pack entry (where Radius honors them), not onto the resource instance (which Radius ignores
    // for UDT types). Regression for params previously being emitted on the instance and ignored.
    [Fact]
    public void ManagedResource_NativeType_RecipeParameters_EmittedOnRecipePack()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        // Retarget the backing resource to a native Radius.* type so it binds through the recipe pack.
        var cache = builder.AddRedis("cache")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("Radius.Data/redisCaches", "2025-08-01-preview"));
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe
        {
            RecipeLocation = AzureRedisRecipe,
            Parameters = { ["sku"] = "Premium", ["capacity"] = 2 },
        });

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Parameters must appear on the recipe pack entry (where Radius honors them for UDT types),
        // not on the native resource instance (where Radius ignores them).
        var packBlock = ExtractResourceBlock(bicep, "resource recipepack ");
        Assert.Contains(AzureRedisRecipe, packBlock);
        Assert.Contains("parameters: {", packBlock);
        Assert.Contains("sku: 'Premium'", packBlock);
        // Number preserved as a Bicep number, not a quoted string.
        Assert.Contains("capacity: 2", packBlock);
        Assert.DoesNotContain("capacity: '2'", packBlock);

        // The native instance carries no recipe/parameters block.
        var instanceBlock = ExtractResourceBlock(bicep, "resource cache ");
        Assert.DoesNotContain("recipe", instanceBlock);
        Assert.DoesNotContain("parameters", instanceBlock);
    }

    // Native (UDT) types bind one recipe — and one parameter set — per type via the shared pack.
    // Two same-type instances sharing a recipe location but supplying different parameters would
    // last-write-win silently, so this divergence is rejected (ASPIRERADIUS026).
    [Fact]
    public void ManagedResource_NativeType_DivergentParameters_Throws_ASPIRERADIUS026()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cacheA = builder.AddRedis("cachea")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("Radius.Data/redisCaches", "2025-08-01-preview"));
        var cacheB = builder.AddRedis("cacheb")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("Radius.Data/redisCaches", "2025-08-01-preview"));
        // Same recipe location, divergent parameters.
        env.WithManagedResource(cacheA, RadiusCloud.Azure, new RadiusRecipe
        {
            RecipeLocation = AzureRedisRecipe,
            Parameters = { ["sku"] = "Premium" },
        });
        env.WithManagedResource(cacheB, RadiusCloud.Azure, new RadiusRecipe
        {
            RecipeLocation = AzureRedisRecipe,
            Parameters = { ["sku"] = "Basic" },
        });

        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => GenerateBicep(app));
        Assert.Contains("ASPIRERADIUS026", ex.Message);
        Assert.Contains("recipe parameters", ex.Message);
    }

    // Slices a single top-level `resource <id> '...' = { ... }` block out of generated Bicep,
    // from the declaration marker to the next top-level `resource ` declaration (or end of file),
    // so assertions can target one resource without matching text in sibling resources.
    private static string ExtractResourceBlock(string bicep, string declarationMarker)
    {
        var start = bicep.IndexOf(declarationMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Expected to find '{declarationMarker}' in generated Bicep.");
        var next = bicep.IndexOf("\nresource ", start + declarationMarker.Length, StringComparison.Ordinal);
        return next >= 0 ? bicep[start..next] : bicep[start..];
    }
}

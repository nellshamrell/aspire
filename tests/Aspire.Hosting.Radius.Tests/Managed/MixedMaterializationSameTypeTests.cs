// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Managed;

public class MixedMaterializationSameTypeTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string AzureRedisRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";
    private const string AzureRedisRecipeV2 = "br:reg.azurecr.io/recipes/azure-rediscache:2.0";
    private const string AzureSqlRecipe = "br:reg.azurecr.io/recipes/azure-sql:1.0";
    private const string AzureSqlRecipeV2 = "br:reg.azurecr.io/recipes/azure-sql:2.0";
    private const string CustomLocalRedisRecipe = "br:reg.example.io/recipes/custom-rediscache:latest";

    private static string GenerateBicep(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        return new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);
    }

    [Fact]
    public void SameType_OneManaged_OneInCluster_BindPerInstance()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        var managedCache = builder.AddRedis("managedcache");
        var localCache = builder.AddRedis("localcache");
        env.WithManagedResource(managedCache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });

        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(managedCache)
            .WithReference(localCache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Two resources of the same type: one cloud, one in-cluster default. Both recipes
        // must materialize — the managed instance must not clobber the in-cluster default.
        Assert.Contains(AzureRedisRecipe, bicep);
        Assert.Contains("local-dev/rediscaches", bicep);

        // The managed instance binds to its own named recipe ('managedcache'), registered
        // alongside the type's 'default' in-cluster recipe (legacy redis type).
        Assert.Contains("name: 'managedcache'", bicep);
    }

    [Fact]
    public void SameLegacyType_TwoManaged_DifferentRecipes_BothBindPerInstance()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        var cacheA = builder.AddRedis("cachea");
        var cacheB = builder.AddRedis("cacheb");
        env.WithManagedResource(cacheA, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });
        env.WithManagedResource(cacheB, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipeV2 });

        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(cacheA)
            .WithReference(cacheB);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Both managed recipes must materialize. Before the divergence fix, the two legacy
        // instances collapsed onto the shared 'default' recipe name and the last one won.
        Assert.Contains(AzureRedisRecipe, bicep);
        Assert.Contains(AzureRedisRecipeV2, bicep);
        Assert.Contains("name: 'cachea'", bicep);
        Assert.Contains("name: 'cacheb'", bicep);
    }

    [Fact]
    public void SameUdtType_TwoManaged_DifferentRecipes_ThrowsAspireRadius026()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        var sqlA = builder.AddSqlServer("sqla");
        var sqlB = builder.AddSqlServer("sqlb");
        env.WithManagedResource(sqlA, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureSqlRecipe });
        env.WithManagedResource(sqlB, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureSqlRecipeV2 });

        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(sqlA)
            .WithReference(sqlB);

        using var app = builder.Build();

        // Radius user-defined types bind exactly one recipe per type per environment (recipe
        // packs map a type to a single recipe; no per-instance override or named recipes). Two
        // UDT instances of the same type resolving to different recipes cannot be represented,
        // so the publish must fail rather than silently deploy both with the wrong recipe.
        var ex = Assert.Throws<InvalidOperationException>(() => GenerateBicep(app));
        Assert.Contains("ASPIRERADIUS026", ex.Message);
        Assert.Contains("sqla", ex.Message);
        Assert.Contains("sqlb", ex.Message);
    }

    [Fact]
    public void SameUdtType_TwoManaged_SameRecipe_BindsAtTypeLevel()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        var sqlA = builder.AddSqlServer("sqla");
        var sqlB = builder.AddSqlServer("sqlb");
        env.WithManagedResource(sqlA, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureSqlRecipe });
        env.WithManagedResource(sqlB, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureSqlRecipe });

        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(sqlA)
            .WithReference(sqlB);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Both instances share one recipe, so the type binds it once through the recipe pack.
        Assert.Contains(AzureSqlRecipe, bicep);
    }

    [Fact]
    public void SameType_CustomInClusterRecipe_PlusManaged_DoesNotClobber()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        // An in-cluster sibling with a *custom* (non-null) recipe location, plus a managed
        // sibling of the same type. Both recipes are non-null, so the old null-keyed
        // "in-cluster" detection missed the divergence and clobbered the shared type entry.
        var localCache = builder.AddRedis("localcache")
            .PublishAsRadiusResource(r => r.Recipe = new RadiusRecipe { RecipeLocation = CustomLocalRedisRecipe });
        var managedCache = builder.AddRedis("managedcache");
        env.WithManagedResource(managedCache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });

        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(localCache)
            .WithReference(managedCache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        Assert.Contains(CustomLocalRedisRecipe, bicep);
        Assert.Contains(AzureRedisRecipe, bicep);
    }
}

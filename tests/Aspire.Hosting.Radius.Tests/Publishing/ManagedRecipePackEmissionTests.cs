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
}

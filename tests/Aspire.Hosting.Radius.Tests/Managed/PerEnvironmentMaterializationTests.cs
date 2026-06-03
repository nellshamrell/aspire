// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Managed;

public class PerEnvironmentMaterializationTests
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
    public void WithoutSelection_ResourceStaysInClusterDefault()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("dev")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // No managed selection => default local-dev recipe is used.
        Assert.Contains("local-dev/rediscaches", bicep);
        Assert.DoesNotContain(AzureRedisRecipe, bicep);
    }

    [Fact]
    public void WithSelection_SameTypeResource_MaterializesAsCloud()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("prod")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        // Same source app definition, but the selection drives cloud materialization.
        Assert.Contains(AzureRedisRecipe, bicep);
        Assert.DoesNotContain("local-dev/rediscaches", bicep);
    }
}

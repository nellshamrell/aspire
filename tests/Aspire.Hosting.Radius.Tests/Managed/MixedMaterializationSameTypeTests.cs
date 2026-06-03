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
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        var bicep = new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);

        // Two resources of the same type: one cloud, one in-cluster default.
        Assert.Contains(AzureRedisRecipe, bicep);
        Assert.Contains("local-dev/rediscaches", bicep);
    }
}

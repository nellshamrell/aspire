// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class ManagedRunWiringTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string AzureRedisRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";

    [Fact]
    public void RunMode_ManagedSelection_DoesNotAlterLocalContainerWiring()
    {
        var builder = DistributedApplication.CreateBuilder(); // run mode
        var env = builder.AddRadiusEnvironment("radius")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");

        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });

        // The selection is recorded for publish...
        Assert.Single(env.Resource.Annotations.OfType<RadiusManagedResourcesAnnotation>());

        // ...but the resource still runs as a normal local container in the inner loop.
        Assert.Single(cache.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.IsAssignableFrom<IResourceWithConnectionString>(cache.Resource);
    }
}

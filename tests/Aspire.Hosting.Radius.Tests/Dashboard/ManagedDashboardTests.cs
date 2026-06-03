// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Tests.Dashboard;

public class ManagedDashboardTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string AzureRedisRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";

    private static IResourceBuilder<RadiusEnvironmentResource> AzureEnv(IDistributedApplicationBuilder builder)
        => builder.AddRadiusEnvironment("radius")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

    [Fact]
    public void ManagedResource_ExposesCloudAndRecipe_ForDashboardDisplay()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRedisRecipe });

        var marker = cache.Resource.Annotations.OfType<RadiusManagedResourceAnnotation>().Single();
        Assert.Equal("radius", marker.EnvironmentName);
        Assert.Equal(RadiusCloud.Azure, marker.Cloud);
        Assert.Equal(AzureRedisRecipe, marker.RecipeLocation);
    }

    [Fact]
    public void NonManagedResource_HasNoManagedMarker()
    {
        var builder = DistributedApplication.CreateBuilder();
        AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        Assert.Empty(cache.Resource.Annotations.OfType<RadiusManagedResourceAnnotation>());
    }
}

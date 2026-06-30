// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Tests.Managed;

public class WithManagedResourceTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string AzureRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";

    private static IResourceBuilder<RadiusEnvironmentResource> AzureEnv(IDistributedApplicationBuilder builder)
        => builder.AddRadiusEnvironment("radius")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

    [Fact]
    public void WithManagedResource_HappyPath_ReturnsSameBuilder_AndAttachesSelection()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        var recipe = new RadiusRecipe { RecipeLocation = AzureRecipe };
        var returned = env.WithManagedResource(cache, RadiusCloud.Azure, recipe);

        Assert.Same(env, returned);

        var annotation = env.Resource.Annotations.OfType<RadiusManagedResourcesAnnotation>().Single();
        var selection = Assert.Contains("cache", (IDictionary<string, ManagedResourceSelection>)annotation.Selections);
        Assert.Equal(RadiusCloud.Azure, selection.Cloud);
        Assert.Same(recipe, selection.Recipe);
    }

    [Fact]
    public void WithManagedResource_AttachesDashboardMarker_OnTargetResource()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe });

        var marker = cache.Resource.Annotations.OfType<RadiusManagedResourceAnnotation>().Single();
        Assert.Equal("radius", marker.EnvironmentName);
        Assert.Equal(RadiusCloud.Azure, marker.Cloud);
        Assert.Equal(AzureRecipe, marker.RecipeLocation);
    }

    [Fact]
    public void WithManagedResource_RepeatedForSameResource_LastWriteWins()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe });
        var second = new RadiusRecipe { RecipeLocation = "br:reg.azurecr.io/recipes/azure-rediscache:2.0" };
        env.WithManagedResource(cache, RadiusCloud.Azure, second);

        var annotation = env.Resource.Annotations.OfType<RadiusManagedResourcesAnnotation>().Single();
        Assert.Single(annotation.Selections);
        Assert.Same(second, annotation.Selections["cache"].Recipe);

        // Only one dashboard marker for this environment remains.
        Assert.Single(cache.Resource.Annotations.OfType<RadiusManagedResourceAnnotation>());
    }

    [Fact]
    public void WithManagedResource_ConvenienceOverload_BuildsRecipeFromLocation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        env.WithManagedResource(cache, RadiusCloud.Azure, AzureRecipe);

        var annotation = env.Resource.Annotations.OfType<RadiusManagedResourcesAnnotation>().Single();
        Assert.Equal(AzureRecipe, annotation.Selections["cache"].Recipe.RecipeLocation);
    }

    [Theory]
    [InlineData(RadiusCloud.None)]
    [InlineData((RadiusCloud)42)]
    public void WithManagedResource_InvalidCloud_Throws(RadiusCloud cloud)
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            env.WithManagedResource(cache, cloud, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("cloud", ex.ParamName);
        Assert.Contains("require a target cloud", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WithManagedResource_NullResource_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);

        Assert.Throws<ArgumentNullException>(() =>
            env.WithManagedResource(null!, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));
    }

    [Fact]
    public void WithManagedResource_NullRecipe_Throws()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        Assert.Throws<ArgumentNullException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, (RadiusRecipe)null!));
    }
}

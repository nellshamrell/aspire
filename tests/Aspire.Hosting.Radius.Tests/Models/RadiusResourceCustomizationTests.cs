// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Tests.Models;

public class RadiusResourceCustomizationTests
{
    [Fact]
    public void Defaults_ProvisioningIsAutomatic()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Equal(RadiusResourceProvisioning.Automatic, customization.Provisioning);
    }

    [Fact]
    public void Defaults_RecipeIsNull()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Null(customization.Recipe);
    }

    [Fact]
    public void Defaults_HostIsNull()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Null(customization.Host);
    }

    [Fact]
    public void Defaults_PortIsNull()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Null(customization.Port);
    }

    [Fact]
    public void ManualProvisioning_CanSetHostAndPort()
    {
        var customization = new RadiusResourceCustomization
        {
            Provisioning = RadiusResourceProvisioning.Manual,
            Host = "pg.example.com",
            Port = 5432,
        };

        Assert.Equal(RadiusResourceProvisioning.Manual, customization.Provisioning);
        Assert.Equal("pg.example.com", customization.Host);
        Assert.Equal(5432, customization.Port);
    }

    [Fact]
    public void Recipe_CanBeConfigured()
    {
        var customization = new RadiusResourceCustomization
        {
            Recipe = new RadiusRecipe
            {
                Name = "azure-redis-premium",
                TemplatePath = "ghcr.io/myorg/recipes/azure-redis:v1",
            }
        };

        Assert.NotNull(customization.Recipe);
        Assert.Equal("azure-redis-premium", customization.Recipe.Name);
        Assert.Equal("ghcr.io/myorg/recipes/azure-redis:v1", customization.Recipe.TemplatePath);
    }

    [Fact]
    public void Recipe_ParametersCanBeAdded()
    {
        var recipe = new RadiusRecipe { Name = "custom" };
        recipe.Parameters["sku"] = "Premium";
        recipe.Parameters["capacity"] = 2;

        Assert.Equal(2, recipe.Parameters.Count);
        Assert.Equal("Premium", recipe.Parameters["sku"]);
        Assert.Equal(2, recipe.Parameters["capacity"]);
    }
}

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
    public void Defaults_ConnectionStringOverridesIsEmpty()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Empty(customization.ConnectionStringOverrides);
    }

    [Fact]
    public void CanSetManualProvisioning()
    {
        var customization = new RadiusResourceCustomization
        {
            Provisioning = RadiusResourceProvisioning.Manual,
            Host = "db.example.com",
            Port = 5432,
        };

        Assert.Equal(RadiusResourceProvisioning.Manual, customization.Provisioning);
        Assert.Equal("db.example.com", customization.Host);
        Assert.Equal(5432, customization.Port);
    }

    [Fact]
    public void CanSetCustomRecipe()
    {
        var recipe = new RadiusRecipe
        {
            Name = "custom-redis",
            TemplatePath = "ghcr.io/myorg/recipes/redis:latest",
        };

        var customization = new RadiusResourceCustomization
        {
            Recipe = recipe,
        };

        Assert.NotNull(customization.Recipe);
        Assert.Equal("custom-redis", customization.Recipe.Name);
        Assert.Equal("ghcr.io/myorg/recipes/redis:latest", customization.Recipe.TemplatePath);
    }

    [Fact]
    public void RecipeParametersCanBeSet()
    {
        var recipe = new RadiusRecipe
        {
            Name = "parameterized-redis",
            TemplatePath = "ghcr.io/myorg/recipes/redis:latest",
            Parameters = new Dictionary<string, string>
            {
                ["maxMemory"] = "256mb",
                ["evictionPolicy"] = "allkeys-lru",
            },
        };

        Assert.NotNull(recipe.Parameters);
        Assert.Equal(2, recipe.Parameters.Count);
        Assert.Equal("256mb", recipe.Parameters["maxMemory"]);
    }

    [Fact]
    public void ConnectionStringOverridesCanBePopulated()
    {
        var customization = new RadiusResourceCustomization();

        customization.ConnectionStringOverrides["default"] = "Server={HOST};Port={PORT};Database=mydb";

        Assert.Single(customization.ConnectionStringOverrides);
        Assert.Contains("Server={HOST}", customization.ConnectionStringOverrides["default"]);
    }
}

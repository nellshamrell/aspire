// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Tests.Models;

public class RadiusResourceCustomizationTests
{
    [Fact]
    public void Provisioning_DefaultsToAutomatic()
    {
        var customization = new RadiusResourceCustomization();
        Assert.Equal(RadiusResourceProvisioning.Automatic, customization.Provisioning);
    }

    [Fact]
    public void Recipe_DefaultsToNull()
    {
        var customization = new RadiusResourceCustomization();
        Assert.Null(customization.Recipe);
    }

    [Fact]
    public void Host_DefaultsToNull()
    {
        var customization = new RadiusResourceCustomization();
        Assert.Null(customization.Host);
    }

    [Fact]
    public void Port_DefaultsToNull()
    {
        var customization = new RadiusResourceCustomization();
        Assert.Null(customization.Port);
    }

    [Fact]
    public void CanSetManualProvisioning()
    {
        var customization = new RadiusResourceCustomization
        {
            Provisioning = RadiusResourceProvisioning.Manual,
            Host = "host.docker.internal",
            Port = 5432
        };

        Assert.Equal(RadiusResourceProvisioning.Manual, customization.Provisioning);
        Assert.Equal("host.docker.internal", customization.Host);
        Assert.Equal(5432, customization.Port);
    }

    [Fact]
    public void CanSetCustomRecipe()
    {
        var customization = new RadiusResourceCustomization
        {
            Recipe = new RadiusRecipe
            {
                Name = "my-recipe",
                TemplatePath = "ghcr.io/my-org/recipes/redis:latest"
            }
        };

        Assert.NotNull(customization.Recipe);
        Assert.Equal("my-recipe", customization.Recipe.Name);
        Assert.Equal("ghcr.io/my-org/recipes/redis:latest", customization.Recipe.TemplatePath);
    }

    [Fact]
    public void RadiusRecipe_Parameters_IsEmptyByDefault()
    {
        var recipe = new RadiusRecipe
        {
            Name = "test",
            TemplatePath = "test-path"
        };

        Assert.Empty(recipe.Parameters);
    }

    [Fact]
    public void RadiusRecipe_Parameters_CanBePopulated()
    {
        var recipe = new RadiusRecipe
        {
            Name = "test",
            TemplatePath = "test-path"
        };
        recipe.Parameters["key"] = "value";

        Assert.Single(recipe.Parameters);
        Assert.Equal("value", recipe.Parameters["key"]);
    }

    [Fact]
    public void CustomizationAnnotation_HoldsCustomization()
    {
        var customization = new RadiusResourceCustomization
        {
            Provisioning = RadiusResourceProvisioning.Manual
        };
        var annotation = new RadiusResourceCustomizationAnnotation(customization);

        Assert.Same(customization, annotation.Customization);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
    }

    [Fact]
    public void CustomizationAnnotation_ImplementsIResourceAnnotation()
    {
        var annotation = new RadiusResourceCustomizationAnnotation(new RadiusResourceCustomization());
        Assert.IsAssignableFrom<IResourceAnnotation>(annotation);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Annotations;

namespace Aspire.Hosting.Radius.Tests.Dashboard;

public class RecipeParameterDashboardTests
{
    [Fact]
    public void Configured_RecipeParameters_AreExposedForDisplay()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius")
            .WithRecipeParameters(p => { p["vpcId"] = "vpc-1"; p["port"] = 6379; })
            .WithRecipeParameters("Radius.Data/redisCaches", p => p["sku"] = "Premium");

        var ann = env.Resource.Annotations.OfType<RadiusRecipeParametersAnnotation>().Single();

        Assert.Equal("vpc-1", RadiusRecipeParameterDisplay.FormatValue(ann.EnvironmentWide["vpcId"]));
        Assert.Equal("6379", RadiusRecipeParameterDisplay.FormatValue(ann.EnvironmentWide["port"]));
        Assert.Equal("Premium", RadiusRecipeParameterDisplay.FormatValue(ann.ByResourceType["Radius.Data/redisCaches"]["sku"]));
    }

    [Fact]
    public void SecretBoundParameter_DisplaysParameterName_NotValue()
    {
        var builder = DistributedApplication.CreateBuilder();
        var secret = builder.AddParameter("recipeSecret", "TopSecretValue", secret: true);
        var env = builder.AddRadiusEnvironment("radius")
            .WithRecipeParameters(p => p["apiKey"] = secret);

        var ann = env.Resource.Annotations.OfType<RadiusRecipeParametersAnnotation>().Single();
        var display = RadiusRecipeParameterDisplay.FormatValue(ann.EnvironmentWide["apiKey"]);

        Assert.Equal("{recipeSecret}", display);
        Assert.DoesNotContain("TopSecretValue", display);
    }

    [Fact]
    public void ArrayAndObjectValues_RenderForDisplay()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius")
            .WithRecipeParameters(p =>
            {
                p["subnets"] = new[] { "a", "b" };
                p["settings"] = new Dictionary<string, object> { ["tier"] = "std" };
            });

        var ann = env.Resource.Annotations.OfType<RadiusRecipeParametersAnnotation>().Single();

        Assert.Equal("[a, b]", RadiusRecipeParameterDisplay.FormatValue(ann.EnvironmentWide["subnets"]));
        Assert.Equal("{ tier: std }", RadiusRecipeParameterDisplay.FormatValue(ann.EnvironmentWide["settings"]));
    }
}

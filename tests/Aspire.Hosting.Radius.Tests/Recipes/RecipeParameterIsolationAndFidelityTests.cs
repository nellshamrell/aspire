// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Recipes;

public class RecipeParameterIsolationAndFidelityTests
{
    private static string Publish(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        var context = new RadiusBicepPublishingContext(radiusEnv);
        return context.GenerateBicep(model);
    }

    // T029 — per-environment isolation: parameters on one environment do not leak to another.
    [Fact]
    public void RecipeParameters_AreScopedPerEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var dev = builder.AddRadiusEnvironment("dev").WithRecipeParameters(p => p["region"] = "dev-region");
        var prod = builder.AddRadiusEnvironment("prod").WithRecipeParameters(p => p["region"] = "prod-region");

        var devAnn = dev.Resource.Annotations.OfType<RadiusRecipeParametersAnnotation>().Single();
        var prodAnn = prod.Resource.Annotations.OfType<RadiusRecipeParametersAnnotation>().Single();

        Assert.NotSame(devAnn, prodAnn);
        Assert.Equal("dev-region", devAnn.EnvironmentWide["region"]);
        Assert.Equal("prod-region", prodAnn.EnvironmentWide["region"]);
        Assert.False(devAnn.EnvironmentWide.ContainsValue("prod-region"));
    }

    // T019 — per-resource parameters use the same serialization contract (type fidelity).
    [Fact]
    public void PerResourceParameters_PreserveBicepTypeFidelity()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddRedis("cache")
            .PublishAsRadiusResource(c => c.Recipe = new RadiusRecipe
            {
                Parameters =
                {
                    ["port"] = 6379,
                    ["enabled"] = true,
                    ["tags"] = new[] { "a", "b" },
                },
            });

        using var app = builder.Build();
        var bicep = Publish(app);

        Assert.Contains("port: 6379", bicep);
        Assert.Contains("enabled: true", bicep);
        Assert.Contains("'a'", bicep);
        Assert.DoesNotContain("port: '6379'", bicep);
        Assert.DoesNotContain("enabled: 'true'", bicep);
    }
}

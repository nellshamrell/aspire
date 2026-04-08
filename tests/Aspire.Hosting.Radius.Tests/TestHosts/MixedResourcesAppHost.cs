// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Creates a test host with mixed resource types: portable resources and containers
/// targeting a Radius environment.
/// </summary>
internal static class MixedResourcesAppHost
{
    public static IDistributedApplicationTestingBuilder CreateBuilder(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("radius");

        // Portable resources
        var redis = builder.AddRedis("cache");
        var postgres = builder.AddPostgres("db");

        // Container resource with references
        builder.AddContainer("api", "myapp/api:latest")
            .WithReference(redis)
            .WithReference(postgres);

        return builder;
    }

    public static IDistributedApplicationTestingBuilder CreateWithCustomRecipe(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("radius");

        builder.AddRedis("cache")
            .PublishAsRadiusResource(options =>
            {
                options.Recipe = new RadiusRecipe
                {
                    Name = "azure-redis",
                    TemplatePath = "ghcr.io/myorg/recipes/azure-redis:1.0"
                };
            });

        builder.AddContainer("api", "myapp/api:latest");

        return builder;
    }

    public static IDistributedApplicationTestingBuilder CreateWithManualProvisioning(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("radius");

        builder.AddRedis("cache")
            .PublishAsRadiusResource(options =>
            {
                options.Provisioning = RadiusResourceProvisioning.Manual;
                options.Host = "redis.example.com";
                options.Port = 6379;
            });

        builder.AddContainer("api", "myapp/api:latest");

        return builder;
    }

    public static IDistributedApplicationTestingBuilder CreateWithRecipeParameters(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("radius");

        builder.AddRedis("cache")
            .PublishAsRadiusResource(options =>
            {
                options.Recipe = new RadiusRecipe
                {
                    Name = "azure-redis",
                    TemplatePath = "ghcr.io/myorg/recipes/azure-redis:1.0",
                    Parameters = new Dictionary<string, object>
                    {
                        ["sku"] = "Premium",
                        ["capacity"] = 2,
                        ["enableNonSslPort"] = false
                    }
                };
            });

        return builder;
    }
}

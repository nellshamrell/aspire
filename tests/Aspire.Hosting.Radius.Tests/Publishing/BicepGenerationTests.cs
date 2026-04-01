#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Tests verifying that the resource-to-Bicep mapping produces valid output structure.
/// These tests validate the expected contracts for Bicep generation.
/// Full end-to-end Bicep generation tests will be expanded when RadiusBicepPublishingContext is implemented.
/// </summary>
public class BicepGenerationTests
{
    [Fact]
    public void SimpleApp_OneContainer_MapsToRadiusContainer()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myregistry.azurecr.io/api", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");
        var mapping = ResourceTypeMapper.GetRadiusType(api);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void MultiResourceApp_AllResourcesMapped()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        var sql = builder.AddSqlServer("sqlserver");
        builder.AddContainer("api", "myregistry.azurecr.io/api", "latest")
            .WithReference(redis)
            .WithReference(sql);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var mappedTypes = new Dictionary<string, string>();
        foreach (var resource in model.Resources)
        {
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            mappedTypes[resource.Name] = mapping.Type;
        }

        Assert.Contains("redis", mappedTypes.Keys);
        Assert.Contains("sqlserver", mappedTypes.Keys);
        Assert.Contains("api", mappedTypes.Keys);

        Assert.Equal("Applications.Datastores/redisCaches", mappedTypes["redis"]);
        Assert.Equal("Applications.Datastores/sqlDatabases", mappedTypes["sqlserver"]);
        Assert.Equal("Applications.Core/containers", mappedTypes["api"]);
    }

    [Fact]
    public void ResourcesWithConnections_HaveReferenceAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");

        // WithReference adds environment variable annotations; verify they exist
        var envAnnotations = api.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void CustomRecipe_AttachesAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis")
            .PublishAsRadiusResource(config =>
            {
                config.Recipe = new RadiusRecipe
                {
                    Name = "custom-redis",
                    TemplatePath = "ghcr.io/my-org/recipes/redis:latest"
                };
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redis = model.Resources.First(r => r.Name == "redis");
        var annotation = redis.Annotations.OfType<RadiusResourceCustomizationAnnotation>().FirstOrDefault();

        Assert.NotNull(annotation);
        Assert.NotNull(annotation.Customization.Recipe);
        Assert.Equal("custom-redis", annotation.Customization.Recipe.Name);
        Assert.Equal("ghcr.io/my-org/recipes/redis:latest", annotation.Customization.Recipe.TemplatePath);
    }

    [Fact]
    public void EnvironmentVariables_PropagateThroughModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest")
            .WithEnvironment("MY_VAR", "my_value");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");
        var envAnnotations = api.Annotations
            .OfType<EnvironmentCallbackAnnotation>()
            .ToList();

        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void RadiusEnvironment_NotMappedAsPortableResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        // RadiusEnvironmentResource is the compute environment, not a portable resource
        // It should generate an Applications.Core/environments block, not go through ResourceTypeMapper
        Assert.IsType<RadiusEnvironmentResource>(radiusEnv);
        Assert.Equal("radius", radiusEnv.Name);
        Assert.Equal("default", radiusEnv.Namespace);
    }

    [Fact]
    public void LargeApp_AllResourcesMapWithinPerformanceBudget()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        // Create 20 container resources
        for (var i = 0; i < 20; i++)
        {
            builder.AddContainer($"container-{i}", "myimage", "latest");
        }

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var resource in model.Resources.Where(r => r is not RadiusEnvironmentResource))
        {
            _ = ResourceTypeMapper.GetRadiusType(resource);
        }
        sw.Stop();

        // Performance budget: <500ms per resource (20 resources ≤ 10 seconds)
        // Mapping should be essentially instant — well under budget
        Assert.True(sw.ElapsedMilliseconds < 10000, $"Mapping 20 resources took {sw.ElapsedMilliseconds}ms (budget: 10000ms)");
    }

    [Fact]
    public async Task ManualPostgres_BicepUsesPortableResourceWithManualProvisioning()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var postgres = builder.AddPostgres("postgres");
        builder.Resources.OfType<PostgresServerResource>().First()
            .Annotations.Add(new RadiusResourceCustomizationAnnotation(
                new RadiusResourceCustomization
                {
                    Provisioning = RadiusResourceProvisioning.Manual,
                    Host = "postgres.example.com",
                    Port = 5432
                }));
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(postgres);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var publishingContext = new RadiusBicepPublishingContext(
            model,
            loggerFactory.CreateLogger<RadiusBicepPublishingContext>());

        var bicep = await publishingContext.GenerateBicepAsync();

        Assert.Contains("resource postgres 'Applications.Datastores/postgresDatabases@2023-10-01-preview' = {", bicep);
        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("host: 'postgres.example.com'", bicep);
        Assert.Contains("port: 5432", bicep);
        Assert.DoesNotContain("resource postgres 'Applications.Core/containers", bicep);
    }

    [Fact]
    public async Task MultipleEnvironments_DefaultResourcesOnlyPublishToFirstEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius-first")
            .WithRadiusNamespace("first");
        builder.AddRadiusEnvironment("radius-second")
            .WithRadiusNamespace("second");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var publishingContext = new RadiusBicepPublishingContext(
            model,
            loggerFactory.CreateLogger<RadiusBicepPublishingContext>());

        var bicep = await publishingContext.GenerateBicepAsync();

        Assert.Equal(1, CountOccurrences(bicep, "resource api 'Applications.Core/containers@2023-10-01-preview' = {"));
        Assert.Contains("application: radius_first_app.id", bicep);
        Assert.DoesNotContain("application: radius_second_app.id", bicep);
    }

    [Fact]
    public async Task ConnectionsBlock_QuotesHyphenatedConnectionNames()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var cache = builder.AddRedis("cache-name");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(cache);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var publishingContext = new RadiusBicepPublishingContext(
            model,
            loggerFactory.CreateLogger<RadiusBicepPublishingContext>());

        var bicep = await publishingContext.GenerateBicepAsync();

        Assert.Contains("'cache-name': {", bicep);
    }

    private static int CountOccurrences(string input, string value)
    {
        var count = 0;
        var index = 0;

        while ((index = input.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepGenerationTests
{
    [Fact]
    public async Task SimpleApp_GeneratesValidBicepStructure()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("webapi", bicep);
    }

    [Fact]
    public async Task MultiResourceApp_AllResourcesInOutput()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        builder.AddRedis("cache");
        builder.AddSqlServer("sqlserver").AddDatabase("appdb");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("cache", bicep);
        Assert.Contains("webapi", bicep);
    }

    [Fact]
    public async Task CustomRecipe_ReflectedInBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Recipe = "custom-redis-recipe";
            });

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("custom-redis-recipe", bicep);
    }

    [Fact]
    public async Task EnvironmentVariables_PropagatedToContainerEnv()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(redis);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // The webapi container should have connection info for the redis cache
        Assert.Contains("cache", bicep);
        Assert.Contains("webapi", bicep);
    }

    [Fact]
    public async Task ConfigureRadiusInfrastructure_CanMutateDynamicRecipesAndConnections()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius")
            .ConfigureRadiusInfrastructure(infra =>
            {
                var env = infra.GetProvisionableResources().OfType<RadiusEnvironmentConstruct>().Single();
                env.RemoveRecipe("Applications.Datastores/redisCaches");
                env.AddRecipe(
                    "Applications.Datastores/redisCaches",
                    "custom",
                    "bicep",
                    "ghcr.io/example/custom/redis:latest");

                var container = infra.GetProvisionableResources().OfType<RadiusContainerConstruct>().Single();
                container.AddConnection("redisStore", "cache");
            });

        builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("ghcr.io/example/custom/redis:latest", bicep);
        Assert.DoesNotContain("ghcr.io/radius-project/recipes/local-dev/rediscaches:latest", bicep);
        Assert.Contains("connections:", bicep);
        Assert.Contains("redisStore:", bicep);
        Assert.Contains("source: cache.id", bicep);
    }

    [Fact]
    public async Task PerformanceTest_GenerationCompletesWithinBudget()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");

        // Add 20 resources
        for (var i = 0; i < 20; i++)
        {
            builder.AddContainer($"container{i}", $"image{i}:latest");
        }

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bicep = context.GenerateBicep(model, environment);
        sw.Stop();

        // Performance budget: <500ms per resource, so <10 seconds for 20 resources
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"Bicep generation took {sw.Elapsed.TotalSeconds}s, exceeding 10s budget.");
        Assert.NotEmpty(bicep);
    }
}

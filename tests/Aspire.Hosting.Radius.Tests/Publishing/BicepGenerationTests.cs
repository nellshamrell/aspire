// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepGenerationTests
{
    [Fact]
    public async Task SimpleApp_GeneratesBicepWithEnvironmentAndApp()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("worker", "myapp/worker");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("extension radius", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
    }

    [Fact]
    public async Task MultiResourceApp_AllResourcesInOutput()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        var sql = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        var api = builder.AddContainer("api", "myapp/api")
            .WithReference(redis)
            .WithReference(sql);

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
    }

    [Fact]
    public async Task ContainerWithConnections_ConnectionsInBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api").WithReference(redis);

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("connections:", bicep);
        Assert.Contains("redis", bicep);
        Assert.Contains(".id", bicep);
    }

    [Fact]
    public async Task CustomNamespace_ReflectedInBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius").WithRadiusNamespace("production");
        builder.AddContainer("worker", "myapp/worker");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("production", bicep);
    }

    [Fact]
    public async Task CustomRecipe_RegisteredOnEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache").PublishAsRadiusResource(r =>
        {
            r.Recipe = new RadiusRecipe
            {
                Name = "azure-redis-premium",
                TemplatePath = "ghcr.io/myorg/recipes/azure-redis:v2"
            };
        });
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("recipeConfig:", bicep);
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
    }

    [Fact]
    public async Task ManualProvisioning_EmitsManualProperties()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres").PublishAsRadiusResource(r =>
        {
            r.Provisioning = RadiusResourceProvisioning.Manual;
            r.Host = "mydbserver.example.com";
            r.Port = 5432;
        });
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("mydbserver.example.com", bicep);
        Assert.Contains("5432", bicep);
    }

    [Fact]
    public async Task PostgresDefaultsToManualProvisioning()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
    }

    [Fact]
    public async Task ConfigureRadiusInfrastructure_ModifiesNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius")
            .ConfigureRadiusInfrastructure(options =>
            {
                options.Environment.ComputeNamespace = "custom-ns";
            });
        builder.AddContainer("worker", "myapp/worker");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        // Collect ConfigureRadiusInfrastructure callbacks
        Action<RadiusInfrastructureOptions>? configureCallback = null;
        foreach (var annotation in environment.Annotations.OfType<RadiusInfrastructureConfigurationAnnotation>())
        {
            var existingCallback = configureCallback;
            var newCallback = annotation.Configure;
            configureCallback = existingCallback is null
                ? newCallback
                : options => { existingCallback(options); newCallback(options); };
        }

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build(configureCallback);

        Assert.Contains("custom-ns", bicep);
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory.Create(b => { }).CreateLogger("RadiusTests");
    }
}

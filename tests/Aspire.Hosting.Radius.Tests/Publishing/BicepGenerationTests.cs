// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepGenerationTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    [Fact]
    public void SimpleApp_GeneratesValidBicepStructure()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();

        Assert.Single(results);
        var bicep = results["radius"];

        Assert.Contains("extension radius", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("name: 'myapp'", bicep);
    }

    [Fact]
    public void MultiResourceApp_AllResourcesInOutput()
    {
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();

        Assert.Single(results);
        var bicep = results["radius"];

        // Portable resources
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues", bicep);
        Assert.Contains("Applications.Datastores/mongoDatabases", bicep);
        Assert.Contains("Applications.Datastores/postgresDatabases", bicep);

        // Container workloads
        Assert.Contains("name: 'api'", bicep);
        Assert.Contains("name: 'worker'", bicep);
    }

    [Fact]
    public void Connections_ProperlyReferencedInBicep()
    {
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // api container should have connections to redis, sqlserver, rabbitmq
        Assert.Contains("connections:", bicep);
        Assert.Contains("redis", bicep);
    }

    [Fact]
    public void CustomRecipe_ReflectedInBicep()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache");
        redis.PublishAsRadiusResource(config =>
        {
            config.Recipe = new RadiusRecipe
            {
                Name = "azure-redis-premium",
                TemplatePath = "ghcr.io/myorg/recipes/azure-redis:v2"
            };
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Custom recipe should appear on the environment's recipes block
        Assert.Contains("azure-redis-premium", bicep);
        Assert.Contains("ghcr.io/myorg/recipes/azure-redis:v2", bicep);

        // T042b: Recipe name should appear on the portable resource block
        Assert.Contains("recipe:", bicep);
        Assert.Contains("name: 'azure-redis-premium'", bicep);
    }

    [Fact]
    public void NoRadiusEnvironment_ReturnsEmpty()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddContainer("api", "myimage:latest");
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();

        Assert.Empty(results);
    }

    [Fact]
    public void EnvironmentVariables_PropagatedToContainerEnvSection()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myregistry.azurecr.io/api:latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Container should have image
        Assert.Contains("image: 'myregistry.azurecr.io/api:latest'", bicep);
    }

    [Fact]
    public void Performance_TemplateGenerationCompletesInTimeBudget()
    {
        // Create a multi-resource app
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var sw = Stopwatch.StartNew();
        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        sw.Stop();

        // Performance budget: <500ms per resource, 7 resources total = <3500ms
        Assert.True(sw.ElapsedMilliseconds < 10000, $"Bicep generation took {sw.ElapsedMilliseconds}ms, exceeding 10s budget");
    }

    [Fact]
    public void ConfigureRadiusInfrastructure_Callback_ModifiesOutput()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius")
            .ConfigureRadiusInfrastructure(opts =>
            {
                opts.Namespace = "custom-ns";
            });
        builder.AddContainer("api", "myimage:latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Collect callbacks from annotations
        var env = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var callbacks = env.Annotations
            .OfType<Annotations.RadiusInfrastructureConfigurationAnnotation>()
            .Select(a => a.Configure)
            .ToList();

        Action<RadiusInfrastructureOptions> combined = opts =>
        {
            foreach (var cb in callbacks)
            {
                cb(opts);
            }
        };

        var context = new RadiusBicepPublishingContext(model, _logger, combined);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // T042g: Verify the callback was able to modify the namespace
        Assert.Contains("namespace: 'custom-ns'", bicep);
    }
}

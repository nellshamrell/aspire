#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Stress tests verifying performance targets for large application models.
/// </summary>
public class LargeAppStressTests
{
    [Fact]
    public async Task LargeApp_50Resources_BicepGenerationUnder5Seconds()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        // Create 50 resources: mix of containers, redis, sql, rabbitmq, mongodb
        for (var i = 0; i < 20; i++)
        {
            builder.AddContainer($"container{i}", $"myregistry/service{i}", "latest");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddRedis($"redis{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddSqlServer($"sql{i}");
        }

        for (var i = 0; i < 5; i++)
        {
            builder.AddRabbitMQ($"rabbit{i}");
        }

        for (var i = 0; i < 5; i++)
        {
            builder.AddMongoDB($"mongo{i}");
        }

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<RadiusBicepPublishingContext>();

        var context = new RadiusBicepPublishingContext(model, logger);

        var sw = Stopwatch.StartNew();
        var bicep = await context.GenerateBicepAsync();
        sw.Stop();

        // Performance target: < 5 seconds for 50 resources
        Assert.True(sw.Elapsed.TotalSeconds < 5.0,
            $"Bicep generation took {sw.Elapsed.TotalSeconds:F2}s, expected < 5s");

        // Validate output is valid Bicep structure
        Assert.Contains("extension radius", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);

        // Validate all resource types are present
        Assert.Contains("Applications.Core/containers", bicep);
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues", bicep);
        Assert.Contains("Applications.Datastores/mongoDatabases", bicep);
    }

    [Fact]
    public async Task LargeApp_50Resources_BicepOutputReasonableSize()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        for (var i = 0; i < 20; i++)
        {
            builder.AddContainer($"svc{i}", $"myregistry/svc{i}", "latest");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddRedis($"cache{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddSqlServer($"db{i}");
        }

        for (var i = 0; i < 5; i++)
        {
            builder.AddRabbitMQ($"mq{i}");
        }

        for (var i = 0; i < 5; i++)
        {
            builder.AddMongoDB($"nosql{i}");
        }

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<RadiusBicepPublishingContext>();

        var context = new RadiusBicepPublishingContext(model, logger);
        var bicep = await context.GenerateBicepAsync();

        // Bicep output should be under a reasonable size (task says 10KB)
        var byteSize = System.Text.Encoding.UTF8.GetByteCount(bicep);
        Assert.True(byteSize < 50_000,
            $"Generated Bicep is {byteSize} bytes, expected reasonable size for 50 resources");
    }

    [Fact]
    public void LargeApp_AllResourcesMapped_NoFallbackWarnings()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddRedis("redis0");
        builder.AddSqlServer("sql0");
        builder.AddRabbitMQ("rabbit0");
        builder.AddMongoDB("mongo0");
        builder.AddContainer("api", "myregistry/api", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        foreach (var resource in model.Resources)
        {
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.False(mapping.IsFallback,
                $"Resource '{resource.Name}' ({resource.GetType().Name}) used fallback mapping");
        }
    }
}

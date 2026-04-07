// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Performance;

/// <summary>
/// Stress tests verifying Bicep generation performance for large applications.
/// </summary>
public class LargeAppStressTests
{
    [Fact]
    public async Task FiftyResources_BicepGeneratesInUnderFiveSeconds()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        // Add 10 Redis instances
        for (var i = 0; i < 10; i++)
        {
            builder.AddRedis($"redis{i}");
        }

        // Add 10 SQL Server instances with databases
        for (var i = 0; i < 10; i++)
        {
            builder.AddSqlServer($"sql{i}").AddDatabase($"db{i}");
        }

        // Add 10 MongoDB instances
        for (var i = 0; i < 10; i++)
        {
            builder.AddMongoDB($"mongo{i}");
        }

        // Add 10 RabbitMQ instances
        for (var i = 0; i < 10; i++)
        {
            builder.AddRabbitMQ($"rabbit{i}");
        }

        // Add 10 containers referencing a subset of resources
        for (var i = 0; i < 10; i++)
        {
            builder.AddContainer($"svc{i}", $"myregistry/svc{i}:latest");
        }

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var stopwatch = Stopwatch.StartNew();
        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, NullLogger.Instance);
        var bicep = infraBuilder.Build();
        stopwatch.Stop();

        // Performance: must complete in under 5 seconds (SC-003 targets <10s for ≤20 resources)
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Bicep generation for 50 resources took {stopwatch.Elapsed.TotalSeconds:F2}s (limit: 5s)");

        // Validity: Bicep must contain core structure
        Assert.Contains("extension radius", bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);

        // Size: generated Bicep should be reasonable
        var sizeKb = bicep.Length / 1024.0;
        Assert.True(sizeKb < 100,
            $"Generated Bicep is {sizeKb:F1}KB (limit: 100KB)");
    }

    [Fact]
    public async Task FiftyResources_BicepContainsAllResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        for (var i = 0; i < 10; i++)
        {
            builder.AddRedis($"redis{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddSqlServer($"sql{i}").AddDatabase($"db{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddMongoDB($"mongo{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddRabbitMQ($"rabbit{i}");
        }

        for (var i = 0; i < 10; i++)
        {
            builder.AddContainer($"svc{i}", $"myregistry/svc{i}:latest");
        }

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, NullLogger.Instance);
        var bicep = infraBuilder.Build();

        // Verify all resource types are present
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Datastores/mongoDatabases", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
    }
}

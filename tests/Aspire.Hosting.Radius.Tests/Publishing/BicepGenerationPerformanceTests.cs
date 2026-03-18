// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepGenerationPerformanceTests
{
    [Fact]
    public async Task LargeApp_50Resources_BicepGenerationUnder5Seconds()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");

        // Add 15 Redis instances
        for (var i = 0; i < 15; i++)
        {
            builder.AddRedis($"cache{i}");
        }

        // Add 10 SQL Server instances with databases
        for (var i = 0; i < 10; i++)
        {
            builder.AddSqlServer($"sql{i}").AddDatabase($"db{i}");
        }

        // Add 5 RabbitMQ instances
        for (var i = 0; i < 5; i++)
        {
            builder.AddRabbitMQ($"mq{i}");
        }

        // Add 5 MongoDB instances
        for (var i = 0; i < 5; i++)
        {
            builder.AddMongoDB($"mongo{i}").AddDatabase($"nosql{i}");
        }

        // Add 15 container workloads
        for (var i = 0; i < 15; i++)
        {
            builder.AddContainer($"svc{i}", $"myregistry/service{i}:latest");
        }

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var sw = Stopwatch.StartNew();
        var bicep = context.GenerateBicep(model, environment);
        sw.Stop();

        // Performance: must complete in under 5 seconds
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"Bicep generation took {sw.Elapsed.TotalSeconds:F2}s, expected <5s");

        // Validate output is non-empty and contains expected structure
        Assert.NotEmpty(bicep);
        Assert.Contains("Applications.Core/environments", bicep);
        Assert.Contains("Applications.Core/applications", bicep);
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues", bicep);
        Assert.Contains("Applications.Datastores/mongoDatabases", bicep);
        Assert.Contains("Applications.Core/containers", bicep);
    }

    [Fact]
    public async Task LargeApp_GeneratedBicepIsReasonableSize()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");

        // Add 20 mixed resources
        for (var i = 0; i < 5; i++)
        {
            builder.AddRedis($"cache{i}");
            builder.AddSqlServer($"sql{i}").AddDatabase($"db{i}");
            builder.AddContainer($"svc{i}", $"myregistry/service{i}:latest");
            builder.AddRabbitMQ($"mq{i}");
        }

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Generated Bicep should be reasonable size (under 50KB for 20 resources)
        var sizeBytes = System.Text.Encoding.UTF8.GetByteCount(bicep);
        Assert.True(sizeBytes < 50 * 1024,
            $"Generated Bicep is {sizeBytes / 1024}KB, expected <50KB");
    }
}

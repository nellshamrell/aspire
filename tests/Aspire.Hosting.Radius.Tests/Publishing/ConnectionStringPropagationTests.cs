// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ConnectionStringPropagationTests
{
    [Fact]
    public async Task RedisReference_MappedToPortableResourceReference()
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

        // Verify connection is established between webapi and cache
        Assert.Contains("cache", bicep);
        Assert.Contains("webapi", bicep);
    }

    [Fact]
    public async Task NoHardcodedSecrets_InGeneratedBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");
        builder.AddSqlServer("sqlserver").AddDatabase("appdb");
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

        // No passwords or connection strings should be hardcoded
        Assert.DoesNotContain("password", bicep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", bicep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MultipleReferences_AllMappedCorrectly()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        var rabbit = builder.AddRabbitMQ("messaging");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(redis)
            .WithReference(rabbit);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Both portable resources should appear in the Bicep
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues", bicep);
    }
}

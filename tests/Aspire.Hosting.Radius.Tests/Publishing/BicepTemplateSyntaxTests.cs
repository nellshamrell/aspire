// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepTemplateSyntaxTests
{
    [Fact]
    public async Task GeneratedBicep_HasBalancedBraces()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("extension radius", bicep);

        var openBraces = bicep.Count(c => c == '{');
        var closeBraces = bicep.Count(c => c == '}');
        Assert.Equal(openBraces, closeBraces);
    }

    [Fact]
    public async Task ResourceTypes_HaveCorrectApiVersions()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddSqlServer("sql");
        builder.AddRabbitMQ("rabbit");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("Applications.Core/environments@2023-10-01-preview", bicep);
        Assert.Contains("Applications.Core/applications@2023-10-01-preview", bicep);
        Assert.Contains("Applications.Core/containers@2023-10-01-preview", bicep);
        Assert.Contains("Applications.Datastores/redisCaches@2023-10-01-preview", bicep);
        Assert.Contains("Applications.Datastores/sqlDatabases@2023-10-01-preview", bicep);
        Assert.Contains("Applications.Messaging/rabbitMQQueues@2023-10-01-preview", bicep);
    }

    [Fact]
    public async Task ResourceNames_AreQuotedStrings()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("worker", "myapp/worker");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("name: 'radius'", bicep);
        Assert.Contains("name: 'worker'", bicep);
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory.Create(b => { }).CreateLogger("RadiusTests");
    }
}

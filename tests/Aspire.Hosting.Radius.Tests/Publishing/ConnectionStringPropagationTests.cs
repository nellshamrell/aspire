// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ConnectionStringPropagationTests
{
    [Fact]
    public async Task WithReference_CreatesConnectionInBicep()
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
    }

    [Fact]
    public async Task MultipleReferences_AllConnectionsPresent()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        var rabbit = builder.AddRabbitMQ("rabbitmq");
        builder.AddContainer("api", "myapp/api")
            .WithReference(redis)
            .WithReference(rabbit);

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("redis", bicep);
        Assert.Contains("rabbitmq", bicep);
    }

    [Fact]
    public async Task NoHardcodedSecrets_InGeneratedBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.DoesNotContain("password", bicep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", bicep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", bicep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChildResourceReference_ResolvesToParent()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        var db = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        builder.AddContainer("api", "myapp/api").WithReference(db);

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("Applications.Datastores/sqlDatabases", bicep);
        Assert.Contains("connections:", bicep);
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory.Create(b => { }).CreateLogger("RadiusTests");
    }
}

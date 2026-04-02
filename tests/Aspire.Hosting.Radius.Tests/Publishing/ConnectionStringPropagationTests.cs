// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ConnectionStringPropagationTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    [Fact]
    public void WithReference_MapsToRadiusConnections()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "myimage:latest")
            .WithReference(redis);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Connections block should exist on the container
        Assert.Contains("connections:", bicep);
        Assert.Contains("'cache':", bicep);
        Assert.Contains("source:", bicep);
        Assert.Contains(".id", bicep);
    }

    [Fact]
    public void NoHardcodedSecrets_InGeneratedBicep()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "myimage:latest")
            .WithReference(redis);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // No passwords or connection strings in Bicep
        Assert.DoesNotContain("password", bicep, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", bicep, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChildResource_WithReference_ResolvesToParent()
    {
        // T042a: When WithReference targets a child resource (e.g., SqlServerDatabaseResource),
        // the connection should resolve to the parent portable resource.
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var sql = builder.AddSqlServer("sqlserver");
        var db = sql.AddDatabase("appdb");
        builder.AddContainer("api", "myimage:latest")
            .WithReference(db);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // The connection should be present and reference the parent SqlServer portable resource
        Assert.Contains("connections:", bicep);
        Assert.Contains("'appdb':", bicep);
        // The source should reference the sqlserver portable resource identifier
        Assert.Contains("sqlserver", bicep);
    }

    [Fact]
    public void MultipleReferences_AllMappedToConnections()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache");
        var rabbitmq = builder.AddRabbitMQ("messaging");
        builder.AddContainer("api", "myimage:latest")
            .WithReference(redis)
            .WithReference(rabbitmq);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        Assert.Contains("'cache':", bicep);
        Assert.Contains("'messaging':", bicep);
    }

    [Fact]
    public void HyphenatedResourceName_EmitsQuotedConnectionKey()
    {
        // T042d: Hyphenated names must be quoted in Bicep connection keys
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var redis = builder.AddRedis("cache-name");
        builder.AddContainer("api", "myimage:latest")
            .WithReference(redis);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // T042d: Connection key should be quoted to handle hyphen
        Assert.Contains("'cache-name':", bicep);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using RadiusModels = Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ResourceTypeMapperTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    [Fact]
    public void RedisResource_MapsToRedisCaches()
    {
        var builder = DistributedApplication.CreateBuilder();
        var redis = builder.AddRedis("cache");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(redis.Resource, _logger);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
        Assert.Equal(ResourceTypeMapper.RadiusApiVersion, mapping.ApiVersion);
        Assert.NotNull(mapping.DefaultRecipe);
        Assert.Contains("rediscaches", mapping.DefaultRecipe);
    }

    [Fact]
    public void SqlServerResource_MapsToSqlDatabases()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sql = builder.AddSqlServer("sqlserver");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(sql.Resource, _logger);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
        Assert.Equal(ResourceTypeMapper.RadiusApiVersion, mapping.ApiVersion);
        Assert.NotNull(mapping.DefaultRecipe);
        Assert.Contains("sqldatabases", mapping.DefaultRecipe);
    }

    [Fact]
    public void SqlServerDatabaseResource_MapsToSqlDatabases()
    {
        var builder = DistributedApplication.CreateBuilder();
        var sql = builder.AddSqlServer("sqlserver");
        var db = sql.AddDatabase("mydb");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(db.Resource, _logger);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void MongoDbResource_MapsToMongoDatabases()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mongo = builder.AddMongoDB("mongodb");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(mongo.Resource, _logger);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
        Assert.Contains("mongodatabases", mapping.DefaultRecipe);
    }

    [Fact]
    public void MongoDbDatabaseResource_MapsToMongoDatabases()
    {
        var builder = DistributedApplication.CreateBuilder();
        var mongo = builder.AddMongoDB("mongodb");
        var db = mongo.AddDatabase("catalogdb");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(db.Resource, _logger);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
    }

    [Fact]
    public void RabbitMqResource_MapsToRabbitMQQueues()
    {
        var builder = DistributedApplication.CreateBuilder();
        var rabbitmq = builder.AddRabbitMQ("rabbitmq");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(rabbitmq.Resource, _logger);

        Assert.Equal("Applications.Messaging/rabbitMQQueues", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
        Assert.Contains("rabbitmqqueues", mapping.DefaultRecipe);
    }

    [Fact]
    public void ContainerResource_MapsToContainers()
    {
        var builder = DistributedApplication.CreateBuilder();
        var container = builder.AddContainer("api", "myimage:latest");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(container.Resource, _logger);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void PostgresResource_MapsToPostgresDatabases_WithManualProvisioning()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(pg.Resource, _logger);

        Assert.Equal("Applications.Datastores/postgresDatabases", mapping.Type);
        Assert.Equal(RadiusModels.RadiusResourceProvisioning.Manual, mapping.DefaultProvisioning);
        Assert.Null(mapping.DefaultRecipe);
    }

    [Fact]
    public void PostgresDatabaseResource_MapsToPostgresDatabases()
    {
        var builder = DistributedApplication.CreateBuilder();
        var pg = builder.AddPostgres("postgres");
        var db = pg.AddDatabase("inventorydb");
        using var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusMapping(db.Resource, _logger);

        Assert.Equal("Applications.Datastores/postgresDatabases", mapping.Type);
    }

    [Fact]
    public void UnknownResource_FallsBackToContainers()
    {
        var resource = new TestCustomResource("custom-thing");

        var mapping = ResourceTypeMapper.GetRadiusMapping(resource, _logger);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void IsPortableResource_ReturnsTrueForNonContainerTypes()
    {
        var mapping = new ResourceMapping("Applications.Datastores/redisCaches", "2023-10-01-preview", "recipe");

        Assert.True(ResourceTypeMapper.IsPortableResource(mapping));
    }

    [Fact]
    public void IsPortableResource_ReturnsFalseForContainerType()
    {
        var mapping = ResourceTypeMapper.ContainerMapping;

        Assert.False(ResourceTypeMapper.IsPortableResource(mapping));
    }

    [Fact]
    public void DaprStateStoreResource_MapsToStateStores()
    {
        // Dapr hosting package is not available in this workspace, so test via
        // a stub resource whose type name matches the string-name mapper entry.
        var resource = new DaprStateStoreResource("my-statestore");

        var mapping = ResourceTypeMapper.GetRadiusMapping(resource, _logger);

        Assert.Equal("Applications.Dapr/stateStores", mapping.Type);
        Assert.True(ResourceTypeMapper.IsPortableResource(mapping));
    }

    [Fact]
    public void DaprPubSubResource_MapsToPubSubBrokers()
    {
        var resource = new DaprPubSubResource("my-pubsub");

        var mapping = ResourceTypeMapper.GetRadiusMapping(resource, _logger);

        Assert.Equal("Applications.Dapr/pubSubBrokers", mapping.Type);
        Assert.True(ResourceTypeMapper.IsPortableResource(mapping));
    }

    /// <summary>
    /// A dummy resource type unknown to the mapper for testing fallback behavior.
    /// </summary>
    private sealed class TestCustomResource(string name) : Resource(name);

    /// <summary>
    /// Stub resource whose type name matches the DaprStateStoreResource mapper entry.
    /// </summary>
    private sealed class DaprStateStoreResource(string name) : Resource(name);

    /// <summary>
    /// Stub resource whose type name matches the DaprPubSubResource mapper entry.
    /// </summary>
    private sealed class DaprPubSubResource(string name) : Resource(name);
}

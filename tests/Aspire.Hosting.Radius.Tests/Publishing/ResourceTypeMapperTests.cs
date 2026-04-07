// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ResourceTypeMapperTests
{
    [Fact]
    public void GetRadiusType_RedisResource_ReturnsCachesType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redis = builder.AddRedis("cache");
        var mapping = ResourceTypeMapper.GetRadiusType(redis.Resource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
        Assert.Equal("2023-10-01-preview", mapping.ApiVersion);
        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void GetRadiusType_SqlServerResource_ReturnsSqlType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var sql = builder.AddSqlServer("sqlserver");
        var mapping = ResourceTypeMapper.GetRadiusType(sql.Resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void GetRadiusType_MongoDBResource_ReturnsMongoType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var mongo = builder.AddMongoDB("mongodb");
        var mapping = ResourceTypeMapper.GetRadiusType(mongo.Resource);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void GetRadiusType_RabbitMQResource_ReturnsQueueType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var rabbit = builder.AddRabbitMQ("rabbitmq");
        var mapping = ResourceTypeMapper.GetRadiusType(rabbit.Resource);

        Assert.Equal("Applications.Messaging/rabbitMQQueues", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void GetRadiusType_PostgresResource_ReturnsPostgresType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var postgres = builder.AddPostgres("postgres");
        var mapping = ResourceTypeMapper.GetRadiusType(postgres.Resource);

        Assert.Equal("Applications.Datastores/postgresDatabases", mapping.Type);
        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void GetRadiusType_ContainerResource_ReturnsContainerType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var container = builder.AddContainer("worker", "myapp/worker");
        var mapping = ResourceTypeMapper.GetRadiusType(container.Resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
        Assert.Null(mapping.DefaultRecipe);
    }

    [Fact]
    public void IsPortableResource_Redis_ReturnsTrue()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var redis = builder.AddRedis("cache");
        Assert.True(ResourceTypeMapper.IsPortableResource(redis.Resource));
    }

    [Fact]
    public void IsPortableResource_Container_ReturnsFalse()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var container = builder.AddContainer("worker", "myapp/worker");
        Assert.False(ResourceTypeMapper.IsPortableResource(container.Resource));
    }

    [Fact]
    public void GetRadiusType_SqlServerDatabase_ReturnsSqlType()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var sql = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        var mapping = ResourceTypeMapper.GetRadiusType(sql.Resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }
}

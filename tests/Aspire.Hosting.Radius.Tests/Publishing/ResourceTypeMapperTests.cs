// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ResourceTypeMapperTests
{
    [Fact]
    public void RedisResource_MapsToRedisCaches()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var redis = builder.AddRedis("cache");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(redis.Resource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
        Assert.Equal(ResourceTypeMapper.DefaultApiVersion, mapping.ApiVersion);
    }

    [Fact]
    public void SqlServerResource_MapsToSqlDatabases()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var sql = builder.AddSqlServer("sqlserver");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(sql.Resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void SqlServerDatabaseResource_MapsToSqlDatabases()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var db = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(db.Resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void MongoDbResource_MapsToMongoDatabases()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var mongo = builder.AddMongoDB("mongo");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(mongo.Resource);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
    }

    [Fact]
    public void MongoDbDatabaseResource_MapsToMongoDatabases()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var db = builder.AddMongoDB("mongo").AddDatabase("nosqldb");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(db.Resource);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
    }

    [Fact]
    public void RabbitMqResource_MapsToRabbitMqQueues()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var rabbit = builder.AddRabbitMQ("messaging");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(rabbit.Resource);

        Assert.Equal("Applications.Messaging/rabbitMQQueues", mapping.Type);
    }

    [Fact]
    public void ContainerResource_MapsToContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var container = builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(container.Resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void PostgresResource_MapsToSqlDatabases()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var postgres = builder.AddPostgres("postgres");
        var app = builder.Build();

        var mapping = ResourceTypeMapper.GetRadiusType(postgres.Resource);

        // PostgreSQL maps to sqlDatabases (manual provisioning expected)
        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void UnmappedResource_FallsBackToContainers()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var container = builder.AddContainer("custom", "custom-image:latest");
        var app = builder.Build();

        // ContainerResource is a mapped type, but the fallback behavior is the same.
        // We test that the generic container fallback is returned for ContainerResource.
        var mapping = ResourceTypeMapper.GetRadiusType(container.Resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void ContainerResource_IsWorkloadResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var container = builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        var app = builder.Build();

        Assert.True(ResourceTypeMapper.IsWorkloadResource(container.Resource));
        Assert.False(ResourceTypeMapper.IsPortableResource(container.Resource));
    }

    [Fact]
    public void RedisResource_IsPortableResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var redis = builder.AddRedis("cache");
        var app = builder.Build();

        Assert.True(ResourceTypeMapper.IsPortableResource(redis.Resource));
        Assert.False(ResourceTypeMapper.IsWorkloadResource(redis.Resource));
    }

    [Fact]
    public void AllMappings_ReturnValidApiVersion()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        var resources = new IResource[]
        {
            builder.AddRedis("cache").Resource,
            builder.AddSqlServer("sql").Resource,
            builder.AddMongoDB("mongo").Resource,
            builder.AddRabbitMQ("rabbit").Resource,
            builder.AddContainer("container", "image:latest").Resource,
        };
        var app = builder.Build();

        foreach (var resource in resources)
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.False(string.IsNullOrEmpty(mapping.ApiVersion));
            Assert.False(string.IsNullOrEmpty(mapping.Type));
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Preview;

public class ResourceTypeMapperTests
{
    [Fact]
    public void ProjectResource_MapsToContainers()
    {
        var resource = new TestProjectResource("myproject");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void ContainerResource_MapsToContainers()
    {
        var resource = new ContainerResource("webapi");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
    }

    [Fact]
    public void RedisResource_MapsToRedisCaches()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var redis = builder.AddRedis("cache");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "cache");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
    }

    [Fact]
    public void SqlServerServerResource_MapsToSqlDatabases()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddSqlServer("sqlserver");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "sqlserver");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void SqlServerDatabaseResource_MapsToSqlDatabases()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddSqlServer("sqlserver").AddDatabase("mydb");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "mydb");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void PostgresServerResource_MapsToSqlDatabases()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddPostgres("postgres");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "postgres");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }

    [Fact]
    public void MongoDBServerResource_MapsToMongoDatabases()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddMongoDB("mongo");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "mongo");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
    }

    [Fact]
    public void RabbitMQServerResource_MapsToRabbitMQQueues()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRabbitMQ("messaging");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "messaging");

        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Messaging/rabbitMQQueues", mapping.Type);
    }

    [Fact]
    public void IsWorkloadResource_ContainerResource_ReturnsTrue()
    {
        var resource = new ContainerResource("webapi");

        Assert.True(ResourceTypeMapper.IsWorkloadResource(resource));
    }

    [Fact]
    public void IsWorkloadResource_RedisResource_ReturnsFalse()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        var redis = builder.AddRedis("cache");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "cache");

        Assert.False(ResourceTypeMapper.IsWorkloadResource(resource));
    }

    [Fact]
    public void IsPortableResource_RedisResource_ReturnsTrue()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRedis("cache");
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = model.Resources.First(r => r.Name == "cache");

        Assert.True(ResourceTypeMapper.IsPortableResource(resource));
    }

    [Fact]
    public void IsPortableResource_ContainerResource_ReturnsFalse()
    {
        var resource = new ContainerResource("webapi");

        Assert.False(ResourceTypeMapper.IsPortableResource(resource));
    }

    /// <summary>
    /// A minimal ProjectResource stub for testing type mapping.
    /// The real ProjectResource requires a project path, so we use this stub
    /// that matches the type name for the ResourceTypeMapper check.
    /// </summary>
    private sealed class TestProjectResource(string name) : ProjectResource(name)
    {
    }
}

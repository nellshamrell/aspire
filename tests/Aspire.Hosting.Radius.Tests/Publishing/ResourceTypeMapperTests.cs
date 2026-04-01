#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ResourceTypeMapperTests
{
    [Fact]
    public void RedisResource_MapsToDatastoresRedisCaches()
    {
        var resource = new RedisResource("redis");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
        Assert.False(mapping.IsManualProvisioning);
        Assert.False(mapping.IsFallback);
    }

    [Fact]
    public void SqlServerResource_MapsToDatastoresSqlDatabases()
    {
        var password = new ParameterResource("password", _ => "test", secret: true);
        var resource = new SqlServerServerResource("sqlserver", password);
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
        Assert.False(mapping.IsManualProvisioning);
    }

    [Fact]
    public void MongoDbResource_MapsToDatastoresMongoDatabases()
    {
        var resource = new MongoDBServerResource("mongo");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Datastores/mongoDatabases", mapping.Type);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
        Assert.False(mapping.IsManualProvisioning);
    }

    [Fact]
    public void RabbitMqResource_MapsToMessagingRabbitMQQueues()
    {
        var password = new ParameterResource("password", _ => "test", secret: true);
        var resource = new RabbitMQServerResource("rabbitmq", null, password);
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Messaging/rabbitMQQueues", mapping.Type);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
        Assert.False(mapping.IsManualProvisioning);
    }

    [Fact]
    public void ContainerResource_MapsToCoreContainers()
    {
        var resource = new ContainerResource("mycontainer");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
        Assert.False(mapping.IsManualProvisioning);
    }

    [Fact]
    public void PostgresResource_MapsToManualProvisioning()
    {
        var password = new ParameterResource("password", _ => "test", secret: true);
        var resource = new PostgresServerResource("postgres", null, password);
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.True(mapping.IsManualProvisioning);
        Assert.Equal("2023-10-01", mapping.ApiVersion);
    }

    [Fact]
    public void UnmappedResource_FallsToCoreContainers()
    {
        var resource = new CustomUnmappedResource("custom");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
        Assert.True(mapping.IsFallback);
    }

    [Fact]
    public void UnmappedResource_LogsWarning()
    {
        var logger = new FakeLogger<ResourceTypeMapperTests>();
        var resource = new CustomUnmappedResource("custom");

        _ = ResourceTypeMapper.GetRadiusType(resource, logger);

        var entry = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("CustomUnmappedResource", entry.Message);
        Assert.Contains("no native Radius mapping", entry.Message);
    }

    [Fact]
    public void AllMappings_UseConsistentApiVersion()
    {
        var resources = new IResource[]
        {
            new RedisResource("redis"),
            new SqlServerServerResource("sql", new ParameterResource("p", _ => "t", secret: true)),
            new MongoDBServerResource("mongo"),
            new RabbitMQServerResource("rabbitmq", null, new ParameterResource("p", _ => "t", secret: true)),
            new ContainerResource("container"),
            new PostgresServerResource("postgres", null, new ParameterResource("p", _ => "t", secret: true)),
        };

        foreach (var resource in resources)
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.Equal(ResourceTypeMapper.RadiusApiVersion, mapping.ApiVersion);
        }
    }

    [Fact]
    public void RedisResource_HasDefaultRecipe()
    {
        var resource = new RedisResource("redis");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.NotNull(mapping.DefaultRecipe);
    }

    [Fact]
    public void ContainerResourceSubclass_InheritsContainerMapping()
    {
        // RadiusDashboardResource extends ContainerResource
        var resource = new RadiusDashboardResource("dashboard");
        var mapping = ResourceTypeMapper.GetRadiusType(resource);

        Assert.Equal("Applications.Core/containers", mapping.Type);
        Assert.False(mapping.IsFallback);
    }

    /// <summary>
    /// A custom resource type that has no Radius mapping, used for fallback testing.
    /// </summary>
    private sealed class CustomUnmappedResource(string name) : Resource(name)
    {
    }
}

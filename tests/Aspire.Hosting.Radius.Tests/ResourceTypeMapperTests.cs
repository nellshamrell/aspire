// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests;

public class ResourceTypeMapperTests
{
    [Fact]
    public void Redis_maps_to_redisCaches()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var redis = builder.AddRedis("cache");
        var model = RadiusTestHelper.GetModel(builder);
        var resource = model.Resources.First(r => r.Name == "cache");

        var mapping = ResourceTypeMapper.GetMapping(resource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.RadiusType);
    }

    [Fact]
    public void Postgres_maps_to_postgresDatabases()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddPostgres("db");
        var model = RadiusTestHelper.GetModel(builder);
        var resource = model.Resources.First(r => r.Name == "db");

        var mapping = ResourceTypeMapper.GetMapping(resource);

        Assert.Equal("Applications.Datastores/postgresDatabases", mapping.RadiusType);
    }

    [Fact]
    public void IsPortableResource_returns_true_for_known_types()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRedis("cache");
        var model = RadiusTestHelper.GetModel(builder);
        var resource = model.Resources.First(r => r.Name == "cache");

        Assert.True(ResourceTypeMapper.IsPortableResource(resource));
    }

    [Fact]
    public void IsComputeResource_returns_true_for_container_type()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddContainer("api", "myapp/api:latest");
        var model = RadiusTestHelper.GetModel(builder);
        var resource = model.Resources.First(r => r.Name == "api");

        Assert.True(ResourceTypeMapper.IsComputeResource(resource));
    }

    [Fact]
    public void IsPortableResource_returns_false_for_containers()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddContainer("api", "myapp/api:latest");
        var model = RadiusTestHelper.GetModel(builder);
        var resource = model.Resources.First(r => r.Name == "api");

        Assert.False(ResourceTypeMapper.IsPortableResource(resource));
    }
}

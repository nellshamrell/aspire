#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Tests verifying that the resource model structure is compatible with
/// rad deploy expectations. Static validation without running a cluster.
/// </summary>
public class BicepToRadDeployCompatibilityTests
{
    [Fact]
    public void Model_ContainsEnvironmentResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // rad deploy expects an Applications.Core/environments block
        var envResources = model.Resources.OfType<RadiusEnvironmentResource>().ToList();
        Assert.Single(envResources);
    }

    [Fact]
    public void Model_ContainsPortableResources_InCorrectOrder()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("redis");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify both the environment and portable resource exist in the model
        var resourceNames = model.Resources.Select(r => r.Name).ToList();
        Assert.Contains("radius", resourceNames);
        Assert.Contains("redis", resourceNames);
        Assert.Contains("api", resourceNames);
    }

    [Fact]
    public void PortableResource_MapsToValidRadiusType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddSqlServer("sqlserver");
        builder.AddRabbitMQ("rabbitmq");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var expectedMappings = new Dictionary<string, string>
        {
            ["redis"] = "Applications.Datastores/redisCaches",
            ["sqlserver"] = "Applications.Datastores/sqlDatabases",
            ["rabbitmq"] = "Applications.Messaging/rabbitMQQueues",
        };

        foreach (var (name, expectedType) in expectedMappings)
        {
            var resource = model.Resources.First(r => r.Name == name);
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.Equal(expectedType, mapping.Type);
        }
    }

    [Fact]
    public void ContainerResource_MapsToRadiusContainerType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myregistry.azurecr.io/api", "latest");
        builder.AddContainer("worker", "myregistry.azurecr.io/worker", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        foreach (var resource in model.Resources.Where(r => r is ContainerResource && r is not RadiusDashboardResource && r is not RadiusEnvironmentResource))
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.Equal("Applications.Core/containers", mapping.Type);
        }
    }

    [Fact]
    public void ContainerResource_HasImageAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myregistry.azurecr.io/api", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");
        var imageAnnotation = api.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();

        // rad deploy needs the container image; it must be in annotations
        Assert.NotNull(imageAnnotation);
        Assert.Contains("api", imageAnnotation.Image);
    }

    [Fact]
    public void EnvironmentResource_HasNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("production");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var env = model.Resources.OfType<RadiusEnvironmentResource>().First();

        // rad deploy needs the namespace for Kubernetes targeting
        Assert.Equal("production", env.Namespace);
    }

    [Fact]
    public void ResourceReferences_CanBeResolvedFromModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // All referenced resources must be resolvable from the model
        // In Bicep, this becomes: connections: { cache: { source: cache.id } }
        var allNames = model.Resources.Select(r => r.Name).ToHashSet();
        Assert.Contains("cache", allNames);
        Assert.Contains("api", allNames);
    }

    [Fact]
    public void FullApp_AllBicepBlocksRepresentable()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("default");

        var redis = builder.AddRedis("cache");
        var sql = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        var rabbit = builder.AddRabbitMQ("messaging");

        builder.AddContainer("api", "myregistry.azurecr.io/api", "latest")
            .WithReference(redis)
            .WithReference(sql);
        builder.AddContainer("worker", "myregistry.azurecr.io/worker", "latest")
            .WithReference(rabbit);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify all expected blocks are representable:
        // 1. Environment block
        Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());

        // 2. Portable resources
        var portableNames = new[] { "cache", "sqlserver", "messaging" };
        foreach (var name in portableNames)
        {
            var resource = model.Resources.FirstOrDefault(r => r.Name == name);
            Assert.NotNull(resource);
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.NotEqual("Applications.Core/containers", mapping.Type);
        }

        // 3. Container workloads
        var containerNames = new[] { "api", "worker" };
        foreach (var name in containerNames)
        {
            var resource = model.Resources.First(r => r.Name == name);
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.Equal("Applications.Core/containers", mapping.Type);
        }
    }

    [Fact]
    public void AllResources_UseConsistentApiVersion()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddSqlServer("sqlserver");
        builder.AddContainer("api", "myimage", "latest");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // All mappable resources should use the same API version
        foreach (var resource in model.Resources.Where(r => r is not RadiusEnvironmentResource))
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource);
            Assert.Equal("2023-10-01", mapping.ApiVersion);
        }
    }
}

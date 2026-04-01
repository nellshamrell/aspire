#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Tests verifying that connection strings from WithReference() are correctly
/// propagated through the model for Radius Bicep generation.
/// </summary>
public class ConnectionStringPropagationTests
{
    [Fact]
    public void WithReference_Redis_CreatesEnvironmentAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");
        var envAnnotations = api.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();

        // WithReference should produce environment annotations for connection string propagation
        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithReference_SqlServer_CreatesEnvironmentAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var sql = builder.AddSqlServer("sqlserver")
            .AddDatabase("appdb");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(sql);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");
        var envAnnotations = api.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();

        Assert.NotEmpty(envAnnotations);
    }

    [Fact]
    public void WithReference_MapsToPortableResourceType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // The referenced Redis resource should map to a Radius portable type
        var redisResource = model.Resources.First(r => r.Name == "cache");
        var mapping = ResourceTypeMapper.GetRadiusType(redisResource);

        Assert.Equal("Applications.Datastores/redisCaches", mapping.Type);
        Assert.False(mapping.IsManualProvisioning);
    }

    [Fact]
    public void MultipleReferences_AllCreateAnnotations()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        var rabbit = builder.AddRabbitMQ("messaging");
        builder.AddContainer("worker", "myimage", "latest")
            .WithReference(redis)
            .WithReference(rabbit);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var worker = model.Resources.First(r => r.Name == "worker");
        var envAnnotations = worker.Annotations.OfType<EnvironmentCallbackAnnotation>().ToList();

        // Should have annotations for both references
        Assert.True(envAnnotations.Count >= 2,
            $"Expected at least 2 environment annotations for 2 references, got {envAnnotations.Count}");
    }

    [Fact]
    public void NoHardcodedSecrets_InResourceModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var sql = builder.AddSqlServer("sqlserver");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(sql);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var api = model.Resources.First(r => r.Name == "api");

        // The container resource should not contain raw connection strings in its annotations.
        // Connection strings should be generated through environment callback annotations,
        // which produce references rather than hardcoded values.
        var resourceAnnotationTypes = api.Annotations.Select(a => a.GetType().Name).ToList();
        Assert.DoesNotContain("ConnectionStringAnnotation", resourceAnnotationTypes);
    }

    [Fact]
    public void ReferencedResources_AreInModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        var mongo = builder.AddMongoDB("mongo");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(redis)
            .WithReference(mongo);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // All referenced resources should be present in the model
        var resourceNames = model.Resources.Select(r => r.Name).ToHashSet();
        Assert.Contains("cache", resourceNames);
        Assert.Contains("mongo", resourceNames);
        Assert.Contains("api", resourceNames);
    }

    [Fact]
    public void ChildResourceReference_CanBeResolved()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        var sqlDb = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        builder.AddContainer("api", "myimage", "latest")
            .WithReference(sqlDb);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // The child database resource should be in the model
        var dbResource = model.Resources.FirstOrDefault(r => r.Name == "appdb");
        Assert.NotNull(dbResource);

        // The parent SQL Server should also be in the model
        var sqlResource = model.Resources.FirstOrDefault(r => r.Name == "sqlserver");
        Assert.NotNull(sqlResource);

        // The parent should map to a Radius type
        var mapping = ResourceTypeMapper.GetRadiusType(sqlResource);
        Assert.Equal("Applications.Datastores/sqlDatabases", mapping.Type);
    }
}

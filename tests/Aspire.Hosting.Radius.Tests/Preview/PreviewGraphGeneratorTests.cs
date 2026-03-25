// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Preview;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Preview;

public class PreviewGraphGeneratorTests : IDisposable
{
    private readonly string _tempDir;

    public PreviewGraphGeneratorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"radius-preview-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_TwoResourcesWithReference_WritesThreeJsonFiles()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var cache = builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(cache);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        Assert.True(File.Exists(Path.Combine(_tempDir, "status.json")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "applications.json")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "graph.json")));
    }

    [Fact]
    public async Task GenerateAsync_FiltersOutRadiusEnvironmentAndDashboard()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);
        // Should only contain the webapi container, not the radius env or dashboard
        Assert.Single(graph.Resources);
        Assert.Equal("webapi", graph.Resources[0].Name);
        Assert.DoesNotContain(graph.Resources, r => r.Name.Contains("radius"));
    }

    [Fact]
    public async Task GenerateAsync_BuildsBidirectionalConnections()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var cache = builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(cache);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);

        var webapi = graph.Resources.First(r => r.Name == "webapi");
        var cacheResource = graph.Resources.First(r => r.Name == "cache");

        // Workload should have outbound connection to portable
        Assert.Single(webapi.Connections);
        Assert.Equal("cache", webapi.Connections[0].Name);
        Assert.Equal("Outbound", webapi.Connections[0].Direction);

        // Portable should have inbound connection from workload
        Assert.Single(cacheResource.Connections);
        Assert.Equal("webapi", cacheResource.Connections[0].Name);
        Assert.Equal("Inbound", cacheResource.Connections[0].Direction);
    }

    [Fact]
    public async Task GenerateAsync_GeneratesCorrectSyntheticResourceIds()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);
        var webapi = graph.Resources.First(r => r.Name == "webapi");
        Assert.Equal("/planes/radius/local/resourceGroups/default/providers/Applications.Core/containers/webapi", webapi.Id);
    }

    [Fact]
    public async Task GenerateAsync_SetsProvisioningStateToPreview()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        builder.AddRedis("cache");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);
        Assert.All(graph.Resources, r => Assert.Equal("Preview", r.ProvisioningState));
    }

    [Fact]
    public async Task GenerateAsync_StatusJson_HasCorrectMetadata()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("my-namespace");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");
        builder.AddRedis("cache");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var statusJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "status.json"));
        var status = JsonSerializer.Deserialize<PreviewStatus>(statusJson);

        Assert.NotNull(status);
        Assert.True(status.PreviewMode);
        Assert.Equal("my-namespace", status.Namespace);
        Assert.Equal(2, status.ResourceCount);
    }

    [Fact]
    public async Task GenerateAsync_ApplicationsJson_MatchesRadiusFormat()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var applicationsJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "applications.json"));
        var applications = JsonSerializer.Deserialize<PreviewApplicationList>(applicationsJson);

        Assert.NotNull(applications);
        Assert.Single(applications.Value);
        Assert.Equal("Applications.Core/applications", applications.Value[0].Type);
        Assert.Equal("Preview", applications.Value[0].Properties.ProvisioningState);
    }

    [Fact]
    public async Task GenerateAsync_CustomNamespace_UsedInResourceIds()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("pipeline-test");

        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);
        Assert.Contains("/resourceGroups/pipeline-test/", graph.Resources[0].Id);
    }

    [Fact]
    public async Task GenerateAsync_ZeroResources_GeneratesStatusWithZeroCount()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        // No other resources added

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var statusJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "status.json"));
        var status = JsonSerializer.Deserialize<PreviewStatus>(statusJson);

        Assert.NotNull(status);
        Assert.True(status.PreviewMode);
        Assert.Equal(0, status.ResourceCount);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);
        Assert.Empty(graph.Resources);
    }

    [Fact]
    public async Task GenerateAsync_CircularReferences_NoDuplication()
    {
        var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var cache = builder.AddRedis("cache");
        var webapi = builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(cache);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewGraphGenerator>();
        var generator = new PreviewGraphGenerator(logger);

        await generator.GenerateAsync(model, environment, _tempDir);

        var graphJson = await File.ReadAllTextAsync(Path.Combine(_tempDir, "graph.json"));
        var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson);

        Assert.NotNull(graph);

        var webapiResource = graph.Resources.First(r => r.Name == "webapi");
        var cacheResource = graph.Resources.First(r => r.Name == "cache");

        // Each resource should have exactly one connection (no duplication)
        Assert.Single(webapiResource.Connections);
        Assert.Single(cacheResource.Connections);
    }
}

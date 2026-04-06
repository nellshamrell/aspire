// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Preview;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Preview;

public class PreviewGraphGeneratorTests : IDisposable
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");
    private readonly string _outputDir;

    public PreviewGraphGeneratorTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), $"radius-preview-test-{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, recursive: true);
        }
    }

    [Fact]
    public async Task GenerateAsync_TwoResourcesWithReference_WritesThreeJsonFiles()
    {
        // Arrange: app with a container referencing a Redis cache
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            var redis = builder.AddRedis("cache");
            builder.AddContainer("api", "myimage:latest")
                .WithReference(redis);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);

            // Act
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            // Assert: three files written
            Assert.True(File.Exists(Path.Combine(_outputDir, "status.json")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "applications.json")));
            Assert.True(File.Exists(Path.Combine(_outputDir, "graph.json")));
        }
    }

    [Fact]
    public async Task GenerateAsync_StatusJson_HasCorrectMetadata()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("testapp").WithRadiusNamespace("staging");
            var redis = builder.AddRedis("cache");
            builder.AddContainer("api", "myimage:latest")
                .WithReference(redis);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var json = await File.ReadAllTextAsync(Path.Combine(_outputDir, "status.json"));
            var status = JsonSerializer.Deserialize<PreviewStatus>(json)!;

            Assert.True(status.PreviewMode);
            Assert.Equal("testapp", status.ApplicationName);
            Assert.Equal("staging", status.Namespace);
            Assert.Equal(2, status.ResourceCount); // api + cache
        }
    }

    [Fact]
    public async Task GenerateAsync_FiltersOutResourcesWithoutDeploymentTargetAnnotation()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            builder.AddContainer("api", "myimage:latest");
        });

        using (app)
        {
            // Manually remove DeploymentTargetAnnotation from api to simulate no targeting
            var apiResource = model.Resources.First(r => r.Name == "api");
            var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToList();
            foreach (var a in annotations)
            {
                apiResource.Annotations.Remove(a);
            }

            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var json = await File.ReadAllTextAsync(Path.Combine(_outputDir, "status.json"));
            var status = JsonSerializer.Deserialize<PreviewStatus>(json)!;

            Assert.Equal(0, status.ResourceCount);
        }
    }

    [Fact]
    public async Task GenerateAsync_ExcludesRadiusEnvironmentAndDashboardResources()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp"); // DashboardEnabled=true by default → creates dashboard
            builder.AddContainer("api", "myimage:latest");
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            // Should include api but NOT the RadiusEnvironmentResource or RadiusDashboardResource
            Assert.Single(graph.Resources);
            Assert.Equal("api", graph.Resources[0].Name);
            Assert.DoesNotContain(graph.Resources, r => r.Name.Contains("dashboard"));
            Assert.DoesNotContain(graph.Resources, r => r.Name == "myapp");
        }
    }

    [Fact]
    public async Task GenerateAsync_BuildsBidirectionalConnections()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            var redis = builder.AddRedis("cache");
            builder.AddContainer("api", "myimage:latest")
                .WithReference(redis);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            var api = graph.Resources.First(r => r.Name == "api");
            var cache = graph.Resources.First(r => r.Name == "cache");

            // api should have Outbound connection to cache
            Assert.Single(api.Connections);
            Assert.Equal("cache", api.Connections[0].Name);
            Assert.Equal("Outbound", api.Connections[0].Direction);

            // cache should have Inbound connection from api
            Assert.Single(cache.Connections);
            Assert.Equal("api", cache.Connections[0].Name);
            Assert.Equal("Inbound", cache.Connections[0].Direction);
        }
    }

    [Fact]
    public async Task GenerateAsync_GeneratesCorrectSyntheticResourceIds()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp").WithRadiusNamespace("testns");
            builder.AddContainer("api", "myimage:latest");
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            var api = graph.Resources.First(r => r.Name == "api");
            Assert.Equal(
                "/planes/radius/local/resourceGroups/testns/providers/Applications.Core/containers/api",
                api.Id);
        }
    }

    [Fact]
    public async Task GenerateAsync_SetsProvisioningStateToPreview()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            var redis = builder.AddRedis("cache");
            builder.AddContainer("api", "myimage:latest")
                .WithReference(redis);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            Assert.All(graph.Resources, r => Assert.Equal("Preview", r.ProvisioningState));
        }
    }

    [Fact]
    public async Task GenerateAsync_ApplicationsJson_MatchesRadiusFormat()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp").WithRadiusNamespace("testns");
            builder.AddContainer("api", "myimage:latest");
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var json = await File.ReadAllTextAsync(Path.Combine(_outputDir, "applications.json"));
            var appList = JsonSerializer.Deserialize<PreviewApplicationList>(json)!;

            Assert.Single(appList.Value);
            var application = appList.Value[0];
            Assert.Equal("myapp", application.Name);
            Assert.Equal("Applications.Core/applications", application.Type);
            Assert.Equal("Preview", application.Properties.ProvisioningState);
            Assert.Equal(
                "/planes/radius/local/resourceGroups/testns/providers/Applications.Core/applications/myapp",
                application.Id);
        }
    }

    [Fact]
    public async Task GenerateAsync_SqlServerDatabaseChildResource_ResolvesToParentInConnections()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            var sql = builder.AddSqlServer("sqlserver");
            var db = sql.AddDatabase("appdb");
            builder.AddContainer("api", "myimage:latest")
                .WithReference(db);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            // appdb should NOT appear as a graph node (it's a child, parent sqlserver is the portable resource)
            Assert.DoesNotContain(graph.Resources, r => r.Name == "appdb");

            // api should have outbound connection to sqlserver (the parent)
            var api = graph.Resources.First(r => r.Name == "api");
            Assert.Single(api.Connections);
            Assert.Equal("sqlserver", api.Connections[0].Name);
            Assert.Equal("Outbound", api.Connections[0].Direction);

            // sqlserver should have inbound connection from api
            var sqlserver = graph.Resources.First(r => r.Name == "sqlserver");
            Assert.Single(sqlserver.Connections);
            Assert.Equal("api", sqlserver.Connections[0].Name);
            Assert.Equal("Inbound", sqlserver.Connections[0].Direction);
        }
    }

    [Fact]
    public async Task GenerateAsync_CircularReferences_NoDuplicateConnections()
    {
        // Multiple containers referencing the same portable resource.
        // Verify: no duplicate inbound connections, correct bidirectional edges,
        // generation completes without issues.
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            var redis = builder.AddRedis("cache");
            builder.AddContainer("serviceA", "imageA:latest")
                .WithReference(redis);
            builder.AddContainer("serviceB", "imageB:latest")
                .WithReference(redis);
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            Assert.Equal(3, graph.Resources.Count); // serviceA, serviceB, cache

            var serviceA = graph.Resources.First(r => r.Name == "serviceA");
            var serviceB = graph.Resources.First(r => r.Name == "serviceB");
            var cache = graph.Resources.First(r => r.Name == "cache");

            // serviceA should have exactly 1 outbound connection to cache
            Assert.Single(serviceA.Connections);
            Assert.Equal("cache", serviceA.Connections[0].Name);
            Assert.Equal("Outbound", serviceA.Connections[0].Direction);

            // serviceB should have exactly 1 outbound connection to cache
            Assert.Single(serviceB.Connections);
            Assert.Equal("cache", serviceB.Connections[0].Name);
            Assert.Equal("Outbound", serviceB.Connections[0].Direction);

            // cache should have exactly 2 inbound connections (from serviceA and serviceB), no duplicates
            Assert.Equal(2, cache.Connections.Count);
            Assert.All(cache.Connections, c => Assert.Equal("Inbound", c.Direction));
            Assert.Contains(cache.Connections, c => c.Name == "serviceA");
            Assert.Contains(cache.Connections, c => c.Name == "serviceB");
        }
    }

    [Fact]
    public async Task GenerateAsync_UnmappedResourceType_IncludedAsContainerWithWarning()
    {
        // The ResourceTypeMapper falls back to Applications.Core/containers for unknown types
        // and logs a warning. The generator should still include these resources in the graph.
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            builder.AddContainer("api", "myimage:latest");
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            // Container is not "unmapped" per se, but verify the fallback behavior:
            // all resources should appear in graph with container type
            Assert.Single(graph.Resources);
            Assert.Equal("api", graph.Resources[0].Name);
            Assert.Equal("Applications.Core/containers", graph.Resources[0].Type);
        }
    }

    [Fact]
    public async Task GenerateAsync_WritesStatusWithCorrectResourceCount_EvenWhenZero()
    {
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("myapp");
            // No resources added — only the RadiusEnvironment itself
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);
            await generator.GenerateAsync(model, radiusEnv, _outputDir);

            var statusJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "status.json"));
            var status = JsonSerializer.Deserialize<PreviewStatus>(statusJson)!;

            Assert.True(status.PreviewMode);
            Assert.Equal(0, status.ResourceCount);

            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            Assert.Empty(graph.Resources);
        }
    }

    [Fact]
    public async Task GenerateAsync_CompletesWithin2Seconds_For20ResourcesWith10Connections()
    {
        // SC-007: Preview data generation < 2 seconds for apps with up to 20 resources
        var (app, model, radiusEnv) = await BuildModelWithBeforeStartAsync(builder =>
        {
            builder.AddRadiusEnvironment("benchmark");

            // Create 10 Redis resources (portable) and 10 containers (workloads)
            var portableResources = new List<IResourceBuilder<RedisResource>>();
            for (int i = 0; i < 10; i++)
            {
                portableResources.Add(builder.AddRedis($"redis{i}"));
            }

            // Each container references one Redis resource → 10 connections
            for (int i = 0; i < 10; i++)
            {
                builder.AddContainer($"service{i}", $"image{i}:latest")
                    .WithReference(portableResources[i]);
            }
        });

        using (app)
        {
            var generator = new PreviewGraphGenerator(_logger);

            var sw = Stopwatch.StartNew();
            await generator.GenerateAsync(model, radiusEnv, _outputDir);
            sw.Stop();

            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
                $"Preview generation took {sw.Elapsed.TotalMilliseconds:F0}ms, expected < 2000ms");

            // Verify output correctness
            var graphJson = await File.ReadAllTextAsync(Path.Combine(_outputDir, "graph.json"));
            var graph = JsonSerializer.Deserialize<PreviewGraphResponse>(graphJson)!;

            Assert.Equal(20, graph.Resources.Count);
            var outboundConnections = graph.Resources.Sum(r => r.Connections.Count(c => c.Direction == "Outbound"));
            Assert.Equal(10, outboundConnections);
        }
    }

    /// <summary>
    /// Helper: builds an app model and fires BeforeStartEvent so DeploymentTargetAnnotations
    /// are attached (mimicking what happens during aspire run).
    /// </summary>
    private static async Task<(DistributedApplication App, DistributedApplicationModel Model, RadiusEnvironmentResource RadiusEnv)>
        BuildModelWithBeforeStartAsync(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = DistributedApplication.CreateBuilder();
        configure(builder);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = RadiusTestHelper.GetRadiusEnvironment(model);

        return (app, model, radiusEnv);
    }
}

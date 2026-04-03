// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

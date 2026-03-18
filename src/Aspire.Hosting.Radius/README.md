# Aspire.Hosting.Radius

Radius compute environment integration for [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/). Register a Radius environment with a single call and leverage Aspire's standard `aspire run`, `aspire publish`, and `aspire deploy` pipeline without modifying resource declarations.

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Radius as the compute environment
builder.AddRadiusEnvironment("radius")
    .WithRadiusNamespace("my-namespace")
    .WithDashboard();

// Add resources as usual — Radius handles deployment
var redis = builder.AddRedis("cache");
builder.AddContainer("api", "myregistry/api:latest")
    .WithReference(redis);

builder.Build().Run();
```

## API Reference

### Extension Methods

#### `AddRadiusEnvironment`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
    this IDistributedApplicationBuilder builder,
    string name = "radius")
```

Registers a Radius compute environment in the Aspire app model. This is the primary entry point for Radius integration.

- **name**: Human-readable name for the Radius environment (default: `"radius"`)
- Registers `RadiusInfrastructure` as an eventing subscriber (idempotent)
- Creates a dashboard resource automatically

#### `WithRadiusNamespace`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> WithRadiusNamespace(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    string @namespace)
```

Sets the Kubernetes namespace where Radius resources will deploy (default: `"default"`).

#### `WithDashboard`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    bool enabled = true)
```

Enables or disables the Radius dashboard container during `aspire run`. The dashboard runs on `localhost:7007` and is **enabled by default**.

#### `PublishAsRadiusResource<T>`

```csharp
public static IResourceBuilder<T> PublishAsRadiusResource<T>(
    this IResourceBuilder<T> builder,
    Action<RadiusResourceCustomization> configure)
    where T : IResource
```

Applies Radius-specific publishing customizations to a resource:

```csharp
// Custom recipe
builder.AddRedis("cache")
    .PublishAsRadiusResource(cfg => cfg.Recipe = "my-redis-recipe");

// Manual provisioning (e.g., PostgreSQL)
builder.AddPostgres("db")
    .PublishAsRadiusResource(cfg =>
    {
        cfg.Provisioning = RadiusResourceProvisioning.Manual;
        cfg.Host = "postgres.default.svc.cluster.local";
        cfg.Port = 5432;
    });
```

### Resource Type Mapping

| Aspire Resource | Radius Type |
|---|---|
| `RedisResource` | `Applications.Datastores/redisCaches` |
| `SqlServerServerResource` | `Applications.Datastores/sqlDatabases` |
| `MongoDBServerResource` | `Applications.Datastores/mongoDatabases` |
| `RabbitMQServerResource` | `Applications.Messaging/rabbitMQQueues` |
| `PostgresServerResource` | `Applications.Datastores/sqlDatabases` |
| `DaprStateStoreResource` | `Applications.Dapr/stateStores` |
| `DaprPubSubResource` | `Applications.Dapr/pubSubBrokers` |
| `ContainerResource` | `Applications.Core/containers` |
| `ProjectResource` | `Applications.Core/containers` |
| Unmapped types | `Applications.Core/containers` (with warning) |

### Pipeline Phases

| Command | Phase | Description |
|---|---|---|
| `aspire run` | Inner-loop | Attaches deployment annotations, starts dashboard on `localhost:7007` |
| `aspire publish` | Publishing | Generates `app.bicep` with Radius portable resources |
| `aspire deploy` | Deployment | Runs `rad deploy` against the generated Bicep template |

## Documentation

- [Specification](../../specs/001-add-radius-environment/spec.md)
- [Implementation plan](../../specs/001-add-radius-environment/plan.md)
- [Quick start](../../specs/001-add-radius-environment/quickstart.md)
- [Deployment guide](../../specs/001-add-radius-environment/deployment-guide.md)
- [Bicep schema reference](../../specs/001-add-radius-environment/bicep-schema-reference.md)
- [Publishing guide](../../specs/001-add-radius-environment/publishing-guide.md)
- [Troubleshooting](../../docs/troubleshooting.md)

## Requirements

- .NET 8+
- `Aspire.Hosting` package
- `rad` CLI ([install](https://docs.radapp.io/installation/)) — required for Phase 5 deployment only
- Kubernetes cluster — required for Phase 5 deployment only

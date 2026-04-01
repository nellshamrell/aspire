# Aspire.Hosting.Radius

Radius integration for .NET Aspire. Register a Radius compute environment with a single call and use Aspire's standard pipeline — `aspire run`, `aspire publish`, and `aspire deploy` — without modifying resource declarations.

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

### Extension Methods on `IDistributedApplicationBuilder`

#### `AddRadiusEnvironment(string name = "radius")`

Registers a Radius compute environment and returns an `IResourceBuilder<RadiusEnvironmentResource>`.

- Registers `RadiusInfrastructure` as an `IDistributedApplicationEventingSubscriber` (idempotent)
- Creates a `RadiusEnvironmentResource` in the app model
- Registers publish and deploy pipeline steps via `PipelineStepAnnotation`

```csharp
var radius = builder.AddRadiusEnvironment("radius");
```

### Extension Methods on `IResourceBuilder<RadiusEnvironmentResource>`

#### `WithRadiusNamespace(string @namespace)`

Sets the Kubernetes namespace for resource deployment. Defaults to `"default"`.

```csharp
builder.AddRadiusEnvironment("radius")
    .WithRadiusNamespace("production");
```

#### `WithDashboard(bool enabled = true)`

Enables or disables the Radius dashboard container during `aspire run`. When enabled, the dashboard runs on `localhost:7007`.

```csharp
builder.AddRadiusEnvironment("radius")
    .WithDashboard(false);  // Disable dashboard
```

#### `ConfigureRadiusInfrastructure(Action<RadiusInfrastructureOptions> configure)`

Configures the Radius infrastructure before Bicep compilation. Follows Aspire's `ConfigureInfrastructure` pattern.

```csharp
builder.AddRadiusEnvironment("radius")
    .ConfigureRadiusInfrastructure(options =>
    {
        // Customize the infrastructure builder before Bicep compilation
    });
```

### Extension Methods on `IResourceBuilder<T>` where `T : IResource`

#### `PublishAsRadiusResource<T>(Action<RadiusResourceCustomization> configure)`

Configures per-resource Radius customization (custom recipes, manual provisioning).

```csharp
builder.AddRedis("cache")
    .PublishAsRadiusResource(cfg =>
    {
        cfg.Recipe = new RadiusRecipe
        {
            Name = "azure-redis-premium",
            TemplatePath = "ghcr.io/myorg/recipes/redis:1.0"
        };
    });
```

**Manual provisioning** (for resources without a native Radius portable type):

```csharp
builder.AddPostgres("postgres")
    .PublishAsRadiusResource(cfg =>
    {
        cfg.Provisioning = RadiusResourceProvisioning.Manual;
        cfg.Host = "postgres.default.svc.cluster.local";
        cfg.Port = 5432;
    });
```

## Core Types

| Type | Description |
|------|-------------|
| `RadiusEnvironmentResource` | Represents a Radius compute environment (`IComputeEnvironmentResource`) |
| `RadiusDashboardResource` | Container resource for the Radius dashboard UI |
| `RadiusResourceCustomization` | Per-resource configuration (recipe, provisioning mode, host, port) |
| `RadiusRecipe` | Recipe definition with name, template path, and parameters |
| `RadiusResourceProvisioning` | Enum: `Automatic`, `Manual` |

## Pipeline Behavior

| Command | Action |
|---------|--------|
| `aspire run` | Attaches `DeploymentTargetAnnotation` to compute resources; starts dashboard on `localhost:7007` |
| `aspire publish` | Generates `app.bicep` and `bicepconfig.json` in the output directory |
| `aspire deploy` | Runs `rad deploy app.bicep --output json` with progress streaming |

## Resource Type Mapping

Aspire resource types are automatically mapped to Radius portable resources:

| Aspire Type | Radius Type |
|-------------|-------------|
| `RedisResource` | `Applications.Datastores/redisCaches` |
| `SqlServerServerResource` | `Applications.Datastores/sqlDatabases` |
| `MongoDBServerResource` | `Applications.Datastores/mongoDatabases` |
| `RabbitMQServerResource` | `Applications.Messaging/rabbitMQQueues` |
| `DaprStateStoreResource` | `Applications.Dapr/stateStores` |
| `DaprPubSubResource` | `Applications.Dapr/pubSubBrokers` |
| `PostgresServerResource` | Manual provisioning (no native Radius type) |
| `ContainerResource` | `Applications.Core/containers` |
| `ProjectResource` | `Applications.Core/containers` |
| Unmapped types | `Applications.Core/containers` (fallback with warning) |

## Further Reading

- [Feature Specification](../../../specs/001-add-radius-environment/spec.md)
- [Implementation Plan](../../../specs/001-add-radius-environment/plan.md)
- [Quick Start Guide](../../../specs/001-add-radius-environment/quickstart.md)
- [Deployment Guide](../../../specs/001-add-radius-environment/deployment-guide.md)
- [Bicep Schema Reference](../../../specs/001-add-radius-environment/bicep-schema-reference.md)
- [Troubleshooting](../../../docs/troubleshooting.md)

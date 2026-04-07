# Aspire.Hosting.Radius

Radius support for .NET Aspire, enabling Radius as a first-class compute environment.

## Overview

`Aspire.Hosting.Radius` integrates [Radius](https://radapp.io/) into the .NET Aspire hosting model. A single `AddRadiusEnvironment()` call enables Aspire's standard pipeline — `aspire run`, `aspire publish`, and `aspire deploy` — to target a Radius environment on Kubernetes, with no changes to your resource declarations.

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

builder.AddRadiusEnvironment("radius");

var redis = builder.AddRedis("cache");
builder.AddProject<Projects.Api>("api")
    .WithReference(redis);

builder.Build().Run();
```

## API Reference

### `AddRadiusEnvironment`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
    this IDistributedApplicationBuilder builder,
    string name = "radius");
```

Registers a Radius compute environment. In run mode, resources are annotated for visualization. In publish/deploy mode, Bicep templates are generated and deployed via `rad deploy`.

### `WithRadiusNamespace`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> WithRadiusNamespace(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    string @namespace);
```

Sets the Kubernetes namespace where Radius resources will be deployed. Default is `"default"`.

### `PublishAsRadiusResource<T>`

```csharp
public static IResourceBuilder<T> PublishAsRadiusResource<T>(
    this IResourceBuilder<T> builder,
    Action<RadiusResourceCustomization> configure)
    where T : IResource;
```

Applies per-resource Radius customizations: custom recipes, manual provisioning mode, or connection string overrides.

### `ConfigureRadiusInfrastructure`

```csharp
public static IResourceBuilder<RadiusEnvironmentResource> ConfigureRadiusInfrastructure(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    Action<RadiusInfrastructureOptions> configure);
```

Provides access to the Azure Provisioning AST before Bicep compilation, enabling advanced customization of generated resources.

## Resource Type Mapping

| Aspire Resource | Radius Type |
|---|---|
| `RedisResource` | `Applications.Datastores/redisCaches` |
| `SqlServerServerResource` | `Applications.Datastores/sqlDatabases` |
| `MongoDBServerResource` | `Applications.Datastores/mongoDatabases` |
| `PostgresServerResource` | `Applications.Datastores/sqlDatabases` (manual) |
| `RabbitMQServerResource` | `Applications.Messaging/rabbitMQQueues` |
| `ContainerResource` / `ProjectResource` | `Applications.Core/containers` |

Unmapped resource types fall back to `Applications.Core/containers` with a warning.

## Pipeline Steps

| Step | Pipeline | Description |
|---|---|---|
| `publish-{name}` | Publish | Generates `app.bicep` via Azure Provisioning SDK |
| `validate-rad-cli-{name}` | Deploy | Verifies `rad` CLI is on PATH |
| `deploy-radius-{name}` | Deploy | Executes `rad deploy app.bicep --output json` |

## Related Documentation

- [Specification](../../specs/001-add-radius-environment/spec.md)
- [Deployment Guide](../../specs/001-add-radius-environment/deployment-guide.md)
- [Quick Start](../../specs/001-add-radius-environment/quickstart.md)
- [Troubleshooting](../../docs/troubleshooting.md)
- [Samples](../../samples/)

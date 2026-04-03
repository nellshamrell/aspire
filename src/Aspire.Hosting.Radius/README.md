# Aspire.Hosting.Radius

Radius compute environment support for .NET Aspire, enabling portable resource deployment via the `rad` CLI.

## Overview

`Aspire.Hosting.Radius` integrates [Radius](https://radapp.io/) as a first-class compute environment in .NET Aspire. Register a Radius environment with a single call and use Aspire's standard `aspire run`, `aspire publish`, and `aspire deploy` workflows — no changes to your existing resource declarations.

**Supported resource types**: Redis, SQL Server, MongoDB, RabbitMQ, PostgreSQL (manual), Dapr state stores, Dapr pub/sub, containers, and projects. Unmapped types fall back to `Applications.Core/containers`.

## Quick Start

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Radius as the compute environment
builder.AddRadiusEnvironment("radius")
    .WithRadiusNamespace("my-app");

// Declare resources as usual — no Radius-specific changes needed
var redis = builder.AddRedis("cache");
var sql = builder.AddSqlServer("sql").AddDatabase("appdb");

builder.AddContainer("api", "myregistry/api:latest")
    .WithReference(redis)
    .WithReference(sql);

builder.Build().Run();
```

Then use the standard Aspire CLI:

```bash
aspire run       # Inner-loop with Radius dashboard on localhost:7007
aspire publish   # Generate app.bicep for Radius
aspire deploy    # Deploy to Kubernetes via rad CLI
```

## API Reference

### Extension Methods

#### `AddRadiusEnvironment(builder, name)`

Adds a Radius compute environment to the application model.

```csharp
IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
    this IDistributedApplicationBuilder builder,
    string name = "radius")
```

- Registers a `RadiusInfrastructure` event subscriber for `BeforeStartEvent`
- Attaches publish and deploy pipeline steps via `PipelineStepAnnotation`

#### `WithRadiusNamespace(builder, namespace)`

Sets the Kubernetes namespace for the Radius environment.

```csharp
IResourceBuilder<RadiusEnvironmentResource> WithRadiusNamespace(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    string @namespace)
```

#### `WithDashboard(builder, enabled)`

Enables or disables the Radius dashboard container during `aspire run`.

```csharp
IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    bool enabled = true)
```

Default: `true`. Dashboard runs on `localhost:7007`.

#### `PublishAsRadiusResource<T>(builder, configure)`

Configures a resource with custom Radius provisioning options.

```csharp
IResourceBuilder<T> PublishAsRadiusResource<T>(
    this IResourceBuilder<T> builder,
    Action<RadiusResourceCustomization> configure) where T : IResource
```

Configuration options:
- `Recipe` — custom recipe name and template path
- `Provisioning` — `Automatic` (default) or `Manual`
- `Host` / `Port` — required when provisioning is `Manual`

#### `ConfigureRadiusInfrastructure(builder, configure)`

Customizes the Bicep generation AST before compilation.

```csharp
IResourceBuilder<RadiusEnvironmentResource> ConfigureRadiusInfrastructure(
    this IResourceBuilder<RadiusEnvironmentResource> builder,
    Action<RadiusInfrastructureOptions> configure)
```

Options expose mutable collections of environment, application, portable resource, and container constructs.

## Phases

| Phase | Command | Requires |
|---|---|---|
| Inner-loop | `aspire run` | Docker only |
| Publishing | `aspire publish` | Nothing extra |
| Deployment | `aspire deploy` | Kubernetes + `rad` CLI |

## Resources

- [Feature Specification](../../specs/001-add-radius-environment/spec.md)
- [Implementation Plan](../../specs/001-add-radius-environment/plan.md)
- [Quick Start Guide](../../specs/001-add-radius-environment/quickstart.md)
- [Bicep Schema Reference](../../specs/001-add-radius-environment/bicep-schema-reference.md)
- [Publishing Guide](../../specs/001-add-radius-environment/publishing-guide.md)
- [Deployment Guide](../../specs/001-add-radius-environment/deployment-guide.md)
- [Troubleshooting](../../docs/troubleshooting.md)
- [Radius Documentation](https://docs.radapp.io/)

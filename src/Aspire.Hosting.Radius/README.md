# Aspire.Hosting.Radius library

Provides extensions and resource definitions for an Aspire AppHost to publish and deploy applications to a [Radius](https://radapp.io) compute environment.

> **Preview / prototype.** This integration is an early prototype. The public API surface and the generated Bicep contract may change in future versions. Pin the integration version in `AppHost.csproj` and avoid taking dependencies on any internal types.

## Getting started

### Prerequisites

* A Kubernetes cluster (for example `kind`, `minikube`, AKS) with [Radius](https://docs.radapp.io/installation/) installed.
* The `rad` CLI on PATH. Version must match the pinned Radius Bicep extension this integration emits (currently `0.59`). Run `rad version` to check.
* `rad init` has been run against the target cluster so the workspace and environment exist.

### Install the package

In your AppHost project, install the Aspire Radius Hosting library with [NuGet](https://www.nuget.org):

```dotnetcli
dotnet add package Aspire.Hosting.Radius
```

## Usage example

Then, in the _AppHost.cs_ file of `AppHost`, add the environment:

```csharp
builder.AddRadiusEnvironment("radius");
```

`aspire publish` generates `app.bicep` plus a `bicepconfig.json` pinned to the Radius extension version, and `aspire deploy` invokes `rad deploy` against the file:

```shell
aspire publish -o radius-artifacts
aspire deploy
```

## Supported resources

* `AddContainer(...)` — published as a Radius container workload (`Radius.Compute/containers`).
* `AddProject<T>(...)` — published as a Radius container workload only when the project has a pre-built image attached with `WithContainerImage("<registry>/<image>:<tag>")`. Without one, `aspire publish` fails with a remediation message to build and push an image the cluster can pull.
* Selected resources with a Radius mapping (e.g. Redis, MongoDB, RabbitMQ, Dapr building blocks) emit Radius "legacy" types via the resource type mapper. Child database resources (for example `AddSqlServer("sql").AddDatabase("appdb")`) are collapsed onto the parent today.

Other Aspire resource types are not emitted; only the resources listed above appear in the generated Bicep.

## Multiple compute environments

When the model contains more than one compute environment (for example a Radius environment alongside a Kubernetes one), explicitly assign each resource to the environment that should publish it:

```csharp
var radius = builder.AddRadiusEnvironment("radius");
var k8s    = builder.AddKubernetesEnvironment("k8s");

builder.AddContainer("api", "myorg/api", "1.0")
       .WithComputeEnvironment(radius);
```

Untargeted resources surface a clear error from the core pipeline instead of being silently claimed by one environment.

## Cloud providers

Configure Azure and/or AWS cloud providers directly in the AppHost. The publisher
emits `properties.providers.azure.scope` / `providers.aws.scope` on the
`Radius.Core/environments` resource and the deploy pipeline registers
credentials via `rad credential register` before `rad deploy` runs.

```csharp
var clientSecret = builder.AddParameter("azure-sp-secret", secret: true);

builder.AddRadiusEnvironment("radius")
       .WithAzureProvider(
           subscriptionId: "00000000-0000-0000-0000-000000000000",
           resourceGroup:  "rg-radius",
           azure => azure.WithServicePrincipal(
               tenantId:     "11111111-1111-1111-1111-111111111111",
               clientId:     "22222222-2222-2222-2222-222222222222",
               clientSecret: clientSecret))
       .WithAwsProvider(
           accountId: "123456789012",
           region:    "us-west-2",
           aws => aws.WithIrsa("arn:aws:iam::123456789012:role/radius-irsa"));
```

Supported credential modes:

| Provider | Mode | Method |
|----------|------|--------|
| Azure | Service Principal | `azure.WithServicePrincipal(tenantId, clientId, clientSecret)` |
| Azure | Workload Identity | `azure.WithWorkloadIdentity(tenantId, clientId)` |
| AWS   | Access Key        | `aws.WithAccessKey(accessKeyId, secretAccessKey)` |
| AWS   | IRSA              | `aws.WithIrsa(iamRoleArn)` |

Cloud-provider credential secret material (Azure SP client secret, AWS access-key pair) must be supplied
via `builder.AddParameter(..., secret: true)`. The integration never inlines
those credential values into Bicep or manifests; `rad credential register` resolves them during
deploy and redacts them from any logged command line. Recipe-parameter secrets bound to
Aspire parameters are emitted as valueless Bicep parameters today, not delivered by this
credential-registration path.

See [specs/003-cloud-providers/quickstart.md](../../../specs/003-cloud-providers/quickstart.md) for an end-to-end walkthrough.

## Multiple resource groups

> **Experimental** — `WithRadiusResourceGroup` is gated by `ASPIRERADIUS005`. Suppress the
> diagnostic (`#pragma warning disable ASPIRERADIUS005`) to opt in.

Route each resource into a named [Radius resource group](https://docs.radapp.io/guides/resources/) to
partition a single app graph into independently published and deployed units. When any resource is
routed, `aspire publish` emits one `groups/<group>/app.bicep` per group (each carrying only that
group's resources plus the single logical application), and `aspire deploy` creates every group
idempotently and deploys them in dependency order with one `rad deploy` per group.

```csharp
#pragma warning disable ASPIRERADIUS005

// Shared infrastructure in its own group, with its own environment and configuration.
builder.AddRadiusEnvironment("data-env")
    .WithRadiusResourceGroup("shared-data")
    .WithNamespace("shared-data-ns");
var db = builder.AddPostgres("db").WithRadiusResourceGroup("shared-data");

// A per-service group; a cross-group WithReference emits the target's full UCP ID.
builder.AddRadiusEnvironment("orders-env").WithRadiusResourceGroup("orders");
builder.AddProject<Projects.Orders>("orders")
    .WithRadiusResourceGroup("orders")
    .WithReference(db);

#pragma warning restore ASPIRERADIUS005
```

Key behaviours:

- **Exactly one group per resource** — every routable resource and environment must resolve to a
  single group (`ASPIRERADIUS031`/`ASPIRERADIUS032`).
- **Cross-group references and environment targets** are emitted as fully-qualified UCP IDs; the
  union of these edges must be acyclic (`ASPIRERADIUS035`).
- **Cross-group environment targets** — a group without its own environment can deploy against
  another group's environment via `WithRadiusResourceGroup(group, environmentGroup)`
  (`ASPIRERADIUS034` if the target group owns no environment).
- **Per-group configuration** — providers, namespace, recipe packs, and recipe parameters compose
  per group into that group's environment.
- **No-group default is unchanged** — when no resource is routed to a group, publish/deploy behave
  byte-for-byte as before.

See [specs/007-multi-resource-groups/quickstart.md](../../../specs/007-multi-resource-groups/quickstart.md)
for an end-to-end walkthrough.

## Diagnostics

The package uses the `ASPIRERADIUS` diagnostic prefix for two distinct mechanisms, with
disjoint numeric ranges reserved so the IDs never collide:

| Range | Mechanism | Surfaced as |
|-------|-----------|-------------|
| `ASPIRERADIUS001`–`ASPIRERADIUS009` | Compile-time analyzer diagnostics for experimental APIs (incl. `ASPIRERADIUS005` for `WithRadiusResourceGroup`) | `[Experimental]` warnings (suppressible), documented at `https://aka.ms/aspire/diagnostics/<id>` |
| `ASPIRERADIUS010`–`ASPIRERADIUS019` | Cloud-provider configuration errors | Thrown `InvalidOperationException` (message includes the ID) |
| `ASPIRERADIUS020`–`ASPIRERADIUS029` | Cloud-managed resource (`WithManagedResource`) and recipe/recipe-parameter validation | Thrown `ArgumentException` (config time) / `InvalidOperationException` (publish time) |
| `ASPIRERADIUS030`–`ASPIRERADIUS039` | Multi-resource-group routing (`WithRadiusResourceGroup`) validation | Thrown `ArgumentException` (call site, e.g. empty name) / `InvalidOperationException` (fail-fast gate before publish/deploy) |

Runtime validation codes:

| Code | When | Meaning |
|------|------|---------|
| `ASPIRERADIUS010` | Provider config | A cloud-provider credential callback did not select a credential. |
| `ASPIRERADIUS011` | Provider config | Conflicting cloud-provider credentials across environments sharing a Radius installation. |
| `ASPIRERADIUS020` | Publish | A resource is marked cloud-managed for a cloud whose provider is not configured on the environment. Validated at publish time, so provider/selection call order does not matter. |
| `ASPIRERADIUS022` | Config | A compute workload (project/container, or a resource overridden to the compute container type) cannot be marked cloud-managed. |
| `ASPIRERADIUS023` | Config | A cloud-managed recipe is missing its `RecipeLocation`. |
| `ASPIRERADIUS024` | Config | A child resource cannot be marked cloud-managed directly; mark its parent instead. |
| `ASPIRERADIUS025` | Config | The resource does not map to a supported Radius backing resource type. |
| `ASPIRERADIUS026` | Publish | Multiple instances of one user-defined (`Radius.*`) type resolve to different recipes or recipe parameters; Radius binds one recipe (with one parameter set) per type per environment. |
| `ASPIRERADIUS027` | Publish | A per-instance recipe name or parameters were set (via `PublishAsRadiusResource(c => c.Recipe...)`) on a native (`Radius.*`) type or native container; these are unsupported. Use `WithRecipeParameters(resourceType, ...)` on the environment instead. |
| `ASPIRERADIUS028` | Publish | Two recipe parameters bound to different Aspire parameters sanitize to the same Bicep identifier. Rename one so they produce distinct identifiers. |
| `ASPIRERADIUS031` | Publish/Deploy gate | A compute/backing resource or environment is not assigned to any Radius resource group (orphaned). Route it with `WithRadiusResourceGroup(...)`. |
| `ASPIRERADIUS032` | Publish/Deploy gate | A resource is ambiguously assigned to more than one Radius resource group; it must resolve to exactly one. Keep a single `WithRadiusResourceGroup(...)` call per resource (the last call wins if repeated). |
| `ASPIRERADIUS033` | Config (call site) | A Radius resource-group name passed to `WithRadiusResourceGroup` is empty or whitespace. |
| `ASPIRERADIUS034` | Publish/Deploy gate | A cross-group environment target names a group that has no Radius environment routed to it. Route a `RadiusEnvironmentResource` into that group, or correct the `environmentGroup` argument. |
| `ASPIRERADIUS035` | Publish/Deploy gate | The group dependency graph (cross-group references ∪ cross-group environment targets) contains a cycle. Break the cycle by removing a cross-group `WithReference` or environment target so the groups form a DAG. |

> `ASPIRERADIUS021` was retired: the cloud is taken from the explicit `RadiusCloud` argument
> rather than inferred from the recipe location, so there is no cloud/recipe conflict to flag.

For native `Radius.*` types, recipe parameters are configured at the environment/type scope; per-instance recipe parameters are rejected with `ASPIRERADIUS027`, and environment/type-scoped values override the recipe's intrinsic parameters. The per-resource-over-type-over-environment precedence applies only to legacy `Applications.*` types.

## Known limitations

* For `ASPIRERADIUS011`, AWS access-key credential conflicts are compared by the Aspire parameter name that supplies the access-key ID, not by the resolved access-key value. Two environments that use different parameter names for the same key can be flagged as a false conflict, while the same parameter name with different values is not flagged.

## Additional documentation

* https://docs.radapp.io/
* https://aspire.dev/

## Feedback & contributing

https://github.com/microsoft/aspire

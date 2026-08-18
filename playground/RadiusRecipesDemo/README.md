# RadiusRecipesDemo

This is the third demo in the Radius series. `RadiusConnectionsDemo` wired two containers
together. This one adds a **backing resource** — a Redis cache — which is where Radius stops
looking like a deployment format and starts looking like a platform.

Recipes are the Radius concept that has no direct Aspire equivalent, so this demo is the one
to slow down on. `builder.AddRedis("cache")` never names an image, a Helm chart, or a cloud
SKU. The AppHost declares that the application needs a cache; a **recipe** owned by the
platform team decides what actually gets provisioned.

## Where this fits

| Demo | Adds |
|------|------|
| `RadiusDemo` | One container, publish, deploy, verify |
| `RadiusConnectionsDemo` | A second container and service discovery between them |
| **`RadiusRecipesDemo`** | **A backing resource, recipes, and real Radius `connections`** |
| `RadiusProjectDemo` | A .NET project instead of a public image |

## What you will learn

1. An Aspire backing resource becomes a Radius resource provisioned by a recipe.
2. Referencing one emits a real Radius `connections` entry, unlike a container reference.
3. Radius injects `CONNECTION_*` variables from that connection at deploy time.
4. Why the generated Bicep contains two parallel environment/application chains.
5. Where recipe parameters fit, and why an undeclared one fails the deployment.
6. Secret values are emitted as `@secure()` Bicep parameters, never as literals.
7. A current limitation: Aspire's own connection values do not address the recipe's resource.

## Prerequisites

Same as `RadiusDemo`: a restored repository, Docker, and `kind`, `kubectl`, and Radius CLI
0.59.x on `PATH`. All commands below run from `playground/RadiusRecipesDemo`.

## 1. Run locally

```bash
dotnet run --project RadiusRecipesDemo.AppHost
```

`cache` runs as an ordinary local Redis container and `web` gets a normal connection string.
The recipe machinery is entirely absent from the inner loop — Radius is a publish/deploy
concern only.

Stop the AppHost before continuing.

## 2. Publish

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh publish --non-interactive
cat RadiusRecipesDemo.AppHost/aspire-output/app.bicep
```

### The recipe that provisions the cache

Redis is currently emitted as a Radius *legacy* type, so its recipe is declared inline on a
legacy environment:

```bicep
resource radiusenv_legacy 'Applications.Core/environments@2023-10-01-preview' = {
  name: 'radius'
  properties: {
    compute: {
      kind: 'kubernetes'
      namespace: 'radius-recipes'
    }
    recipes: {
      'Applications.Datastores/redisCaches': {
        default: {
          templateKind: 'bicep'
          templatePath: 'ghcr.io/radius-project/recipes/local-dev/rediscaches:latest'
        }
      }
    }
  }
}
```

The AppHost never mentioned `local-dev/rediscaches`. That template path is the environment's
default recipe for the Redis type. Point the environment at a different recipe — an Azure
Cache for Redis recipe, for instance — and the same AppHost deploys managed infrastructure
instead, with no code change. That substitution is the whole value proposition.

The resource itself is almost empty, because the recipe supplies everything:

```bicep
resource cache 'Applications.Datastores/redisCaches@2023-10-01-preview' = {
  name: 'cache'
  properties: {
    application: app_legacy.id
    environment: radiusenv_legacy.id
  }
}
```

### Why there are two environments and two applications

The generated file contains both `Radius.Core/environments` and
`Applications.Core/environments` (plus a matching pair of applications). Radius is migrating
from built-in portable types (`Applications.*`) to user-defined types (`Radius.*`). Container
workloads already use the new `Radius.Compute/containers` type, while Redis still resolves to
the legacy type, so the publisher emits both parent chains and attaches each resource to the
correct one. The pairs describe the same environment and application; they are not two
deployments. As Radius promotes each type, the legacy chain disappears on its own.

For newer types this shape is different: they are grouped into a
`Radius.Core/recipePacks` resource that the `Radius.Core/environments` references. Both forms
are visible in this one file.

### Recipe parameters

A recipe usually needs input — a region, a SKU, a capacity. `WithRecipeParameters` supplies
those values, either environment-wide or scoped to a single Radius resource type, with the
scoped value winning on collision:

```csharp
builder.AddRadiusEnvironment("radius")
       .WithNamespace("radius-recipes")
       .WithRecipeParameters(p => p["region"] = "eastus")
       .WithRecipeParameters("Applications.Datastores/redisCaches", p => p["capacity"] = 2);
```

These lines are shown commented out in `AppHost.cs`, because **the stock `local-dev`
recipes declare no parameters at all**. Confirm that yourself after deploying:

```bash
rad recipe show default --resource-type Applications.Datastores/redisCaches -e radius
```

```text
PARAMETER  TYPE      DEFAULT VALUE  VALUE     MIN       MAX
No parameters available
```

Supplying a parameter that the recipe does not declare fails the deployment:

```text
"code": "RecipeDeploymentFailed",
"message": "failed to deploy recipe default of type Applications.Datastores/redisCaches",
"code": "InvalidTemplate",
"message": "Deployment template validation failed: 'The following parameters were supplied,
            but do not exist in the template...'"
```

That failure is the lesson. Recipe parameters are a contract with a specific recipe, not free-form
metadata, so the names come from whoever authored the recipe your environment points at. Uncomment
the lines and run `aspire publish` to see how they are emitted — the environment-wide value lands on
every recipe and the scoped value lands only on the Redis recipe, both keeping their Bicep types
(`2` stays a number, not `'2'`) — but leave them out when deploying against `local-dev`.

Note that the scope key is the **emitted** Radius type. Because Redis currently maps to the
legacy type, the scope is `Applications.Datastores/redisCaches`, not `Radius.Data/redisCaches`.
Scoping to a type with no emitted recipe is ignored with a warning rather than failing the
publish, so a silently ineffective parameter usually means the wrong scope key.

### A real Radius connection

This is the difference from the previous demo:

```bicep
resource web 'Radius.Compute/containers@2025-08-01-preview' = {
  name: 'web'
  properties: {
    ...
    connections: {
      cache: {
        source: cache.id
      }
    }
  }
}
```

Referencing a backing resource produces a declared Radius `connections` entry pointing at the
resource's `.id`. Radius uses it to model the dependency, which is what makes the application
graph in step 4 useful.

### Secrets are parameters, not literals

The top of the file declares:

```bicep
@secure()
param cache_password string
```

and the container's `CACHE_PASSWORD` references that parameter rather than embedding a value.
The resolved value is supplied to `rad deploy` separately, so the published artifact is safe
to commit or inspect.

## 3. Deploy

Follow `RadiusDemo` sections 3 and 4 to create the dedicated KinD cluster and Radius
workspace, then create this demo's namespace:

```bash
kubectl --context kind-aspire-radius-demo create namespace radius-recipes
```

Deploy:

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh deploy --non-interactive
```

Deployment now does more than it did in earlier demos: Radius runs the Redis recipe, which
provisions the cache, before the container that depends on it.

## 4. Verify

```bash
rad app graph -a app --preview
```

```text
Name: cache (Applications.Datastores/redisCaches)
Connections:
  web (Radius.Compute/containers) -> cache
Resources:
  redis-cnibclsbkpmss (apps/Deployment)
  redis-cnibclsbkpmss (core/Service)

Name: web (Radius.Compute/containers)
Connections:
  web -> cache (Applications.Datastores/redisCaches)
```

Unlike the previous demo, there is a real edge: `web -> cache`. That edge exists because the
connection was declared in the Bicep rather than inlined into an environment variable.

Note what the recipe created: a `Deployment` and `Service` named `redis-<random-suffix>`, in a
namespace of the recipe's choosing. Nothing in the AppHost chose that name, that namespace, or
that workload shape.

```bash
rad resource list Applications.Datastores/redisCaches
kubectl --context kind-aspire-radius-demo get svc -A | grep redis
```

### What the connection injects

Radius turns a declared connection into environment variables on the consuming container.
Inspect them:

```bash
WEB=$(kubectl --context kind-aspire-radius-demo get pod \
  -n radius-recipes \
  -l radapp.io/resource=web \
  -o jsonpath='{.items[0].metadata.name}')

kubectl --context kind-aspire-radius-demo exec -n radius-recipes $WEB \
  -- env | grep -E 'CONNECTION_CACHE_|CACHE_|ConnectionStrings__'
```

Two different families of variables land in the container, and the difference matters:

```text
# Injected by Radius, from the declared connection
CONNECTION_CACHE_HOST=redis-cnibclsbkpmss.radius-recipes-app.svc.cluster.local
CONNECTION_CACHE_PORT=6379
CONNECTION_CACHE_RESOURCEPROVISIONING=recipe
CONNECTION_CACHE_ID=/planes/radius/local/resourcegroups/default/providers/...

# Emitted by Aspire at publish time
CACHE_HOST=cache-cache.radius-recipes.svc.cluster.local
CACHE_PORT=6379
ConnectionStrings__cache=cache-cache.radius-recipes.svc.cluster.local:6379,******
```

The `CONNECTION_CACHE_*` values are resolved by Radius after the recipe ran, so they address
the cache that actually exists. This is the practical payoff of a declared connection, and it
is why `connections` matters more than it first appears.

### Known limitation: the Aspire-emitted values do not address the recipe's resource

The `CACHE_*` and `ConnectionStrings__cache` values above are computed at publish time, before
any recipe has run, using the naming convention the publisher applies to *containers*
(`{name}-{name}`). A recipe-provisioned resource does not follow that convention, so the host
Aspire emitted does not exist:

```bash
kubectl --context kind-aspire-radius-demo get svc cache-cache -n radius-recipes
# Error from server (NotFound): services "cache-cache" not found
```

The real cache is `redis-cnibclsbkpmss` in the `radius-recipes-app` namespace. The
`ConnectionStrings__cache` value also carries a literal `******` in place of the password —
that placeholder comes from the Redis integration's own connection-string builder in
`Aspire.Hosting.Redis` and is not Radius-specific.

The consequence: a client configured the usual Aspire way (`AddRedisClient("cache")` reading
`ConnectionStrings__cache`) will not reach the cache once deployed to Radius, even though the
same code works locally. Until the publisher can defer these values to the recipe's output,
read the `CONNECTION_CACHE_*` variables that Radius injects instead. This demo uses a plain
web image rather than a Redis client precisely so the walkthrough does not depend on the
broken path.

This is a property of the publisher rather than of any particular cluster: the same mismatch,
with the same `redis-<suffix>` naming and the same `radius-recipes-app` namespace, reproduces
on both KinD and AKS.

## 5. Try swapping the recipe

The most useful experiment in this demo is to point the environment at a different Redis
recipe and redeploy. The application model, the container, and the connection all stay
identical; only the infrastructure the recipe provisions changes. That separation between
"what the app needs" and "how the platform provides it" is the reason to adopt Radius.

## 6. Clean up

All demos in this series publish a Radius application named `app` into the same resource
group, so resources from an earlier demo linger and show up in `rad app graph`. Remove this
demo's resources before moving on:

```bash
rad resource delete Radius.Compute/containers web --yes
rad resource delete Applications.Datastores/redisCaches cache --yes
rad resource delete Radius.Core/applications app --yes
kubectl --context kind-aspire-radius-demo delete namespace radius-recipes
```

Then follow `RadiusDemo` section 7 to remove the workspace and cluster and restore your
previous Radius workspace and Kubernetes context.

## Next steps

Continue to `playground/RadiusProjectDemo`, which replaces the public container image with a
real .NET project.

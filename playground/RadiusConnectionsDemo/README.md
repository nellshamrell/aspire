# RadiusConnectionsDemo

This is the second demo in the Radius series. `RadiusDemo` published a single container.
This one adds a second container and a reference between them, which is where the
interesting part of the Aspire-to-Radius mapping starts.

The lesson is that **a reference between two containers is not a Radius `connection`**.
Aspire resolves service discovery itself and bakes the resulting URL into the consumer's
environment. Radius `connections` are reserved for backing resources such as caches and
databases, which the next demo (`RadiusRecipesDemo`) covers.

## Where this fits

| Demo | Adds |
|------|------|
| `RadiusDemo` | One container, publish, deploy, verify |
| **`RadiusConnectionsDemo`** | **A second container and service discovery between them** |
| `RadiusRecipesDemo` | A backing resource, recipes, and real Radius `connections` |
| `RadiusProjectDemo` | A .NET project instead of a public image |

## What you will learn

1. `WithReference` on a container endpoint emits `services__*` environment variables.
2. The URL Aspire emits addresses the Kubernetes `Service` that the Radius container recipe creates.
3. Radius `connections` do not appear for container-to-container references.
4. Which parts of an Aspire container resource the Radius publisher does **not** carry over.

## Prerequisites

Same as `RadiusDemo`: a restored repository, Docker, and `kind`, `kubectl`, and Radius CLI
0.59.x on `PATH`. All commands below run from `playground/RadiusConnectionsDemo`.

## 1. Run locally

```bash
dotnet run --project RadiusConnectionsDemo.AppHost
```

Both containers appear in the dashboard and `frontend` receives the usual local service
discovery values pointing at the `backend` container's proxied endpoint. Nothing about this
step is Radius-specific — that is the point.

Stop the AppHost before continuing.

## 2. Publish

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh publish --non-interactive
cat RadiusConnectionsDemo.AppHost/aspire-output/app.bicep
```

Compared with `RadiusDemo`, the generated Bicep gains a second
`Radius.Compute/containers` resource, and the consumer carries an `env` block:

```bicep
resource frontend 'Radius.Compute/containers@2025-08-01-preview' = {
  name: 'frontend'
  properties: {
    containers: {
      frontend: {
        image: 'mcr.microsoft.com/azuredocs/aci-helloworld:latest'
        env: {
          BACKEND_HTTP: {
            value: 'http://backend-backend.radius-connections.svc.cluster.local'
          }
          services__backend__http__0: {
            value: 'http://backend-backend.radius-connections.svc.cluster.local'
          }
        }
        ports: {
          http: {
            containerPort: 80
            protocol: 'TCP'
          }
        }
      }
    }
    application: app.id
    environment: radiusenv.id
  }
}
```

Three things are worth pausing on.

**There is no `connections` block.** A container is not a Radius backing resource, so the
publisher emits no `connections` entry for this reference. Service discovery alone connects
the two workloads.

**The host name is doubled: `backend-backend`.** The Radius Kubernetes container recipe names
the `Service` it creates `${normalizedName}-${containerName}`. Aspire uses the resource name
for both, so a resource named `backend` produces a `Service` named `backend-backend`. Aspire
derives the `services__*` value from that same rule, so the two always agree.

**Both `services__backend__http__0` and `BACKEND_HTTP` are emitted.** The first is what
`Microsoft.Extensions.ServiceDiscovery` reads; the second is the connection-property form. A
container image that is not service-discovery aware can read the plain variable directly.

## 3. Deploy

Follow `RadiusDemo` sections 3 and 4 to create the dedicated KinD cluster and Radius
workspace. This demo uses its own namespace, so create that one instead:

```bash
kubectl --context kind-aspire-radius-demo create namespace radius-connections
```

Then deploy:

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh deploy --non-interactive
```

## 4. Verify that service discovery actually resolves

Publishing proves what Aspire wrote. This step proves the value works in the cluster.

Wait for both workloads:

```bash
kubectl --context kind-aspire-radius-demo wait --for=condition=Ready pod \
  -n radius-connections \
  -l radapp.io/application=app \
  --timeout=180s
```

Confirm the recipe created one `Service` per container, with the doubled names:

```bash
kubectl --context kind-aspire-radius-demo get services -n radius-connections
```

Read the injected variables out of the running `frontend` pod:

```bash
FRONTEND=$(kubectl --context kind-aspire-radius-demo get pod \
  -n radius-connections \
  -l radapp.io/resource=frontend \
  -o jsonpath='{.items[0].metadata.name}')

kubectl --context kind-aspire-radius-demo exec -n radius-connections $FRONTEND \
  -- env | grep -E 'services__|BACKEND_'
```

Now call the backend from inside the frontend, using only the injected variable:

```bash
kubectl --context kind-aspire-radius-demo exec -n radius-connections $FRONTEND \
  -- sh -c 'curl -s -o /dev/null -w "backend responded %{http_code}\n" "$services__backend__http__0"'
```

The expected output is `backend responded 200`. The URL was chosen by Aspire at publish time
and the `Service` it names was created by a Radius recipe at deploy time; nothing in this
walkthrough reconciled the two by hand.

## 5. Confirm the Radius view

```bash
rad app graph -a app --preview
```

```text
Name: backend (Radius.Compute/containers)
Connections: (none)
Resources:
  backend (apps/Deployment)
  backend-backend (core/Service)

Name: frontend (Radius.Compute/containers)
Connections: (none)
Resources:
  frontend (apps/Deployment)
  frontend-frontend (core/Service)
```

Both containers appear, each owning the `Deployment` and doubled-name `Service` the recipe
created — but `Connections: (none)`. The relationship between them exists only as an
environment variable that Aspire resolved, so Radius has no declared dependency to show. The
next demo produces a real connection here.

## What does not carry over

The Radius container schema that this integration emits covers the image, environment
variables, ports, and connections. Other parts of an Aspire container resource are not
emitted today, so avoid relying on them in a Radius-targeted AppHost:

- `WithEntrypoint` and `WithArgs` — the deployed container runs its image's default command.
- `WithVolume` and `WithBindMount` — no volume mounts are generated.
- Health check annotations — Radius recipes do not receive probe configuration.

A container whose behavior depends on overridden arguments will run locally and then behave
differently once deployed, so prefer images that are correct with their default command.

## 6. Clean up

All demos in this series publish a Radius application named `app` into the same resource
group, so this demo's resources linger and show up in the next demo's `rad app graph` unless
you remove them:

```bash
rad resource delete Radius.Compute/containers frontend --yes
rad resource delete Radius.Compute/containers backend --yes
rad resource delete Radius.Core/applications app --yes
kubectl --context kind-aspire-radius-demo delete namespace radius-connections
```

Then follow `RadiusDemo` section 7 to remove the workspace and cluster and restore your
previous Radius workspace and Kubernetes context.

## Next steps

Continue to `playground/RadiusRecipesDemo`, which adds a Redis cache and shows real Radius
`connections`, recipe packs, and recipe parameters.

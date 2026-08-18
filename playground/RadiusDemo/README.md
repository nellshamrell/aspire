# RadiusDemo

This demo introduces Radius as an Aspire publish and deployment target. It uses a
single public container so the walkthrough stays focused on the mapping from an
Aspire application model to Radius Bicep and then to Kubernetes resources.

The demo intentionally has a README even though most playground projects do not.
Radius is inactive during local development, and the deployment path requires
external cluster tooling that is not discoverable from the AppHost alone.

## What you will learn

1. Radius does not affect the normal Aspire inner loop.
2. `AddRadiusEnvironment` selects Radius as a publish/deploy target.
3. `aspire publish` generates `app.bicep` and `bicepconfig.json`.
4. `aspire deploy` invokes `rad deploy`.
5. Radius and Kubernetes can both be used to inspect the deployed workload.

## Prerequisites

- The repository has been restored with `./restore.sh`.
- Docker is running.
- `kind`, `kubectl`, and Radius CLI 0.59.x are on `PATH`.
- The Radius CLI minor version matches the Radius Bicep extension version generated
  in `bicepconfig.json`.

The commands below run from `playground/RadiusDemo`.

## 1. Run locally without Radius

```bash
dotnet run --project RadiusDemo.AppHost
```

Open the Aspire dashboard URL printed in the console. The `web` container runs
through the normal Aspire development workflow, but there is no `radius` resource
in the dashboard. `AddRadiusEnvironment` is intentionally inert in Run mode.

Stop the AppHost before continuing.

## 2. Publish the Radius Bicep

Publishing does not require a Radius installation or Kubernetes cluster.

`ASPIRE_PLAYGROUND` changes CLI rendering for the playground test harness, so unset
it before running the walkthrough commands directly.

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh publish --non-interactive
```

The output contains:

```text
RadiusDemo.AppHost/aspire-output/
  app.bicep
  bicepconfig.json
```

Inspect both files:

```bash
cat RadiusDemo.AppHost/aspire-output/app.bicep
cat RadiusDemo.AppHost/aspire-output/bicepconfig.json
```

`app.bicep` begins with `extension radius` and includes:

- `Radius.Core/recipePacks`
- `Radius.Core/environments`
- `Radius.Core/applications`
- `Radius.Compute/containers`

`bicepconfig.json` pins the Radius Bicep extension used to compile those resource
types.

The generated container image is
`mcr.microsoft.com/azuredocs/aci-helloworld:latest`. Aspire.Hosting.Radius warns
that `latest` can use Kubernetes' `Always` pull policy and suggests pre-loading
images for KinD. Pre-loading is not required for this walkthrough because the
public image is pulled from MCR.

## 3. Create a local Radius environment

This walkthrough requires a dedicated KinD cluster. Do not reuse an existing or
shared cluster because cleanup deletes the entire cluster.

Before continuing, note the current Radius workspace and Kubernetes context, if
they are configured:

```bash
rad workspace show
kubectl config current-context
```

Creating the cluster and workspace below changes the globally active Kubernetes
context and Radius workspace. You will restore the previous values during cleanup.

Create the dedicated KinD cluster and install Radius:

```bash
kind create cluster --name aspire-radius-demo --wait=120s
rad install kubernetes --kubecontext kind-aspire-radius-demo
```

`rad install kubernetes` provisions the `default` resource group and environment.
Radius 0.59 requires an explicit active workspace that selects them:

```bash
rad workspace create kubernetes aspire-radius-demo \
  --context kind-aspire-radius-demo \
  --group default \
  --environment default
```

Wait for the Radius control plane:

```bash
kubectl --context kind-aspire-radius-demo get pods -n radius-system
```

All Radius pods should be ready before deployment.

The namespace configured by `.WithNamespace("radius-demo")` must already exist:

```bash
kubectl --context kind-aspire-radius-demo create namespace radius-demo
```

### Using AKS instead of KinD

Every demo in this progression also runs unchanged on AKS. The only differences are how the
cluster is created and how images reach it:

```bash
az aks create -g <resource-group> -n <cluster> --node-count 2 --generate-ssh-keys
az aks get-credentials -g <resource-group> -n <cluster>
rad install kubernetes --kubecontext <cluster>
rad workspace create kubernetes <cluster> \
  --context <cluster> --group default --environment default
```

Two caveats:

- Container images must come from a registry the cluster can pull from; there is no
  `kind load docker-image` equivalent. See `RadiusProjectDemo` for the ACR walkthrough. The
  public images used by demos 1–3 pull without any extra setup.
- Cleanup deletes billable resources. Prefer putting the cluster and registry in a dedicated
  resource group so `az group delete` removes everything in one step.

Radius behavior itself is identical on both — including recipe-provisioned resource naming and
the generated Service names.

## 4. Deploy

Confirm the active workspace is `aspire-radius-demo` and its Kubernetes context is
`kind-aspire-radius-demo` before deploying:

```bash
rad workspace show
```

If workspace creation failed because the name already exists, do not pass
`--force`: cleanup would delete the overwritten workspace. Delete the existing
workspace first only if it is stale and safe to remove, or choose another
dedicated name and update every later workspace reference.

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh deploy --non-interactive
```

The Radius deployment pipeline regenerates the Bicep artifacts in
`RadiusDemo.AppHost/aspire-output` and invokes `rad deploy` against the active
workspace.

## 5. Verify with Radius

The publisher creates a Radius application named `app`.

```bash
rad app graph -a app --preview
rad resource list Radius.Compute/containers
```

`--preview` selects the `Radius.Core` application graph used by the integration.
`rad resource list` is scoped by the active workspace and its default resource
group. Do not add `--application` to that command because it resolves through the
legacy application API, which does not see the preview application. `rad app
graph` still takes `-a`/`--application` together with `--preview`.

## 6. Verify with Kubernetes and curl

Wait for the Radius-managed pod:

```bash
kubectl --context kind-aspire-radius-demo wait --for=condition=Ready pod \
  -n radius-demo \
  -l radapp.io/application=app \
  --timeout=180s
```

Inspect the generated workload:

```bash
kubectl --context kind-aspire-radius-demo get pods,deployments \
  -n radius-demo \
  -l radapp.io/application=app \
  --show-labels
```

Resolve the Deployment and forward its HTTP port:

```bash
DEPLOY=$(kubectl --context kind-aspire-radius-demo get deployment \
  -n radius-demo \
  -l radapp.io/resource=web \
  -o jsonpath='{.items[0].metadata.name}')

kubectl --context kind-aspire-radius-demo port-forward \
  -n radius-demo deployment/$DEPLOY 18080:80
```

In another terminal:

```bash
curl -I http://localhost:18080/
```

The expected response is HTTP 200.

## 7. Clean up

Stop the port-forward, delete the local walkthrough workspace, remove the
dedicated cluster, then restore the previous workspace and Kubernetes context if
they were configured:

```bash
rad workspace delete aspire-radius-demo --yes
kind delete cluster --name aspire-radius-demo
rad workspace switch <previous-workspace>
kubectl config use-context <previous-context>
```

Omit either restore command if its corresponding inspection command reported no
configured value before the walkthrough. The `default` Radius resource group and
environment are stored in the dedicated cluster and are removed with it.

## Next steps

The Radius demos are a progression; continue in order:

| Demo | Adds |
|------|------|
| **`RadiusDemo`** | **One container, publish, deploy, verify** |
| `RadiusConnectionsDemo` | A second container and service discovery between them |
| `RadiusRecipesDemo` | A backing resource, recipes, and real Radius `connections` |
| `RadiusProjectDemo` | A .NET project instead of a public image |

After the container-only flow is familiar:

- Continue to `playground/RadiusConnectionsDemo`.
- Read `src/Aspire.Hosting.Radius/README.md` for recipe parameters, cloud provider
  configuration, and secret stores.
- Compare the generated Bicep with the snapshots under
  `tests/Aspire.Hosting.Radius.Tests/Snapshots`.
- Review `tests/Aspire.Cli.EndToEnd.Tests/RadiusDeployTests.cs`, which exercises the
  same image and deployment flow in CI.

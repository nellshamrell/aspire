# RadiusProjectDemo

This is the fourth demo in the Radius series. The earlier demos all used a public container
image so that nothing distracted from the Aspire-to-Radius mapping. This one uses what an
Aspire developer actually ships: a .NET project.

It is also the demo most likely to be your first real friction point, because the Radius
publisher does not build or push project images yet. That gap is deliberate here — the
walkthrough hits the failure first, then fixes it.

## Where this fits

| Demo | Adds |
|------|------|
| `RadiusDemo` | One container, publish, deploy, verify |
| `RadiusConnectionsDemo` | A second container and service discovery between them |
| `RadiusRecipesDemo` | A backing resource, recipes, and real Radius `connections` |
| **`RadiusProjectDemo`** | **A .NET project instead of a public image** |

## What you will learn

1. Why `aspire publish` fails for a project with no image, and what the error asks you to do.
2. How to build a project image with the .NET SDK and attach it with `WithContainerImage`.
3. Why the image tag matters on KinD.
4. How a project's endpoint becomes a container port of 8080.

## Prerequisites

Same as `RadiusDemo`: a restored repository, Docker, and `kind`, `kubectl`, and Radius CLI
0.59.x on `PATH`. All commands below run from `playground/RadiusProjectDemo`.

## 1. Run locally

```bash
dotnet run --project RadiusProjectDemo.AppHost
```

`apiservice` runs from source with the usual build, debug, and hot reload behavior. Publishing
is the only place Radius participates.

Stop the AppHost before continuing.

## 2. See the failure first

Open `RadiusProjectDemo.AppHost/AppHost.cs` and comment out the `WithContainerImage` line so
the project is declared on its own:

```csharp
builder.AddProject<Projects.RadiusProjectDemo_ApiService>("apiservice");
```

Then publish:

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh publish --non-interactive
```

The publish fails:

```text
Project resource 'apiservice' cannot be published to Radius because no container image has
been associated with it. The Aspire.Hosting.Radius integration does not yet build or push
project images. As a workaround, build and push an image to a registry the target cluster can
pull from, then attach it via WithContainerImage("<registry>/<image>:<tag>") on the project
resource. Tracking issue: https://github.com/microsoft/aspire/issues/16844.
```

This failure is intentional and worth understanding. The publisher could have fallen back to
an image name like `apiservice:latest`, but that image exists nowhere, so the deploy would
succeed and the pod would sit in `ImagePullBackOff` — a Kubernetes-level symptom that says
nothing about the Aspire model. Failing at publish keeps the diagnosis in the tool that caused
it. Restore the `WithContainerImage` line before continuing.

## 3. Build and push the project image

The AppHost defaults to `radiusdemo/apiservice:1.0`, so produce exactly that. The image
reference is configurable — `builder.Configuration["ApiImage"]` — which is what makes the same
AppHost work against both a local KinD cluster and a registry-backed cluster such as AKS
(see [Deploying to AKS instead](#deploying-to-aks-instead)). The .NET SDK builds OCI images
without a Dockerfile:

```bash
cd RadiusProjectDemo.ApiService
../../../dotnet.sh publish /t:PublishContainer \
  -p:ContainerRepository=radiusdemo/apiservice \
  -p:ContainerImageTag=1.0
cd ..
```

Confirm it landed in the local daemon:

```bash
docker images radiusdemo/apiservice
```

**The tag is not incidental.** Kubernetes treats an image tagged `latest` as an `Always` pull
policy, and the Radius container schema has no `imagePullPolicy` field to override it — so a
`latest` image must be reachable from a registry. An explicit tag such as `1.0` lets a locally
built image loaded into KinD be used without any registry. The publisher warns when it sees a
`latest` tag or an image with no registry prefix for exactly this reason.

## 4. Publish

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh publish --non-interactive
cat RadiusProjectDemo.AppHost/aspire-output/app.bicep
```

The project is emitted as an ordinary Radius container:

```bicep
resource apiservice 'Radius.Compute/containers@2025-08-01-preview' = {
  name: 'apiservice'
  properties: {
    containers: {
      apiservice: {
        image: 'radiusdemo/apiservice:1.0'
        env: {
          OTEL_DOTNET_EXPERIMENTAL_OTLP_RETRY: {
            value: 'in_memory'
          }
          ASPNETCORE_FORWARDEDHEADERS_ENABLED: {
            value: 'true'
          }
          HTTP_PORTS: {
            value: '8080'
          }
        }
        ports: {
          http: {
            containerPort: 8080
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

Two details are worth noting.

**The port is 8080, which appears nowhere in the AppHost.** The project's HTTP endpoint has no
explicit target port, so the publisher applies the same default as the Kubernetes publisher:
container port 8080. It also sets `HTTP_PORTS=8080` so the app listens where the port
declaration says it does, and the recipe creates a matching `Service`. Local `applicationUrl`
ports from `launchSettings.json` are a development-time concern and do not follow the app into
the cluster.

**The ASP.NET Core environment variables were added for you.** Aspire contributes the same
configuration a project gets from other publish targets, including forwarded-headers handling
and OTLP retry behavior.

## 5. Deploy

Follow `RadiusDemo` sections 3 and 4 to create the dedicated KinD cluster and Radius
workspace, then create this demo's namespace:

```bash
kubectl --context kind-aspire-radius-demo create namespace radius-project
```

Because the image only exists in your local Docker daemon, load it into the cluster:

```bash
kind load docker-image radiusdemo/apiservice:1.0 --name aspire-radius-demo
```

Skipping this step is the most common failure in this walkthrough; the deploy succeeds and the
pod then fails to pull. Deploy:

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh deploy --non-interactive
```

### Deploying to AKS instead

`kind load docker-image` has no equivalent on a managed cluster — the image must live in a
registry the cluster is allowed to pull from. On AKS that means Azure Container Registry:

```bash
# One-time: create the registry and grant the cluster pull rights.
az acr create -g <resource-group> -n <acrname> --sku Basic
az aks update -g <resource-group> -n <cluster> --attach-acr <acrname>
```

`--attach-acr` assigns the cluster's kubelet identity the `AcrPull` role, so no image pull
secret is needed in the Radius container definition. Role assignment propagation can take a
minute or two.

Build and push to ACR rather than the local daemon:

```bash
cd RadiusProjectDemo.ApiService
az acr login -n <acrname>
../../../dotnet.sh publish /t:PublishContainer \
  -p:ContainerRegistry=<acrname>.azurecr.io \
  -p:ContainerRepository=radiusdemo/apiservice \
  -p:ContainerImageTag=1.0
cd ..
```

> If the push fails with `CONTAINER1008: Failed retrieving credentials`, your Docker config
> points at a credential helper the SDK cannot invoke (common under WSL, where `credsStore` is
> `desktop.exe`). Bypass the helper by passing credentials directly:
>
> ```bash
> SDK_CONTAINER_REGISTRY_UNAME=<user> SDK_CONTAINER_REGISTRY_PWORD=<password> \
>   ../../../dotnet.sh publish /t:PublishContainer ...
> ```

Then deploy, pointing the AppHost at the registry-qualified image:

```bash
unset ASPIRE_PLAYGROUND
../../run-aspire.sh deploy --non-interactive -- \
  --ApiImage <acrname>.azurecr.io/radiusdemo/apiservice:1.0
```

Everything else in this walkthrough — the generated Bicep, the `apiservice-apiservice` Service
name, and the port 8080 default — is identical on AKS.

```bash
kubectl --context kind-aspire-radius-demo wait --for=condition=Ready pod \
  -n radius-project \
  -l radapp.io/application=app \
  --timeout=180s
```

Forward the deployment and call the API:

```bash
DEPLOY=$(kubectl --context kind-aspire-radius-demo get deployment \
  -n radius-project \
  -l radapp.io/resource=apiservice \
  -o jsonpath='{.items[0].metadata.name}')

kubectl --context kind-aspire-radius-demo port-forward \
  -n radius-project deployment/$DEPLOY 18080:8080
```

In another terminal:

```bash
curl http://localhost:18080/
```

The response is `Hello from <pod-name>`, which confirms the reply came from the
Radius-managed pod rather than from a local process.

## 7. Iterating

There is no image build step in `aspire deploy` for projects, so after changing project code
the loop is:

```bash
cd RadiusProjectDemo.ApiService
../../../dotnet.sh publish /t:PublishContainer \
  -p:ContainerRepository=radiusdemo/apiservice \
  -p:ContainerImageTag=1.1
cd ..
kind load docker-image radiusdemo/apiservice:1.1 --name aspire-radius-demo
```

Then update the tag in `WithContainerImage` and redeploy. Bump the tag rather than reusing one:
Kubernetes will not restart pods for a changed image that carries the same tag. Once
[microsoft/aspire#16844](https://github.com/microsoft/aspire/issues/16844) lands, this becomes
part of `aspire deploy`.

## 8. Clean up

```bash
rad resource delete Radius.Compute/containers apiservice --yes
rad resource delete Radius.Core/applications app --yes
kubectl --context kind-aspire-radius-demo delete namespace radius-project
```

Then follow `RadiusDemo` section 7 to remove the workspace and cluster and restore your
previous Radius workspace and Kubernetes context.

## Next steps

You have now covered the four things most Aspire applications need from Radius: a workload,
service discovery, backing resources provisioned by recipes, and your own project code. From
here, `src/Aspire.Hosting.Radius/README.md` documents the platform-team features — cloud
providers, secret stores, and the `ConfigureRadiusInfrastructure` escape hatch.

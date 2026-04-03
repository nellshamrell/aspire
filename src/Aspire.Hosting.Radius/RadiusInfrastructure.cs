// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

using Aspire.Hosting.Radius.Preview;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Subscribes to Aspire application lifecycle events to configure Radius compute environment
/// annotations and optionally start the Radius dashboard container.
/// </summary>
internal sealed class RadiusInfrastructure(ILogger<RadiusInfrastructure> logger) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }

    private async Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        var model = @event.Model;

        var radiusEnvironments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();

        if (radiusEnvironments.Count == 0)
        {
            return;
        }

        foreach (var radiusEnv in radiusEnvironments)
        {
            logger.LogInformation(
                "Configuring Radius environment '{EnvironmentName}' (namespace: {Namespace}, dashboard: {DashboardEnabled})",
                radiusEnv.EnvironmentName,
                radiusEnv.Namespace,
                radiusEnv.DashboardEnabled);

            // Attach DeploymentTargetAnnotation to all compute resources FIRST
            // so the preview generator can filter by annotation.
            AttachDeploymentTargetAnnotations(model, radiusEnv);

            RadiusDashboardResource? dashboard = null;

            if (radiusEnv.DashboardEnabled)
            {
                try
                {
                    dashboard = AddDashboardResource(model, radiusEnv);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create Radius dashboard container for environment '{EnvironmentName}'. Continuing without dashboard.", radiusEnv.EnvironmentName);
                }
            }

            // Generate preview data and configure dashboard bind mount
            if (dashboard is not null)
            {
                try
                {
                    var previewDir = Path.Combine(Path.GetTempPath(), $"radius-preview-{Guid.NewGuid():N}");
                    var generator = new PreviewGraphGenerator(logger);
                    await generator.GenerateAsync(model, radiusEnv, previewDir).ConfigureAwait(false);

                    // Add bind mount from temp dir to /app/preview/ in dashboard container
                    dashboard.Annotations.Add(new ContainerMountAnnotation(
                        previewDir, "/app/preview", ContainerMountType.BindMount, isReadOnly: true));

                    // Set RADIUS_PREVIEW_MODE env var on dashboard container
                    dashboard.Annotations.Add(new EnvironmentCallbackAnnotation(
                        "RADIUS_PREVIEW_MODE", () => "true"));

                    logger.LogInformation(
                        "Preview data generated and mounted at /app/preview in dashboard container '{DashboardName}'",
                        dashboard.Name);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to generate preview data for environment '{EnvironmentName}'. Dashboard will run without preview.", radiusEnv.EnvironmentName);
                }
            }
        }
    }

    private void AttachDeploymentTargetAnnotations(DistributedApplicationModel model, RadiusEnvironmentResource radiusEnv)
    {
        foreach (var resource in model.Resources)
        {
            // Skip the Radius environment resource itself and dashboard resources
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            // Skip resources explicitly targeted to a different compute environment
            var resourceComputeEnvironment = resource.GetComputeEnvironment();
            if (resourceComputeEnvironment is not null && resourceComputeEnvironment != radiusEnv)
            {
                continue;
            }

            // Only attach if the resource doesn't already have a DeploymentTargetAnnotation
            // pointing to this Radius environment
            var existingAnnotations = resource.Annotations.OfType<DeploymentTargetAnnotation>();
            if (existingAnnotations.Any(a => a.DeploymentTarget == radiusEnv))
            {
                continue;
            }

            var annotation = new DeploymentTargetAnnotation(radiusEnv)
            {
                ComputeEnvironment = radiusEnv,
            };

            resource.Annotations.Add(annotation);

            logger.LogDebug(
                "Attached DeploymentTargetAnnotation to resource '{ResourceName}' targeting Radius environment '{EnvironmentName}'",
                resource.Name,
                radiusEnv.EnvironmentName);
        }
    }

    private static RadiusDashboardResource? AddDashboardResource(DistributedApplicationModel model, RadiusEnvironmentResource radiusEnv)
    {
        var dashboardName = $"{radiusEnv.Name}-dashboard";

        // Don't add if a dashboard for this environment already exists
        if (model.Resources.OfType<RadiusDashboardResource>().Any(r => r.Name == dashboardName))
        {
            return model.Resources.OfType<RadiusDashboardResource>().First(r => r.Name == dashboardName);
        }

        var dashboard = new RadiusDashboardResource(dashboardName);

        // Add the container image annotation
        dashboard.Annotations.Add(new ContainerImageAnnotation
        {
            Image = RadiusDashboardResource.DefaultImage,
            Tag = RadiusDashboardResource.DefaultTag,
        });

        // Add endpoint annotation for port 7007
        dashboard.Annotations.Add(new EndpointAnnotation(
            protocol: System.Net.Sockets.ProtocolType.Tcp,
            uriScheme: "http",
            name: "http",
            port: RadiusDashboardResource.DefaultPort,
            targetPort: RadiusDashboardResource.DefaultPort,
            isProxied: false));

        model.Resources.Add(dashboard);

        // Set the endpoint reference on the environment resource
        radiusEnv.DashboardEndpoint = new EndpointReference(dashboard, "http");

        return dashboard;
    }
}

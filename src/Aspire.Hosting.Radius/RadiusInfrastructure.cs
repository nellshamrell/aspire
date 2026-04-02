// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

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

    private Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        var model = @event.Model;

        var radiusEnvironments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();

        if (radiusEnvironments.Count == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var radiusEnv in radiusEnvironments)
        {
            logger.LogInformation(
                "Configuring Radius environment '{EnvironmentName}' (namespace: {Namespace}, dashboard: {DashboardEnabled})",
                radiusEnv.EnvironmentName,
                radiusEnv.Namespace,
                radiusEnv.DashboardEnabled);

            AttachDeploymentTargetAnnotations(model, radiusEnv);

            if (radiusEnv.DashboardEnabled)
            {
                try
                {
                    AddDashboardResource(model, radiusEnv);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create Radius dashboard container for environment '{EnvironmentName}'. Continuing without dashboard.", radiusEnv.EnvironmentName);
                }
            }
        }

        return Task.CompletedTask;
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

    private static void AddDashboardResource(DistributedApplicationModel model, RadiusEnvironmentResource radiusEnv)
    {
        var dashboardName = $"{radiusEnv.Name}-dashboard";

        // Don't add if a dashboard for this environment already exists
        if (model.Resources.OfType<RadiusDashboardResource>().Any(r => r.Name == dashboardName))
        {
            return;
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
    }
}

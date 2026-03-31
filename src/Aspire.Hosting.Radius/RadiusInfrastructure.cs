// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Subscribes to Aspire lifecycle events to set up Radius environment annotations
/// and optionally start the dashboard container.
/// </summary>
internal sealed class RadiusInfrastructure(
    ILogger<RadiusInfrastructure> logger,
    DistributedApplicationExecutionContext executionContext) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(
        IDistributedApplicationEventing eventing,
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        var environments = @event.Model.Resources
            .OfType<RadiusEnvironmentResource>()
            .ToArray();

        if (environments.Length == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var environment in environments)
        {
            logger.LogInformation(
                "Setting up Radius environment '{Name}' with namespace '{Namespace}', dashboard {DashboardState}",
                environment.Name,
                environment.Namespace,
                environment.DashboardEnabled ? "enabled" : "disabled");

            // Attach DeploymentTargetAnnotation to all untargeted compute resources
            foreach (var resource in @event.Model.GetComputeResources())
            {
                var existingEnv = resource.Annotations
                    .OfType<DeploymentTargetAnnotation>()
                    .FirstOrDefault()?.ComputeEnvironment;

                if (existingEnv is not null && existingEnv != environment)
                {
                    continue;
                }

                resource.Annotations.Add(new DeploymentTargetAnnotation(environment)
                {
                    ComputeEnvironment = environment
                });
            }

            // Create dashboard container when enabled and running in run mode
            if (environment.DashboardEnabled && executionContext.IsRunMode)
            {
                try
                {
                    AddDashboardResource(@event.Model, environment);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Failed to create Radius dashboard container for environment '{Name}'. " +
                        "Development will continue without the dashboard.",
                        environment.Name);
                }
            }
        }

        return Task.CompletedTask;
    }

    private void AddDashboardResource(DistributedApplicationModel model, RadiusEnvironmentResource environment)
    {
        var dashboardName = $"{environment.Name}-dashboard";
        var dashboard = new RadiusDashboardResource(dashboardName);

        dashboard.Annotations.Add(new ContainerImageAnnotation
        {
            Image = RadiusDashboardResource.DefaultImage,
            Tag = RadiusDashboardResource.DefaultTag
        });

        dashboard.Annotations.Add(new EndpointAnnotation(
            ProtocolType.Tcp,
            uriScheme: "http",
            transport: "http",
            name: "http",
            port: RadiusDashboardResource.DefaultPort,
            targetPort: RadiusDashboardResource.DefaultPort));

        var endpointAnnotation = dashboard.Annotations
            .OfType<EndpointAnnotation>()
            .First();
        environment.DashboardEndpoint = new EndpointReference(dashboard, endpointAnnotation);

        model.Resources.Add(dashboard);

        logger.LogInformation(
            "Radius dashboard '{DashboardName}' will be available on port {Port}",
            dashboardName,
            RadiusDashboardResource.DefaultPort);
    }
}

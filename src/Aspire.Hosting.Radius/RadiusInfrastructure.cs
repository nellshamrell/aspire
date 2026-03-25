// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002 // IComputeEnvironmentResource is experimental
#pragma warning disable ASPIRECOMPUTE003 // Compute resource APIs are experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Radius.Preview;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Implements <see cref="IDistributedApplicationEventingSubscriber"/> to configure Radius resources
/// before the application starts. Subscribes to <see cref="BeforeStartEvent"/> to attach
/// deployment target annotations and optionally start the Radius dashboard.
/// </summary>
internal sealed class RadiusInfrastructure(
    ILogger<RadiusInfrastructure> logger) : IDistributedApplicationEventingSubscriber
{
    internal async Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
    {
        var radiusEnvironments = @event.Model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            return;
        }

        foreach (var environment in radiusEnvironments)
        {
            logger.LogInformation("Configuring Radius environment '{EnvironmentName}' (namespace: {Namespace}, dashboard: {DashboardEnabled})",
                environment.Name, environment.Namespace, environment.DashboardEnabled);

            // Register dashboard container in the model if enabled
            if (environment.DashboardEnabled && environment.Dashboard?.Resource is RadiusDashboardResource dashboard)
            {
                // Generate preview data before starting the dashboard
                var previewDir = Path.Combine(Path.GetTempPath(), $"radius-preview-{Guid.NewGuid():N}");
                var generator = new PreviewGraphGenerator(logger);
                await generator.GenerateAsync(@event.Model, environment, previewDir, cancellationToken).ConfigureAwait(false);

                // Add bind mount and env var to dashboard container
                environment.Dashboard.WithBindMount(previewDir, "/app/preview", isReadOnly: true);
                environment.Dashboard.WithEnvironment("RADIUS_PREVIEW_MODE", "true");

                @event.Model.Resources.Add(dashboard);
                environment.DashboardEndpoint = dashboard.PrimaryEndpoint;
                logger.LogInformation("Radius dashboard enabled for environment '{EnvironmentName}' on port {Port}",
                    environment.Name, RadiusDashboardResource.DefaultPort);
            }
            else if (!environment.DashboardEnabled)
            {
                logger.LogInformation("Radius dashboard disabled for environment '{EnvironmentName}'", environment.Name);
            }

            foreach (var r in @event.Model.GetComputeResources())
            {
                // Skip resources that are explicitly targeted to a different compute environment
                var resourceComputeEnvironment = r.GetComputeEnvironment();
                if (resourceComputeEnvironment is not null && resourceComputeEnvironment != environment)
                {
                    continue;
                }

                r.Annotations.Add(new DeploymentTargetAnnotation(environment)
                {
                    ComputeEnvironment = environment
                });
            }
        }
    }

    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }
}

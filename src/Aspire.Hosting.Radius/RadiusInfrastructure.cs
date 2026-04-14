// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Subscribes to <see cref="BeforeStartEvent"/> to configure Radius environment resources
/// by attaching <see cref="DeploymentTargetAnnotation"/> to compute resources.
/// </summary>
internal sealed class RadiusInfrastructure(
    ILogger<RadiusInfrastructure> logger) : IDistributedApplicationEventingSubscriber
{
    private async Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        var radiusEnvironments = @event.Model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            return;
        }

        foreach (var environment in radiusEnvironments)
        {
            logger.LogInformation("Configuring Radius environment '{EnvironmentName}' in namespace '{Namespace}'",
                environment.Name, environment.Namespace);
        }

        // Determine the default environment for untargeted resources (first registered)
        var defaultEnvironment = radiusEnvironments[0];

        // Attach DeploymentTargetAnnotation to all compute resources
        foreach (var resource in @event.Model.GetComputeResources())
        {
            var resourceComputeEnvironment = resource.GetComputeEnvironment();

            if (resourceComputeEnvironment is not null)
            {
                // Resource is explicitly targeted — only attach if it targets a Radius environment
                if (resourceComputeEnvironment is RadiusEnvironmentResource radiusTarget)
                {
                    resource.Annotations.Add(new DeploymentTargetAnnotation(radiusTarget)
                    {
                        ComputeEnvironment = radiusTarget
                    });

                    logger.LogDebug("Attached deployment target annotation to resource '{ResourceName}' targeting Radius environment '{EnvironmentName}'",
                        resource.Name, radiusTarget.Name);
                }
            }
            else
            {
                // Untargeted resource — assign to the default (first) Radius environment only
                resource.Annotations.Add(new DeploymentTargetAnnotation(defaultEnvironment)
                {
                    ComputeEnvironment = defaultEnvironment
                });

                logger.LogDebug("Attached deployment target annotation to untargeted resource '{ResourceName}' using default Radius environment '{EnvironmentName}'",
                    resource.Name, defaultEnvironment.Name);
            }
        }
    }

    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE003

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Subscribes to <see cref="BeforeStartEvent"/> to attach <see cref="DeploymentTargetAnnotation"/>
/// to all compute resources targeting a Radius environment.
/// </summary>
internal sealed class RadiusInfrastructure(
    ILogger<RadiusInfrastructure> logger) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext context, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken)
    {
        var radiusEnvironments = @event.Model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            return Task.CompletedTask;
        }

        var isFirstEnvironment = true;

        foreach (var environment in radiusEnvironments)
        {
            logger.LogInformation("Configuring Radius environment '{EnvironmentName}' with namespace '{Namespace}'.",
                environment.Name, environment.Namespace);

            foreach (var resource in @event.Model.GetComputeResources())
            {
                var resourceComputeEnvironment = resource.GetComputeEnvironment();

                // Skip resources explicitly targeted to a different compute environment
                if (resourceComputeEnvironment is not null && resourceComputeEnvironment != environment)
                {
                    continue;
                }

                // Unscoped resources default to the first Radius environment only,
                // consistent with RadiusInfrastructureBuilder behavior
                if (resourceComputeEnvironment is null && !isFirstEnvironment)
                {
                    continue;
                }

                resource.Annotations.Add(new DeploymentTargetAnnotation(environment)
                {
                    ComputeEnvironment = environment
                });
            }

            isFirstEnvironment = false;
        }

        return Task.CompletedTask;
    }
}

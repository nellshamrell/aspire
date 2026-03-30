// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    ILogger<RadiusInfrastructure> logger) : IDistributedApplicationEventingSubscriber
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

            foreach (var resource in @event.Model.GetComputeResources())
            {
                // Skip resources already targeted to a different compute environment
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
        }

        return Task.CompletedTask;
    }
}

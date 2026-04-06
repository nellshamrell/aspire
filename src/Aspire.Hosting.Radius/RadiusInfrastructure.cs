// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Radius.Annotations;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents the infrastructure for Radius within the Aspire Hosting environment.
/// Subscribes to <see cref="BeforeStartEvent"/> to configure Radius resources before publish.
/// </summary>
internal sealed class RadiusInfrastructure(
    ILogger<RadiusInfrastructure> logger,
    DistributedApplicationExecutionContext executionContext) : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc/>
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        eventing.Subscribe<BeforeStartEvent>(OnBeforeStartAsync);
        return Task.CompletedTask;
    }

    private Task OnBeforeStartAsync(BeforeStartEvent @event, CancellationToken cancellationToken = default)
    {
        if (executionContext.IsRunMode)
        {
            return Task.CompletedTask;
        }

        var radiusEnvironments = @event.Model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            EnsureNoPublishAsRadiusResourceAnnotations(@event.Model);
            return Task.CompletedTask;
        }

        foreach (var environment in radiusEnvironments)
        {
            logger.LogInformation(
                "Setting up Radius environment '{Name}' with namespace '{Namespace}'.",
                environment.Name,
                environment.Namespace);

            foreach (var r in @event.Model.GetComputeResources())
            {
                // Skip resources that are explicitly targeted to a different compute environment
                var resourceComputeEnvironment = r.GetComputeEnvironment();
                if (resourceComputeEnvironment is not null && resourceComputeEnvironment != environment)
                {
                    continue;
                }

                // Create a Radius deployment resource for the compute resource
                var deploymentResource = new RadiusDeploymentResource(
                    $"{r.Name}-radius",
                    r,
                    environment);

                // Add deployment target annotation to the resource
                r.Annotations.Add(new DeploymentTargetAnnotation(deploymentResource)
                {
                    ComputeEnvironment = environment
                });

                logger.LogDebug(
                    "Attached DeploymentTargetAnnotation to resource '{ResourceName}' targeting Radius environment '{EnvironmentName}'.",
                    r.Name,
                    environment.Name);
            }
        }

        return Task.CompletedTask;
    }

    private static void EnsureNoPublishAsRadiusResourceAnnotations(DistributedApplicationModel appModel)
    {
        foreach (var r in appModel.GetComputeResources())
        {
            if (r.HasAnnotationOfType<RadiusResourceCustomizationAnnotation>())
            {
                throw new InvalidOperationException(
                    $"Resource '{r.Name}' is configured to publish as a Radius resource, " +
                    $"but there are no '{nameof(RadiusEnvironmentResource)}' resources. " +
                    $"Ensure you have added one by calling '{nameof(RadiusEnvironmentExtensions.AddRadiusEnvironment)}'.");
            }
        }
    }
}

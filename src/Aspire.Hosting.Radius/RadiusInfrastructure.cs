// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
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
        // Full implementation will be added in Phase 3 (T026)
        // This stub ensures DI registration compiles and the event subscription is established
        if (executionContext.IsRunMode)
        {
            return Task.CompletedTask;
        }

        var radiusEnvironments = @event.Model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var environment in radiusEnvironments)
        {
            logger.LogInformation("Radius environment '{Name}' configured with namespace '{Namespace}'.", environment.Name, environment.Namespace);
        }

        return Task.CompletedTask;
    }
}

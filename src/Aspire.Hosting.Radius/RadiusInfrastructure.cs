// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Subscribes to Aspire application lifecycle events to configure Radius compute environment
/// annotations and optionally start the Radius dashboard container.
/// </summary>
internal sealed class RadiusInfrastructure : IDistributedApplicationEventingSubscriber
{
    /// <inheritdoc />
    public Task SubscribeAsync(IDistributedApplicationEventing eventing, DistributedApplicationExecutionContext executionContext, CancellationToken cancellationToken)
    {
        // Full implementation in T026 — subscribes to BeforeStartEvent
        return Task.CompletedTask;
    }
}

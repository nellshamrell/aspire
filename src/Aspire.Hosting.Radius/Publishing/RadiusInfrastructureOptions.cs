// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Exposes mutable collections of <see cref="ProvisionableResource"/> constructs
/// for AST customization via the <c>ConfigureRadiusInfrastructure</c> callback.
/// </summary>
public sealed class RadiusInfrastructureOptions
{
    /// <summary>
    /// Gets the list of <c>Radius.Core/environments</c> constructs.
    /// </summary>
    public List<ProvisionableResource> Environments { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Core/applications</c> constructs.
    /// </summary>
    public List<ProvisionableResource> Applications { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Core/recipePacks</c> constructs.
    /// </summary>
    public List<ProvisionableResource> RecipePacks { get; } = [];

    /// <summary>
    /// Gets the list of resource type instance constructs
    /// (e.g., <c>Radius.Data/redisCaches</c>, <c>Radius.Messaging/rabbitMQQueues</c>).
    /// </summary>
    public List<ProvisionableResource> ResourceTypeInstances { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Compute/containers</c> workload constructs.
    /// </summary>
    public List<ProvisionableResource> Containers { get; } = [];
}

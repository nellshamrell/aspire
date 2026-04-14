// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius;

/// <summary>
/// Internal per-workload deployment configuration used during Bicep generation.
/// Maps an Aspire container or project resource to its Radius.Compute/containers properties.
/// </summary>
internal sealed class RadiusContainerResource
{
    /// <summary>
    /// Gets or sets the workload name (matches the Aspire resource name).
    /// </summary>
    public required string WorkloadName { get; init; }

    /// <summary>
    /// Gets or sets the container image reference.
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// Gets the connection mappings for this container.
    /// Keys are connection names (resource names), values are Bicep identifiers of the source resource.
    /// </summary>
    public Dictionary<string, string> Connections { get; init; } = [];

    /// <summary>
    /// Gets the environment variables for this container.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; init; } = [];
}

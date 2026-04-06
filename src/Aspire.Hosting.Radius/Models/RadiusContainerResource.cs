// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Represents a container workload configuration for Radius deployment.
/// </summary>
/// <remarks>
/// This is an internal helper class used during Bicep generation to carry
/// per-workload metadata. It is ephemeral and not persisted in the app model.
/// </remarks>
internal sealed class RadiusContainerResource
{
    /// <summary>
    /// Gets or sets the name of the container workload as it appears in Radius.
    /// </summary>
    public required string ContainerName { get; set; }

    /// <summary>
    /// Gets or sets the container image reference.
    /// </summary>
    public required string ContainerImage { get; set; }

    /// <summary>
    /// Gets the mapping of Aspire reference names to Radius portable resource references.
    /// </summary>
    public Dictionary<string, string> Connections { get; } = new();

    /// <summary>
    /// Gets the environment variables for the workload.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new();
}

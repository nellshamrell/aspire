// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Per-workload configuration for Radius deployment; carries resource-specific metadata.
/// </summary>
public sealed class RadiusContainerResource
{
    /// <summary>
    /// Gets or sets the name of the container workload.
    /// </summary>
    public required string ContainerName { get; set; }

    /// <summary>
    /// Gets or sets the container image reference.
    /// </summary>
    public required string ContainerImage { get; set; }

    /// <summary>
    /// Gets the mapping of reference names to Radius portable resource references.
    /// </summary>
    public Dictionary<string, string> Connections { get; } = [];

    /// <summary>
    /// Gets the custom environment variables for the workload.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
}

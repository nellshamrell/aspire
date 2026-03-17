// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Represents per-workload configuration for Radius deployment.
/// </summary>
internal class RadiusContainerResource
{
    /// <summary>
    /// Gets or sets the workload name as it appears in Radius.
    /// </summary>
    public required string WorkloadName { get; set; }

    /// <summary>
    /// Gets or sets the container image reference.
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Gets the mapping of Aspire reference names to Radius portable resource references.
    /// </summary>
    public Dictionary<string, string> Connections { get; } = [];

    /// <summary>
    /// Gets the custom environment variables for the workload.
    /// </summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = [];
}

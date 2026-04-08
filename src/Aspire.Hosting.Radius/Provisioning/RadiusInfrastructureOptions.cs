// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Provides mutable access to the Radius infrastructure AST for user customization
/// via <c>ConfigureRadiusInfrastructure</c>.
/// </summary>
public sealed class RadiusInfrastructureOptions
{
    /// <summary>
    /// Gets the list of Radius environment constructs.
    /// </summary>
    public List<RadiusEnvironmentConstruct> Environments { get; set; } = [];

    /// <summary>
    /// Gets the list of Radius application constructs.
    /// </summary>
    public List<RadiusApplicationConstruct> Applications { get; set; } = [];

    /// <summary>
    /// Gets the list of portable resource constructs (redis, sql, etc.).
    /// </summary>
    public List<RadiusPortableResourceConstruct> PortableResources { get; set; } = [];

    /// <summary>
    /// Gets the list of container resource constructs.
    /// </summary>
    public List<RadiusContainerConstruct> Containers { get; set; } = [];
}

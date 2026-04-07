// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Provides options for customizing the Radius infrastructure AST before Bicep compilation.
/// </summary>
/// <remarks>
/// Passed to the <c>ConfigureRadiusInfrastructure</c> callback to allow users to inspect
/// and modify the generated Azure Provisioning AST before it is compiled to Bicep.
/// </remarks>
public sealed class RadiusInfrastructureOptions
{
    /// <summary>
    /// Gets the Radius environment construct in the AST.
    /// </summary>
    public RadiusEnvironmentConstruct Environment { get; }

    /// <summary>
    /// Gets the Radius application construct in the AST.
    /// </summary>
    public RadiusApplicationConstruct Application { get; }

    /// <summary>
    /// Gets the portable resource constructs (datastores, messaging, etc.) in the AST.
    /// </summary>
    public IList<RadiusPortableResourceConstruct> PortableResources { get; }

    /// <summary>
    /// Gets the container workload constructs in the AST.
    /// </summary>
    public IList<RadiusContainerConstruct> Containers { get; }

    internal RadiusInfrastructureOptions(
        RadiusEnvironmentConstruct environment,
        RadiusApplicationConstruct application,
        IList<RadiusPortableResourceConstruct> portableResources,
        IList<RadiusContainerConstruct> containers)
    {
        Environment = environment;
        Application = application;
        PortableResources = portableResources;
        Containers = containers;
    }
}

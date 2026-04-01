// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Options for customizing the Radius infrastructure AST before Bicep compilation.
/// Passed to the user's <c>ConfigureRadiusInfrastructure</c> callback.
/// </summary>
public sealed class RadiusInfrastructureOptions
{
    internal RadiusInfrastructureOptions(RadiusInfrastructureBuilder builder)
    {
        Builder = builder;
    }

    internal RadiusInfrastructureBuilder Builder { get; }

    /// <summary>
    /// Gets the generated Radius environment constructs that will be compiled to Bicep.
    /// </summary>
    public IReadOnlyList<RadiusEnvironmentConstruct> Environments => Builder.Environments;

    /// <summary>
    /// Gets the generated Radius application constructs that will be compiled to Bicep.
    /// </summary>
    public IReadOnlyList<RadiusApplicationConstruct> Applications => Builder.Applications;

    /// <summary>
    /// Gets the generated Radius portable resource constructs that will be compiled to Bicep.
    /// </summary>
    public IReadOnlyList<RadiusPortableResourceConstruct> PortableResources => Builder.PortableResources;

    /// <summary>
    /// Gets the generated Radius container constructs that will be compiled to Bicep.
    /// </summary>
    public IReadOnlyList<RadiusContainerConstruct> Containers => Builder.Containers;
}

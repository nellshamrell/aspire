// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

using System.Diagnostics.CodeAnalysis;
using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Publishing.Constructs;

/// <summary>
/// Represents a legacy <c>Applications.Core/containers@2023-10-01-preview</c>
/// resource in the Bicep AST.
/// </summary>
/// <remarks>
/// Used as an opt-in fallback when the <c>Radius.Compute/containers</c> UDT
/// has no recipe registered in the target environment. The legacy container
/// type ships with a built-in Kubernetes deployment behaviour, so it deploys
/// without any recipe registration. Schema differences from the UDT version:
/// <list type="bullet">
///   <item><description><c>properties.container.image</c> (singular)</description></item>
///   <item><description>parents to <c>Applications.Core/applications</c> (legacy app)</description></item>
/// </list>
/// </remarks>
[Experimental("ASPIRERADIUS004", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public sealed class LegacyContainerConstruct : ProvisionableResource
{
    private BicepValue<string>? _name;
    private BicepValue<string>? _image;
    private BicepValue<string>? _applicationId;
    private BicepDictionary<ConnectionConstruct>? _connections;
    private BicepDictionary<ContainerEnvVarConstruct>? _env;
    private BicepDictionary<ContainerPortConstruct>? _ports;

    /// <summary>The resource name.</summary>
    public BicepValue<string> ContainerName
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }

    /// <summary>Container image (e.g., "nginx:latest").</summary>
    public BicepValue<string> Image
    {
        get { Initialize(); return _image!; }
        set { Initialize(); _image!.Assign(value); }
    }

    /// <summary>Reference to the legacy application resource ID.</summary>
    public BicepValue<string> ApplicationId
    {
        get { Initialize(); return _applicationId!; }
        set { Initialize(); _applicationId!.Assign(value); }
    }

    /// <summary>
    /// Dictionary of named connections to other resources. Keys are connection
    /// names; values contain source resource ID references.
    /// </summary>
    public BicepDictionary<ConnectionConstruct> Connections
    {
        get { Initialize(); return _connections!; }
        set { Initialize(); _connections!.Assign(value); }
    }

    /// <summary>
    /// Environment variables for the container, keyed by variable name. Each entry carries
    /// a <c>value</c> (a literal or a reference to a Bicep parameter for secret values).
    /// </summary>
    public BicepDictionary<ContainerEnvVarConstruct> Env
    {
        get { Initialize(); return _env!; }
        set { Initialize(); _env!.Assign(value); }
    }

    /// <summary>
    /// Ports exposed by the container, keyed by port name. Each entry carries a
    /// <c>containerPort</c> and an optional <c>protocol</c>.
    /// </summary>
    public BicepDictionary<ContainerPortConstruct> Ports
    {
        get { Initialize(); return _ports!; }
        set { Initialize(); _ports!.Assign(value); }
    }

    /// <summary>Initializes a new <see cref="LegacyContainerConstruct"/> with the given Bicep identifier.</summary>
    public LegacyContainerConstruct(string bicepIdentifier)
        : base(bicepIdentifier, new Azure.Core.ResourceType("Applications.Core/containers"), "2023-10-01-preview")
    {
    }

    /// <inheritdoc />
    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(ContainerName), ["name"]);
        _image = DefineProperty<string>(nameof(Image), ["properties", "container", "image"]);
        _applicationId = DefineProperty<string>(nameof(ApplicationId), ["properties", "application"]);
        _connections = DefineDictionaryProperty<ConnectionConstruct>(nameof(Connections), ["properties", "connections"]);
        // Legacy Applications.Core/containers nests env and ports under the singular `container`.
        _env = DefineDictionaryProperty<ContainerEnvVarConstruct>(nameof(Env), ["properties", "container", "env"]);
        _ports = DefineDictionaryProperty<ContainerPortConstruct>(nameof(Ports), ["properties", "container", "ports"]);
    }
}

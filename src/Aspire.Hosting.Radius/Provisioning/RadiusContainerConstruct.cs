// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents an <c>Applications.Core/containers</c> resource in the Radius AST.
/// </summary>
public sealed class RadiusContainerConstruct
{
    public required string BicepIdentifier { get; set; }
    public required string Name { get; set; }
    public string ResourceType { get; } = "Applications.Core/containers";
    public string ApiVersion { get; } = "2023-10-01-preview";

    /// <summary>Bicep identifier of the application construct.</summary>
    public required string ApplicationBicepIdentifier { get; set; }

    /// <summary>Container image URI.</summary>
    public required string Image { get; set; }

    /// <summary>Environment variables for the container.</summary>
    public Dictionary<string, string> Env { get; set; } = [];

    /// <summary>Port mappings (containerPort → protocol).</summary>
    public List<(int ContainerPort, string Protocol)> Ports { get; set; } = [];

    /// <summary>Connection references (connectionName → portableResourceBicepIdentifier).</summary>
    public Dictionary<string, string> Connections { get; set; } = [];

    /// <summary>Image pull policy. Set to "Never" for kind clusters.</summary>
    public string? ImagePullPolicy { get; set; }
}

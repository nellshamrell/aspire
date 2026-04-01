// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/containers</c> resource (workload) in the Bicep AST.
/// </summary>
public sealed class RadiusContainerConstruct
{
    /// <summary>
    /// Gets or sets the Bicep identifier for the container resource.
    /// </summary>
    public required string BicepIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the Radius container resource name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Bicep identifier of the application resource.
    /// Used to generate <c>application: {appIdentifier}.id</c>.
    /// </summary>
    public required string ApplicationIdentifier { get; set; }

    /// <summary>
    /// The container image reference (e.g., "myregistry.azurecr.io/api:latest").
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Connections to portable resources.
    /// Key = connection name, Value = Bicep identifier of the portable resource.
    /// </summary>
    public Dictionary<string, string> Connections { get; } = new();

    /// <summary>
    /// Gets the Radius resource type emitted for container constructs.
    /// </summary>
    public static string ResourceType => "Applications.Core/containers";

    /// <summary>
    /// Gets the API version emitted for container constructs.
    /// </summary>
    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/containers</c> resource (workload) in the Bicep AST.
/// </summary>
internal sealed class RadiusContainerConstruct
{
    public required string BicepIdentifier { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The Bicep identifier of the application resource.
    /// Used to generate <c>application: {appIdentifier}.id</c>.
    /// </summary>
    public required string ApplicationIdentifier { get; init; }

    /// <summary>
    /// The container image reference (e.g., "myregistry.azurecr.io/api:latest").
    /// </summary>
    public required string Image { get; init; }

    /// <summary>
    /// Connections to portable resources.
    /// Key = connection name, Value = Bicep identifier of the portable resource.
    /// </summary>
    public Dictionary<string, string> Connections { get; } = new();

    public static string ResourceType => "Applications.Core/containers";

    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

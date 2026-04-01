// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius portable resource (e.g., <c>Applications.Datastores/redisCaches</c>) in the Bicep AST.
/// The resource type is set dynamically based on <see cref="Publishing.ResourceTypeMapper"/> output.
/// </summary>
internal sealed class RadiusPortableResourceConstruct
{
    public required string BicepIdentifier { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The Radius portable resource type (e.g., "Applications.Datastores/redisCaches").
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// The Bicep identifier of the application resource.
    /// Used to generate <c>application: {appIdentifier}.id</c>.
    /// </summary>
    public required string ApplicationIdentifier { get; init; }

    /// <summary>
    /// The Bicep identifier of the environment resource.
    /// Used to generate <c>environment: {envIdentifier}.id</c>.
    /// </summary>
    public required string EnvironmentIdentifier { get; init; }

    /// <summary>
    /// Whether this resource uses manual provisioning (no Radius recipe).
    /// </summary>
    public bool IsManualProvisioning { get; init; }

    /// <summary>
    /// The host for manual provisioning mode.
    /// </summary>
    public string? ManualHost { get; init; }

    /// <summary>
    /// The port for manual provisioning mode.
    /// </summary>
    public int? ManualPort { get; init; }

    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

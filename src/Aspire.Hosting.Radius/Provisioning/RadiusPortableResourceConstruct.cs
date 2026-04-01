// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius portable resource (e.g., <c>Applications.Datastores/redisCaches</c>) in the Bicep AST.
/// The resource type is set dynamically based on <see cref="Publishing.ResourceTypeMapper"/> output.
/// </summary>
public sealed class RadiusPortableResourceConstruct
{
    /// <summary>
    /// Gets or sets the Bicep identifier for the portable resource.
    /// </summary>
    public required string BicepIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the Radius portable resource name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Radius portable resource type (e.g., "Applications.Datastores/redisCaches").
    /// </summary>
    public required string ResourceType { get; set; }

    /// <summary>
    /// The Bicep identifier of the application resource.
    /// Used to generate <c>application: {appIdentifier}.id</c>.
    /// </summary>
    public required string ApplicationIdentifier { get; set; }

    /// <summary>
    /// The Bicep identifier of the environment resource.
    /// Used to generate <c>environment: {envIdentifier}.id</c>.
    /// </summary>
    public required string EnvironmentIdentifier { get; set; }

    /// <summary>
    /// Whether this resource uses manual provisioning (no Radius recipe).
    /// </summary>
    public bool IsManualProvisioning { get; set; }

    /// <summary>
    /// The host for manual provisioning mode.
    /// </summary>
    public string? ManualHost { get; set; }

    /// <summary>
    /// The port for manual provisioning mode.
    /// </summary>
    public int? ManualPort { get; set; }

    /// <summary>
    /// The recipe name to use for this resource. When set, emits a <c>recipe: { name: '...' }</c> block.
    /// Only needed when the recipe name differs from "default".
    /// </summary>
    public string? RecipeName { get; set; }

    /// <summary>
    /// Gets the API version emitted for portable resource constructs.
    /// </summary>
    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

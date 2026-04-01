// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/applications</c> resource in the Bicep AST.
/// </summary>
public sealed class RadiusApplicationConstruct
{
    /// <summary>
    /// Gets or sets the Bicep identifier for the application resource.
    /// </summary>
    public required string BicepIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the Radius application resource name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Bicep identifier of the environment resource this application references.
    /// Used to generate <c>environment: {envIdentifier}.id</c>.
    /// </summary>
    public required string EnvironmentIdentifier { get; set; }

    /// <summary>
    /// Gets the Radius resource type emitted for application constructs.
    /// </summary>
    public static string ResourceType => "Applications.Core/applications";

    /// <summary>
    /// Gets the API version emitted for application constructs.
    /// </summary>
    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

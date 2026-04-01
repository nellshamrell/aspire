// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/applications</c> resource in the Bicep AST.
/// </summary>
internal sealed class RadiusApplicationConstruct
{
    public required string BicepIdentifier { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The Bicep identifier of the environment resource this application references.
    /// Used to generate <c>environment: {envIdentifier}.id</c>.
    /// </summary>
    public required string EnvironmentIdentifier { get; init; }

    public static string ResourceType => "Applications.Core/applications";

    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

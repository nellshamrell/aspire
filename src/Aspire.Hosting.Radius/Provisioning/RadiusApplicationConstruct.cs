// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents an <c>Applications.Core/applications</c> resource in the Radius AST.
/// </summary>
public sealed class RadiusApplicationConstruct
{
    public required string BicepIdentifier { get; set; }
    public required string Name { get; set; }
    public string ResourceType { get; } = "Applications.Core/applications";
    public string ApiVersion { get; } = "2023-10-01-preview";

    /// <summary>
    /// Bicep identifier of the environment construct this application belongs to.
    /// Used to generate <c>environment: env.id</c> reference.
    /// </summary>
    public required string EnvironmentBicepIdentifier { get; set; }
}

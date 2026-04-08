// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius portable resource (e.g. <c>Applications.Datastores/redisCaches</c>) in the AST.
/// </summary>
public sealed class RadiusPortableResourceConstruct
{
    public required string BicepIdentifier { get; set; }
    public required string Name { get; set; }
    public required string ResourceType { get; set; }
    public required string ApiVersion { get; set; }

    /// <summary>Bicep identifier of the application construct.</summary>
    public required string ApplicationBicepIdentifier { get; set; }

    /// <summary>Bicep identifier of the environment construct.</summary>
    public required string EnvironmentBicepIdentifier { get; set; }

    /// <summary>Custom recipe name, or null for default recipe.</summary>
    public string? RecipeName { get; set; }

    /// <summary>Custom recipe parameters, or null if none.</summary>
    public Dictionary<string, object>? RecipeParameters { get; set; }

    /// <summary>Provisioning mode (Automatic or Manual).</summary>
    public RadiusResourceProvisioning ResourceProvisioning { get; set; } = RadiusResourceProvisioning.Automatic;

    /// <summary>Host for manual provisioning.</summary>
    public string? Host { get; set; }

    /// <summary>Port for manual provisioning.</summary>
    public int? Port { get; set; }
}

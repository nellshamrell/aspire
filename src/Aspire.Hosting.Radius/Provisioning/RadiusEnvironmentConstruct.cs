// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/environments</c> resource in the Bicep AST.
/// </summary>
internal sealed class RadiusEnvironmentConstruct
{
    public required string BicepIdentifier { get; init; }

    public required string Name { get; init; }

    public string ComputeKind { get; init; } = "kubernetes";

    public required string ComputeNamespace { get; init; }

    /// <summary>
    /// Recipe registrations keyed by portable resource type
    /// (e.g., "Applications.Datastores/redisCaches").
    /// Value is the recipe template path.
    /// </summary>
    public Dictionary<string, RecipeRegistration> Recipes { get; } = new();

    public static string ResourceType => "Applications.Core/environments";

    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

/// <summary>
/// A recipe registration on an environment resource.
/// </summary>
internal sealed class RecipeRegistration
{
    public required string Name { get; init; }
    public string TemplateKind { get; init; } = "bicep";
    public required string TemplatePath { get; init; }
}

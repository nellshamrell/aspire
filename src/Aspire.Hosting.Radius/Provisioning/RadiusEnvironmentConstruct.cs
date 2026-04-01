// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/environments</c> resource in the Bicep AST.
/// </summary>
public sealed class RadiusEnvironmentConstruct
{
    /// <summary>
    /// Gets or sets the Bicep identifier for the environment resource.
    /// </summary>
    public required string BicepIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the Radius environment resource name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the compute kind for the environment.
    /// </summary>
    public string ComputeKind { get; set; } = "kubernetes";

    /// <summary>
    /// Gets or sets the Kubernetes namespace used by the environment.
    /// </summary>
    public required string ComputeNamespace { get; set; }

    /// <summary>
    /// Recipe registrations keyed by portable resource type
    /// (e.g., "Applications.Datastores/redisCaches").
    /// Value is the recipe template path.
    /// </summary>
    public Dictionary<string, RecipeRegistration> Recipes { get; } = new();

    /// <summary>
    /// Gets the Radius resource type emitted for environment constructs.
    /// </summary>
    public static string ResourceType => "Applications.Core/environments";

    /// <summary>
    /// Gets the API version emitted for environment constructs.
    /// </summary>
    public static string ApiVersion => RadiusInfrastructureBuilder.RadiusApiVersion;
}

/// <summary>
/// A recipe registration on an environment resource.
/// </summary>
public sealed class RecipeRegistration
{
    /// <summary>
    /// Gets or sets the name of the registered recipe.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the recipe template kind.
    /// </summary>
    public string TemplateKind { get; set; } = "bicep";

    /// <summary>
    /// Gets or sets the recipe template path.
    /// </summary>
    public required string TemplatePath { get; set; }
}

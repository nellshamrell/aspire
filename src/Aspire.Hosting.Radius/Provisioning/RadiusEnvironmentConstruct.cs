// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents an <c>Applications.Core/environments</c> resource in the Radius AST.
/// </summary>
public sealed class RadiusEnvironmentConstruct
{
    public required string BicepIdentifier { get; set; }
    public required string Name { get; set; }
    public string ResourceType { get; } = "Applications.Core/environments";
    public string ApiVersion { get; } = "2023-10-01-preview";
    public string ComputeKind { get; set; } = "kubernetes";
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Recipe registrations grouped by resource type.
    /// Key = Radius resource type (e.g. "Applications.Datastores/redisCaches"),
    /// Value = (recipeName, templatePath).
    /// </summary>
    public Dictionary<string, List<(string RecipeName, string TemplatePath)>> RecipeRegistrations { get; } = [];
}

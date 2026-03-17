// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Represents a Radius recipe configuration for a resource.
/// </summary>
public class RadiusRecipe
{
    /// <summary>
    /// Gets or sets the recipe name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the path to the recipe template.
    /// </summary>
    public string? TemplatePath { get; set; }

    /// <summary>
    /// Gets the parameters for the recipe.
    /// </summary>
    public Dictionary<string, string> Parameters { get; } = [];
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Describes a Radius recipe with its name, template path, and optional parameters.
/// </summary>
public sealed class RadiusRecipe
{
    /// <summary>
    /// Gets or sets the recipe name as registered in the Radius environment.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the OCI template path for the recipe
    /// (e.g., <c>ghcr.io/radius-project/recipes/local-dev/rediscaches:latest</c>).
    /// </summary>
    public string? TemplatePath { get; set; }

    /// <summary>
    /// Gets the recipe parameters passed to the template.
    /// </summary>
    public Dictionary<string, object> Parameters { get; } = [];
}

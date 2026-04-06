// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Represents a Radius recipe configuration with a registered name and template path.
/// </summary>
public sealed class RadiusRecipe
{
    /// <summary>
    /// Gets or sets the recipe name registered on the Radius environment.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the OCI or file path to the recipe template.
    /// </summary>
    public required string TemplatePath { get; set; }

    /// <summary>
    /// Gets optional parameters to pass to the recipe template.
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}

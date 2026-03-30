// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Defines a Radius recipe for resource provisioning.
/// </summary>
public sealed class RadiusRecipe
{
    /// <summary>
    /// Gets or sets the recipe name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the path to the recipe template.
    /// </summary>
    public required string TemplatePath { get; set; }

    /// <summary>
    /// Gets the recipe parameters.
    /// </summary>
    public Dictionary<string, string> Parameters { get; } = [];
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius recipe configuration for a portable resource.
/// </summary>
public class RadiusRecipe
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
    /// Gets or sets optional parameters passed to the recipe template.
    /// Values can be string, int, bool, or nested objects.
    /// Serialized to correct Bicep value types (not quoted as strings).
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }
}

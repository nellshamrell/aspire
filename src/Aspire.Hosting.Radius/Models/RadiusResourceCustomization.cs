// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius;

/// <summary>
/// Per-resource configuration for overriding default Radius behavior during publishing.
/// </summary>
public class RadiusResourceCustomization
{
    /// <summary>
    /// Gets or sets the custom Radius recipe override for this resource.
    /// When set, the generated Bicep will include a <c>recipe: { name: '...' }</c> block
    /// on the portable resource and register the recipe in the environment's <c>recipeConfig</c>.
    /// </summary>
    public RadiusRecipe? Recipe { get; set; }

    /// <summary>
    /// Gets or sets how the resource is provisioned (Automatic via recipe, or Manual with explicit host/port).
    /// </summary>
    public RadiusResourceProvisioning Provisioning { get; set; } = RadiusResourceProvisioning.Automatic;

    /// <summary>
    /// Gets or sets the host for manual provisioning.
    /// Required when <see cref="Provisioning"/> is <see cref="RadiusResourceProvisioning.Manual"/>.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the port for manual provisioning.
    /// Required when <see cref="Provisioning"/> is <see cref="RadiusResourceProvisioning.Manual"/>.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets or sets connection string format overrides keyed by reference name.
    /// </summary>
    public Dictionary<string, string> ConnectionStringOverrides { get; set; } = [];
}

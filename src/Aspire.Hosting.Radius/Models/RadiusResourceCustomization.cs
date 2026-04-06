// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Configuration for per-resource Radius publishing behavior.
/// </summary>
/// <remarks>
/// Created by developers via <c>PublishAsRadiusResource()</c> to customize
/// how individual resources are published to Radius (recipe overrides,
/// provisioning modes, connection string formats).
/// </remarks>
public sealed class RadiusResourceCustomization
{
    /// <summary>
    /// Gets or sets a custom Radius recipe override for this resource.
    /// </summary>
    /// <remarks>
    /// When set, the resource will use this recipe instead of the default mapping.
    /// Must have both <see cref="RadiusRecipe.Name"/> and <see cref="RadiusRecipe.TemplatePath"/> populated.
    /// </remarks>
    public RadiusRecipe? Recipe { get; set; }

    /// <summary>
    /// Gets or sets how the resource is provisioned in Radius.
    /// </summary>
    public RadiusResourceProvisioning Provisioning { get; set; } = RadiusResourceProvisioning.Automatic;

    /// <summary>
    /// Gets or sets the host address for manually provisioned resources.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the port for manually provisioned resources.
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Gets the connection string format overrides for specific references.
    /// </summary>
    public Dictionary<string, string> ConnectionStringOverrides { get; } = new();
}

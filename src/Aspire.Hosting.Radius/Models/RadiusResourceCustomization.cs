// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Per-resource configuration for overriding default Radius behavior during publishing.
/// Used via <c>PublishAsRadiusResource()</c> to customize recipes, provisioning modes, and connection details.
/// </summary>
public sealed class RadiusResourceCustomization
{
    /// <summary>
    /// Gets or sets the custom Radius recipe override for this resource.
    /// When null, the default recipe for the resource type is used.
    /// </summary>
    public RadiusRecipe? Recipe { get; set; }

    /// <summary>
    /// Gets or sets how the resource is provisioned in Radius.
    /// </summary>
    public RadiusResourceProvisioning Provisioning { get; set; } = RadiusResourceProvisioning.Automatic;

    /// <summary>
    /// Gets or sets the host address for manually provisioned resources.
    /// Required when <see cref="Provisioning"/> is <see cref="RadiusResourceProvisioning.Manual"/>.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the port for manually provisioned resources.
    /// Required when <see cref="Provisioning"/> is <see cref="RadiusResourceProvisioning.Manual"/>.
    /// </summary>
    public int? Port { get; set; }
}

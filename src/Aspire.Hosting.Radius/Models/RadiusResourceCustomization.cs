// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Per-resource configuration for overriding default Radius behavior during publishing.
/// Applied via <see cref="RadiusResourceCustomizationAnnotation"/>.
/// </summary>
public sealed class RadiusResourceCustomization
{
    /// <summary>
    /// Gets or sets the custom Radius recipe override for this resource.
    /// </summary>
    public RadiusRecipe? Recipe { get; set; }

    /// <summary>
    /// Gets or sets how the resource is provisioned in Radius.
    /// </summary>
    public RadiusResourceProvisioning Provisioning { get; set; } = RadiusResourceProvisioning.Automatic;

    /// <summary>
    /// Gets or sets the host for manual provisioning mode.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Gets or sets the port for manual provisioning mode.
    /// </summary>
    public int? Port { get; set; }
}

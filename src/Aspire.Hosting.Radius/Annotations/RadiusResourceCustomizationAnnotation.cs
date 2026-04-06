// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// An annotation that holds Radius-specific customization data for a resource.
/// </summary>
/// <remarks>
/// Attached to resources via <c>PublishAsRadiusResource()</c> and consumed
/// during Bicep generation to apply recipe overrides, provisioning modes,
/// and connection string customizations.
/// </remarks>
internal sealed class RadiusResourceCustomizationAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Gets the customization configuration for the annotated resource.
    /// </summary>
    public RadiusResourceCustomization Customization { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusResourceCustomizationAnnotation"/> class.
    /// </summary>
    /// <param name="customization">The customization configuration to store.</param>
    public RadiusResourceCustomizationAnnotation(RadiusResourceCustomization customization)
    {
        ArgumentNullException.ThrowIfNull(customization);
        Customization = customization;
    }
}

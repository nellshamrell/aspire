// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation that holds <see cref="RadiusResourceCustomization"/> data on a resource.
/// Attached via the <c>PublishAsRadiusResource</c> extension method.
/// </summary>
public sealed class RadiusResourceCustomizationAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Initializes a new instance of <see cref="RadiusResourceCustomizationAnnotation"/>.
    /// </summary>
    /// <param name="customization">The customization data for this resource.</param>
    public RadiusResourceCustomizationAnnotation(RadiusResourceCustomization customization)
    {
        Customization = customization;
    }

    /// <summary>
    /// Gets the customization data for this resource.
    /// </summary>
    public RadiusResourceCustomization Customization { get; }
}

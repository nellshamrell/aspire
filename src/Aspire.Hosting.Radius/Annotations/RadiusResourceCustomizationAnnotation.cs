// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Annotation that holds Radius-specific customization data on a resource,
/// applied via <c>PublishAsRadiusResource()</c>.
/// </summary>
internal sealed class RadiusResourceCustomizationAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Gets the customization configuration for this resource.
    /// </summary>
    public RadiusResourceCustomization Customization { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusResourceCustomizationAnnotation"/> class.
    /// </summary>
    /// <param name="customization">The customization to attach to the resource.</param>
    public RadiusResourceCustomizationAnnotation(RadiusResourceCustomization customization)
    {
        Customization = customization;
    }
}

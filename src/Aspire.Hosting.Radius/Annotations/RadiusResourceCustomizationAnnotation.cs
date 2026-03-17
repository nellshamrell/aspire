// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Annotation that holds Radius resource customization data on a resource.
/// </summary>
/// <param name="customization">The customization data for this resource.</param>
public class RadiusResourceCustomizationAnnotation(RadiusResourceCustomization customization) : IResourceAnnotation
{
    /// <summary>
    /// Gets the Radius resource customization data.
    /// </summary>
    public RadiusResourceCustomization Customization { get; } = customization;
}

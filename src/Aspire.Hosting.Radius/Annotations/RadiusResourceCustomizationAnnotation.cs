// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Annotation that attaches a <see cref="RadiusResourceCustomization"/> to a resource.
/// Created by <see cref="RadiusEnvironmentExtensions.PublishAsRadiusResource{T}"/>.
/// </summary>
public sealed class RadiusResourceCustomizationAnnotation(RadiusResourceCustomization customization) : IResourceAnnotation
{
    /// <summary>
    /// Gets the customization configuration for this resource.
    /// </summary>
    public RadiusResourceCustomization Customization { get; } = customization;
}

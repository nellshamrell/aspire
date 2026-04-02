// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents the Radius dashboard container used for visualizing the Radius environment
/// topology during <c>aspire run</c>.
/// </summary>
public sealed class RadiusDashboardResource : ContainerResource
{
    /// <summary>
    /// The default container image for the Radius dashboard.
    /// </summary>
    internal const string DefaultImage = "ghcr.io/radius-project/dashboard";

    /// <summary>
    /// The default image tag.
    /// </summary>
    internal const string DefaultTag = "latest";

    /// <summary>
    /// The default port for the Radius dashboard.
    /// </summary>
    internal const int DefaultPort = 7007;

    /// <summary>
    /// The default display name shown in the Aspire dashboard.
    /// </summary>
    internal const string DefaultDisplayName = "Radius Dashboard";

    /// <summary>
    /// Gets the display name for this dashboard resource.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusDashboardResource"/> class.
    /// </summary>
    /// <param name="name">The resource name for this dashboard.</param>
    /// <param name="displayName">The human-readable display name. Defaults to "Radius Dashboard".</param>
    public RadiusDashboardResource(string name, string? displayName = null)
        : base(name)
    {
        DisplayName = displayName ?? DefaultDisplayName;
    }
}

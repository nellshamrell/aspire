// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents the Radius dashboard container resource in the Aspire app model.
/// When enabled, this container provides a web UI for visualizing Radius resources.
/// </summary>
[DebuggerDisplay("Type = {GetType().Name,nq}, Name = {Name}")]
public sealed class RadiusDashboardResource : ContainerResource
{
    /// <summary>
    /// The default container image for the Radius dashboard.
    /// </summary>
    public const string DefaultImage = "ghcr.io/radius-project/dashboard";

    /// <summary>
    /// The default image tag for the Radius dashboard.
    /// </summary>
    public const string DefaultTag = "latest";

    /// <summary>
    /// The default port the Radius dashboard listens on.
    /// </summary>
    public const int DefaultPort = 7007;

    /// <summary>
    /// Initializes a new instance of <see cref="RadiusDashboardResource"/>.
    /// </summary>
    /// <param name="name">The name of the dashboard resource.</param>
    public RadiusDashboardResource(string name) : base(name)
    {
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents the Radius dashboard container that visualizes the application topology and Radius environment state.
/// </summary>
/// <param name="name">The name of the dashboard resource.</param>
public class RadiusDashboardResource(string name) : ContainerResource(name)
{
    /// <summary>
    /// The default container image for the Radius dashboard.
    /// </summary>
    internal const string DefaultImage = "ghcr.io/radius-project/dashboard";

    /// <summary>
    /// The default tag for the Radius dashboard container image.
    /// </summary>
    internal const string DefaultTag = "0.55";

    /// <summary>
    /// The default port exposed by the Radius dashboard.
    /// </summary>
    internal const int DefaultPort = 7007;

    /// <summary>
    /// Gets the primary HTTP endpoint of the Radius dashboard.
    /// </summary>
    public EndpointReference PrimaryEndpoint => new(this, "http");
}

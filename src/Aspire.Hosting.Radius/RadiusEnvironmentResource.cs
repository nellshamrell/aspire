// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment resource that can host application resources
/// using Radius portable resource types and Kubernetes-based deployment.
/// </summary>
public sealed class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Gets or sets the name of the Radius environment (used in Bicep output).
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// Gets or sets the Kubernetes namespace for resource deployment.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets whether the Radius dashboard container should be started during <c>aspire run</c>.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the endpoint reference to the Radius dashboard, populated when the dashboard container is created.
    /// </summary>
    public EndpointReference? DashboardEndpoint { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The resource name for this Radius environment.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
        EnvironmentName = name;
    }
}

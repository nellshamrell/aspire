// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002 // IComputeEnvironmentResource is experimental

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment in the Aspire app model.
/// </summary>
public class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Gets or sets the Kubernetes namespace for resource deployment.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets whether the Radius dashboard container should be started during <c>aspire run</c>.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the endpoint reference for the Radius dashboard, if one is created.
    /// </summary>
    public EndpointReference? DashboardEndpoint { get; internal set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The name of the Radius environment.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
    }

    /// <inheritdoc />
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        // Radius uses Kubernetes DNS for inter-service communication:
        // {resourceName}.{namespace}.svc.cluster.local
        return ReferenceExpression.Create($"{endpointReference.Resource.Name.ToLowerInvariant()}");
    }
}

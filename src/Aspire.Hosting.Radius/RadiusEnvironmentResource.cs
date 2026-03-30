// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Radius compute environment in the Aspire app model.
/// Serves as the central configuration point for all Radius integration.
/// </summary>
[DebuggerDisplay("Type = {GetType().Name,nq}, Name = {Name}")]
public sealed class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Initializes a new instance of <see cref="RadiusEnvironmentResource"/>.
    /// </summary>
    /// <param name="name">The name of the Radius environment resource.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the Kubernetes namespace for resource deployment.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets whether to start the Radius dashboard container during <c>aspire run</c>.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the endpoint reference for the Radius dashboard, populated when the dashboard container is created.
    /// </summary>
    public EndpointReference? DashboardEndpoint { get; set; }

    /// <inheritdoc />
    [Experimental("ASPIRECOMPUTE002")]
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        // Use Kubernetes DNS naming convention for service discovery
        return ReferenceExpression.Create($"{endpointReference.Resource.Name}.svc.cluster.local");
    }
}

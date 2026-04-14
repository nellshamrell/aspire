// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment in the Aspire app model.
/// </summary>
public class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The name of the Radius environment resource.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
    }

    /// <summary>
    /// Gets or sets the Kubernetes namespace for resource deployment.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <inheritdoc/>
    [Experimental("ASPIRECOMPUTE002", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        var resource = endpointReference.Resource;
        return ReferenceExpression.Create($"{resource.Name}.svc.cluster.local");
    }
}

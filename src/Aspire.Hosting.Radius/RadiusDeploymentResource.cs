// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius deployment target for a compute resource.
/// This resource is created by <see cref="RadiusInfrastructure"/> during the
/// <see cref="BeforeStartEvent"/> to serve as the deployment target in
/// <see cref="DeploymentTargetAnnotation"/>.
/// </summary>
internal sealed class RadiusDeploymentResource(string name, IResource sourceResource, RadiusEnvironmentResource radiusEnvironment)
    : Resource(name), IResourceWithParent<RadiusEnvironmentResource>
{
    /// <summary>
    /// Gets the Radius environment that this deployment resource belongs to.
    /// </summary>
    public RadiusEnvironmentResource Parent => radiusEnvironment;

    /// <summary>
    /// Gets the original Aspire resource that this deployment resource represents.
    /// </summary>
    internal IResource SourceResource => sourceResource;
}

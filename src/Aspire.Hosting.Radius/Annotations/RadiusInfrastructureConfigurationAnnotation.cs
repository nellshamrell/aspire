// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Provisioning;

namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Annotation that stores a <c>ConfigureRadiusInfrastructure</c> callback on a
/// <see cref="RadiusEnvironmentResource"/>.
/// </summary>
internal sealed class RadiusInfrastructureConfigurationAnnotation : IResourceAnnotation
{
    public RadiusInfrastructureConfigurationAnnotation(Action<RadiusInfrastructureOptions> configure)
    {
        Configure = configure;
    }

    public Action<RadiusInfrastructureOptions> Configure { get; }
}

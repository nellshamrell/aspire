// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Annotation that holds the user's infrastructure customization callback,
/// applied via <c>ConfigureRadiusInfrastructure()</c>.
/// </summary>
internal sealed class RadiusInfrastructureConfigurationAnnotation : IResourceAnnotation
{
    public Action<RadiusInfrastructureOptions> Configure { get; }

    public RadiusInfrastructureConfigurationAnnotation(Action<RadiusInfrastructureOptions> configure)
    {
        Configure = configure;
    }
}

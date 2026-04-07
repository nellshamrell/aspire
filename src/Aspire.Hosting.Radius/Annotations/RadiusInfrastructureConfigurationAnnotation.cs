// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Provisioning;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Stores a <c>ConfigureRadiusInfrastructure</c> callback on a <see cref="RadiusEnvironmentResource"/>.
/// </summary>
internal sealed class RadiusInfrastructureConfigurationAnnotation(Action<RadiusInfrastructureOptions> configure) : IResourceAnnotation
{
    /// <summary>
    /// Gets the configuration callback.
    /// </summary>
    public Action<RadiusInfrastructureOptions> Configure { get; } = configure ?? throw new ArgumentNullException(nameof(configure));
}

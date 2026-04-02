// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Options exposed to users via <c>ConfigureRadiusInfrastructure()</c>
/// to customize the Radius Bicep generation before compilation.
/// </summary>
public sealed class RadiusInfrastructureOptions
{
    /// <summary>
    /// Gets or sets the environment name emitted in the Bicep template.
    /// </summary>
    public string EnvironmentName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the application name emitted in the Bicep template.
    /// </summary>
    public string ApplicationName { get; set; } = default!;

    /// <summary>
    /// Gets or sets the Kubernetes namespace for the environment compute block.
    /// </summary>
    public string Namespace { get; set; } = default!;

    /// <summary>
    /// Gets the classified portable resources. Users can add, remove, or modify entries.
    /// </summary>
    public IList<ClassifiedResource> PortableResources { get; internal set; } = default!;

    /// <summary>
    /// Gets the classified container resources. Users can add, remove, or modify entries.
    /// </summary>
    public IList<ClassifiedResource> ContainerResources { get; internal set; } = default!;
}

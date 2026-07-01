// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Per-resource annotation that records the Radius resource group a resource
/// (compute, backing, or a <see cref="RadiusEnvironmentResource"/>) is routed
/// into via <c>WithRadiusResourceGroup(...)</c>, plus an optional
/// environment-group override for the group whose environment the resource
/// deploys against.
/// </summary>
/// <remarks>
/// The annotation is <b>last-write-wins</b>: a resource resolves to exactly one
/// group (FR-003), so <c>WithRadiusResourceGroup</c> replaces any existing
/// annotation rather than accumulating. It is per-resource and never shared
/// between resources, mirroring <c>RadiusCloudProvidersAnnotation</c>.
/// </remarks>
internal sealed class RadiusResourceGroupAnnotation : IResourceAnnotation
{
    internal RadiusResourceGroupAnnotation(string group, string? environmentGroup = null)
    {
        Group = group;
        EnvironmentGroup = environmentGroup;
    }

    /// <summary>The Radius resource-group name this resource is routed into.</summary>
    public string Group { get; }

    /// <summary>
    /// The group whose environment this resource deploys against, when it differs
    /// from <see cref="Group"/>. <see langword="null"/> ⇒ defaults to <see cref="Group"/>
    /// (FR-002).
    /// </summary>
    public string? EnvironmentGroup { get; }

    /// <summary>
    /// The effective environment-group this resource deploys against
    /// (<see cref="EnvironmentGroup"/> when set, otherwise <see cref="Group"/>).
    /// </summary>
    public string EffectiveEnvironmentGroup => EnvironmentGroup ?? Group;

    /// <summary>
    /// Replaces any existing <see cref="RadiusResourceGroupAnnotation"/> on
    /// <paramref name="resource"/> with a new annotation carrying
    /// <paramref name="group"/> / <paramref name="environmentGroup"/> (last-write-wins).
    /// </summary>
    internal static void Set(IResource resource, string group, string? environmentGroup)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var existing = resource.Annotations.OfType<RadiusResourceGroupAnnotation>().ToList();
        foreach (var stale in existing)
        {
            resource.Annotations.Remove(stale);
        }

        resource.Annotations.Add(new RadiusResourceGroupAnnotation(group, environmentGroup));
    }
}

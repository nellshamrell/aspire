// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Per-environment carrier of cloud-managed selections set via
/// <c>WithManagedResource</c>. Attached to the <see cref="RadiusEnvironmentResource"/>;
/// the publisher consumes it to bind each marked resource's Radius type to the
/// selection's cloud-targeting recipe instead of the default local-dev recipe.
/// </summary>
/// <remarks>
/// Mirrors feature <c>003</c>'s <see cref="RadiusCloudProvidersAnnotation"/> shape
/// and <c>GetOrAdd</c> pattern. The annotation is created lazily on first
/// <c>WithManagedResource</c> call, is never shared between environments
/// (per-environment scope, FR-006), and is absent by default so apps that never
/// mark a resource cloud-managed emit byte-identical Bicep (FR-015).
/// </remarks>
internal sealed class RadiusManagedResourcesAnnotation : IResourceAnnotation
{
    /// <summary>
    /// Cloud-managed selections keyed by target resource name. At most one
    /// selection per resource per environment (last-write-wins, FR-006/FR-007).
    /// </summary>
    public IDictionary<string, ManagedResourceSelection> Selections { get; }
        = new Dictionary<string, ManagedResourceSelection>(StringComparer.Ordinal);

    /// <summary>
    /// Returns the singleton <see cref="RadiusManagedResourcesAnnotation"/> on
    /// <paramref name="environment"/>, creating and attaching one if absent.
    /// </summary>
    internal static RadiusManagedResourcesAnnotation GetOrAdd(IResource environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var existing = environment.Annotations.OfType<RadiusManagedResourcesAnnotation>().FirstOrDefault();
        if (existing is not null)
        {
            return existing;
        }

        var created = new RadiusManagedResourcesAnnotation();
        environment.Annotations.Add(created);
        return created;
    }
}

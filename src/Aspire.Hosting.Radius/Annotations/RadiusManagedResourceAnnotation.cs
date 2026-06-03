// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Declarative marker attached to a backing resource that has been marked
/// cloud-managed via <c>WithManagedResource</c>. Carries the target cloud and
/// recipe location for inner-loop dashboard surfacing (FR-013); it does not
/// change any runtime wiring (<c>aspire run</c> stays in-cluster, FR-012).
/// </summary>
/// <remarks>
/// One annotation per (environment, selection). A resource that is in-cluster in
/// one environment and cloud-managed in another carries a marker only for the
/// environment(s) where it is cloud-managed, so the dashboard can distinguish
/// in-cluster from cloud-managed per environment.
/// </remarks>
internal sealed class RadiusManagedResourceAnnotation : IResourceAnnotation
{
    /// <summary>Initializes a new <see cref="RadiusManagedResourceAnnotation"/>.</summary>
    /// <param name="environmentName">The owning Radius environment's name.</param>
    /// <param name="cloud">The target cloud the resource is materialized on.</param>
    /// <param name="recipeLocation">The cloud-targeting recipe's OCI location, if any.</param>
    public RadiusManagedResourceAnnotation(string environmentName, RadiusCloud cloud, string? recipeLocation)
    {
        EnvironmentName = environmentName;
        Cloud = cloud;
        RecipeLocation = recipeLocation;
    }

    /// <summary>The owning Radius environment's name.</summary>
    public string EnvironmentName { get; }

    /// <summary>The target cloud the resource is materialized on.</summary>
    public RadiusCloud Cloud { get; }

    /// <summary>The cloud-targeting recipe's OCI location, or <see langword="null"/>.</summary>
    public string? RecipeLocation { get; }
}

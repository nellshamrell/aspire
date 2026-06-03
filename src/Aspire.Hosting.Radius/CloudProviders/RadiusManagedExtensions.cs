// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting;

/// <summary>
/// Cloud-managed resource entry points for <see cref="RadiusEnvironmentResource"/>.
/// <c>WithManagedResource</c> marks a backing resource to be materialized as a
/// cloud-managed service (via a recipe on a specific cloud) while compute stays on
/// Kubernetes. The selection is recorded on the environment's
/// <see cref="RadiusManagedResourcesAnnotation"/> and consumed by the publisher to
/// bind the resource's Radius type to the cloud-targeting recipe.
/// </summary>
public static class RadiusManagedExtensions
{
    /// <summary>
    /// Marks <paramref name="resource"/> to be materialized as a cloud-managed
    /// service (via <paramref name="recipe"/> on <paramref name="cloud"/>) when this
    /// Radius environment is published/deployed. Compute is unaffected — only backing
    /// resources may be cloud-managed. The selection is scoped to this environment, so
    /// the same resource can be in-cluster in another environment.
    /// </summary>
    /// <remarks>
    /// Radius binds exactly one recipe per user-defined (<c>Radius.*</c>) resource type per
    /// environment. Marking multiple resources of the same user-defined type with different
    /// recipes (or mixing a cloud-managed instance with a custom in-cluster recipe on the same
    /// type) is unsupported and fails at publish time with <c>ASPIRERADIUS026</c>. Legacy
    /// <c>Applications.*</c> types are exempt because they support multiple named recipes.
    /// </remarks>
    /// <param name="builder">The Radius environment to attach the selection to.</param>
    /// <param name="resource">The backing (non-compute) resource to make cloud-managed.</param>
    /// <param name="cloud">The explicit target cloud (Azure or AWS).</param>
    /// <param name="recipe">The cloud-targeting recipe (<see cref="RadiusRecipe.RecipeLocation"/> required).</param>
    /// <returns>The same environment builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="resource"/>, or <paramref name="recipe"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// The target is a child resource (<c>ASPIRERADIUS024</c>), a compute workload
    /// (<c>ASPIRERADIUS022</c>), or not a supported backing resource (<c>ASPIRERADIUS025</c>);
    /// the recipe has no <see cref="RadiusRecipe.RecipeLocation"/> (<c>ASPIRERADIUS023</c>);
    /// the selected cloud has no provider configured on this environment (<c>ASPIRERADIUS020</c>);
    /// or the recipe's declared cloud conflicts with <paramref name="cloud"/> (<c>ASPIRERADIUS021</c>).
    /// </exception>
    // [AspireExportIgnore]: the open-generic `IResourceBuilder<IResource>` target
    // parameter is part of the public C# API surface but Aspire's ATS exporter
    // (ASPIREEXPORT008) doesn't know how to render it. The export is suppressed only
    // for the ATS catalog; the method remains fully usable from C#.
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithManagedResource(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        IResourceBuilder<IResource> resource,
        RadiusCloud cloud,
        RadiusRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(recipe);

        var environment = builder.Resource;
        var target = resource.Resource;

        // Configuration-time validation (FR-003/FR-004/FR-014). Fails fast with a
        // contract diagnostic ID before any publish/deploy work.
        ManagedValidation.Validate(environment, target, cloud, recipe, nameof(resource));

        // Record/replace the selection (last-write-wins per resource per environment).
        var annotation = RadiusManagedResourcesAnnotation.GetOrAdd(environment);
        annotation.Selections[target.Name] = new ManagedResourceSelection(target, cloud, recipe);

        // Declarative dashboard marker (FR-013). Remove any prior marker for this
        // environment so a repeated call reflects the latest selection.
        var staleMarkers = target.Annotations
            .OfType<RadiusManagedResourceAnnotation>()
            .Where(a => string.Equals(a.EnvironmentName, environment.Name, StringComparison.Ordinal))
            .ToList();
        foreach (var stale in staleMarkers)
        {
            target.Annotations.Remove(stale);
        }
        target.Annotations.Add(new RadiusManagedResourceAnnotation(environment.Name, cloud, recipe.RecipeLocation));

        return builder;
    }

    /// <summary>
    /// Convenience overload of <see cref="WithManagedResource(IResourceBuilder{RadiusEnvironmentResource}, IResourceBuilder{IResource}, RadiusCloud, RadiusRecipe)"/>
    /// that constructs a <see cref="RadiusRecipe"/> from <paramref name="recipeLocation"/>.
    /// </summary>
    /// <param name="builder">The Radius environment to attach the selection to.</param>
    /// <param name="resource">The backing (non-compute) resource to make cloud-managed.</param>
    /// <param name="cloud">The explicit target cloud (Azure or AWS).</param>
    /// <param name="recipeLocation">The OCI location of the cloud-targeting recipe.</param>
    /// <returns>The same environment builder for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="resource"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="recipeLocation"/> is null/empty, or a validation rule fails (see the primary overload).</exception>
    // [AspireExportIgnore]: see the primary overload — the `IResourceBuilder<IResource>`
    // target parameter is not ATS-renderable (ASPIREEXPORT008).
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithManagedResource(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        IResourceBuilder<IResource> resource,
        RadiusCloud cloud,
        string recipeLocation)
    {
        ArgumentException.ThrowIfNullOrEmpty(recipeLocation);
        return builder.WithManagedResource(resource, cloud, new RadiusRecipe { RecipeLocation = recipeLocation });
    }
}

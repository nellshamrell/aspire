// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.CloudProviders;

/// <summary>
/// One per (environment, target-resource) marking that a backing resource should
/// be materialized as a cloud-managed service. Immutable value object created by
/// <c>RadiusManagedExtensions.WithManagedResource</c> and stored on the
/// environment's <c>RadiusManagedResourcesAnnotation</c>, keyed by the target
/// resource name (so the target identity lives in the dictionary key).
/// </summary>
/// <param name="Cloud">
/// The explicit target cloud (Azure or AWS). Consumed at publish time to verify the
/// matching cloud provider is configured on the environment (<c>ASPIRERADIUS020</c>).
/// </param>
/// <param name="Recipe">
/// The cloud-targeting recipe (reuses feature <c>001</c>'s
/// <see cref="RadiusRecipe"/>); <see cref="RadiusRecipe.RecipeLocation"/> points
/// at the cloud-targeting recipe, with optional
/// <see cref="RadiusRecipe.Parameters"/>.
/// </param>
internal sealed record ManagedResourceSelection(
    RadiusCloud Cloud,
    RadiusRecipe Recipe);

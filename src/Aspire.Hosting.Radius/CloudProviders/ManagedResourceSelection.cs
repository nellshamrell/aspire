// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.CloudProviders;

/// <summary>
/// One per (environment, target-resource) marking that a backing resource should
/// be materialized as a cloud-managed service. Immutable value object created by
/// <c>RadiusManagedExtensions.WithManagedResource</c> and stored on the
/// environment's <c>RadiusManagedResourcesAnnotation</c>.
/// </summary>
/// <param name="TargetResource">
/// The backing (non-compute) resource being made cloud-managed. Identity is the
/// resource name.
/// </param>
/// <param name="Cloud">The explicit target cloud (Azure or AWS).</param>
/// <param name="Recipe">
/// The cloud-targeting recipe (reuses feature <c>001</c>'s
/// <see cref="RadiusRecipe"/>); <see cref="RadiusRecipe.RecipeLocation"/> points
/// at the cloud-targeting recipe, with optional
/// <see cref="RadiusRecipe.Parameters"/>.
/// </param>
internal sealed record ManagedResourceSelection(
    IResource TargetResource,
    RadiusCloud Cloud,
    RadiusRecipe Recipe);

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.ResourceMapping;

namespace Aspire.Hosting.Radius.CloudProviders;

/// <summary>
/// Configuration-time validators for cloud-managed resource selections
/// (<c>WithManagedResource</c>). Each rule throws <see cref="ArgumentException"/>
/// with the contract diagnostic ID embedded in the message so failures surface
/// early (before publish/deploy) with a clear, actionable explanation.
/// </summary>
/// <remarks>
/// These rules are pure functions of the single <c>(target, recipe)</c> input, so they
/// can run eagerly when <c>WithManagedResource</c> is called regardless of builder call
/// order. Cross-resource invariants that depend on other mutable environment state — the
/// matching cloud provider being configured (<c>ASPIRERADIUS020</c>) and same-type recipe
/// divergence (<c>ASPIRERADIUS026</c>) — are validated at publish time in
/// <c>RadiusInfrastructureBuilder</c> instead, where the environment's final state is known.
/// </remarks>
internal static class ManagedValidation
{
    /// <summary>
    /// Runs every configuration-time rule for a selection: not-a-child
    /// (<c>ASPIRERADIUS024</c>), not-compute (<c>ASPIRERADIUS022</c>), supported backing
    /// type (<c>ASPIRERADIUS025</c>), and recipe-location present (<c>ASPIRERADIUS023</c>).
    /// </summary>
    /// <param name="target">The resource being marked cloud-managed.</param>
    /// <param name="recipe">The cloud-targeting recipe.</param>
    /// <param name="paramName">The parameter name to attribute failures to.</param>
    internal static void Validate(
        IResource target,
        RadiusRecipe recipe,
        string paramName)
    {
        ValidateNotChild(target, paramName);
        ValidateNotCompute(target, paramName);
        ValidateSupportedBackingResource(target, paramName);
        ValidateRecipeLocation(target, recipe, paramName);
    }

    /// <summary>
    /// <c>ASPIRERADIUS024</c>: a child resource (e.g. a database on a server) cannot be
    /// marked cloud-managed directly; publishing represents it via its parent, so the
    /// selection must be applied to the parent resource instead.
    /// </summary>
    internal static void ValidateNotChild(IResource target, string paramName)
    {
        if (target is IResourceWithParent child)
        {
            throw new ArgumentException(
                $"Resource '{target.Name}' is a child of '{child.Parent.Name}' and cannot be marked " +
                "cloud-managed directly; Radius materializes it through its parent. Call " +
                $"WithManagedResource on the parent resource '{child.Parent.Name}' instead. " +
                "Diagnostic: ASPIRERADIUS024.",
                paramName);
        }
    }

    /// <summary>
    /// <c>ASPIRERADIUS022</c>: only backing resources may be cloud-managed; a
    /// compute workload (project/container) cannot be. A backing resource whose
    /// <c>TypeOverride</c> retargets it to the compute <c>Containers</c> type also
    /// resolves to compute at publish (<c>ResolveResourceType</c> honors the override
    /// first), so reject that here too — otherwise it slips past both this rule and
    /// <c>ASPIRERADIUS025</c> and a compute workload ends up marked cloud-managed.
    /// </summary>
    internal static void ValidateNotCompute(IResource target, string paramName)
    {
        var typeOverride = target.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .LastOrDefault()?.Customization.TypeOverride;
        var overridesToCompute = typeOverride is not null
            && string.Equals(typeOverride.Type, RadiusResourceTypes.Containers, StringComparison.Ordinal);

        if (IsComputeWorkload(target) || overridesToCompute)
        {
            throw new ArgumentException(
                $"Resource '{target.Name}' is a compute workload (project/container) and cannot be " +
                "marked cloud-managed. Only backing resources (e.g. databases, caches, queues) may be " +
                "cloud-managed; compute always runs as a Radius.Compute/containers workload on Kubernetes. " +
                "Diagnostic: ASPIRERADIUS022.",
                paramName);
        }
    }

    /// <summary>
    /// <c>ASPIRERADIUS025</c>: the target must map to a known non-compute Radius backing
    /// resource type. Resources with no Radius mapping (e.g. parameters) would otherwise be
    /// accepted here and then silently skipped or misclassified during publish.
    /// </summary>
    internal static void ValidateSupportedBackingResource(IResource target, string paramName)
    {
        // A custom TypeOverride wins over the built-in mapping during publishing
        // (RadiusInfrastructureBuilder.ResolveResourceType checks it before the mapper), so
        // honor it here too: an override to any non-compute type is a valid backing resource.
        // Without this, a resource that only maps via an override would pass at publish time
        // but be falsely rejected at configuration time.
        var typeOverride = target.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .LastOrDefault()?.Customization.TypeOverride;
        var hasNonComputeOverride = typeOverride is not null
            && !string.Equals(typeOverride.Type, RadiusResourceTypes.Containers, StringComparison.Ordinal);

        if (!hasNonComputeOverride && !ResourceTypeMapper.IsBackingResource(target))
        {
            throw new ArgumentException(
                $"Resource '{target.Name}' (type '{target.GetType().Name}') does not map to a Radius " +
                "backing resource type and cannot be marked cloud-managed. Only supported backing " +
                "resources (e.g. Redis, SQL Server, PostgreSQL, MongoDB, RabbitMQ) may be cloud-managed. " +
                "Diagnostic: ASPIRERADIUS025.",
                paramName);
        }
    }

    /// <summary>
    /// <c>ASPIRERADIUS023</c>: a cloud-managed selection requires a non-empty recipe
    /// location; without one the resource would silently fall back to the in-cluster
    /// default recipe while still being reported as cloud-managed.
    /// </summary>
    internal static void ValidateRecipeLocation(IResource target, RadiusRecipe recipe, string paramName)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeLocation))
        {
            throw new ArgumentException(
                $"Resource '{target.Name}' is marked cloud-managed but its recipe has no RecipeLocation. " +
                "A cloud-managed recipe must specify the OCI location of the cloud-targeting recipe " +
                "(RadiusRecipe.RecipeLocation). Diagnostic: ASPIRERADIUS023.",
                paramName);
        }
    }

    // A compute workload is a project, or a plain container added via AddContainer.
    // Backing resources (Redis, SQL, Postgres, Mongo, RabbitMQ) also derive from
    // ContainerResource for inner-loop hosting, but they expose a connection string
    // and map to a Radius backing resource type — so they are NOT compute.
    private static bool IsComputeWorkload(IResource resource)
        => resource is ProjectResource
            || (resource is ContainerResource && resource is not IResourceWithConnectionString);
}

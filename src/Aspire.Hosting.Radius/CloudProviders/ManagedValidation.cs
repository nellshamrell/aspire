// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;

namespace Aspire.Hosting.Radius.CloudProviders;

/// <summary>
/// Configuration-time validators for cloud-managed resource selections
/// (<c>WithManagedResource</c>). Each rule throws <see cref="ArgumentException"/>
/// with the contract diagnostic ID embedded in the message so failures surface
/// early (before publish/deploy) with a clear, actionable explanation.
/// </summary>
internal static class ManagedValidation
{
    /// <summary>
    /// Runs every configuration-time rule for a selection: not-compute
    /// (<c>ASPIRERADIUS022</c>), provider-configured (<c>ASPIRERADIUS020</c>),
    /// and cloud/recipe match (<c>ASPIRERADIUS021</c>).
    /// </summary>
    /// <param name="environment">The owning Radius environment resource.</param>
    /// <param name="target">The resource being marked cloud-managed.</param>
    /// <param name="cloud">The explicit target cloud.</param>
    /// <param name="recipe">The cloud-targeting recipe.</param>
    /// <param name="paramName">The parameter name to attribute failures to.</param>
    internal static void Validate(
        IResource environment,
        IResource target,
        RadiusCloud cloud,
        RadiusRecipe recipe,
        string paramName)
    {
        ValidateNotCompute(target, paramName);
        ValidateProviderConfigured(environment, target, cloud, paramName);
        ValidateCloudRecipeMatch(target, cloud, recipe, paramName);
    }

    /// <summary>
    /// <c>ASPIRERADIUS022</c>: only backing resources may be cloud-managed; a
    /// compute workload (project/container) cannot be.
    /// </summary>
    internal static void ValidateNotCompute(IResource target, string paramName)
    {
        if (IsComputeWorkload(target))
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
    /// <c>ASPIRERADIUS020</c>: the selected cloud must have a matching provider
    /// configured on the same environment (feature <c>003</c>
    /// <c>WithAzureProvider</c>/<c>WithAwsProvider</c>).
    /// </summary>
    internal static void ValidateProviderConfigured(
        IResource environment,
        IResource target,
        RadiusCloud cloud,
        string paramName)
    {
        var providers = environment.Annotations
            .OfType<RadiusCloudProvidersAnnotation>()
            .FirstOrDefault();

        var configured = cloud switch
        {
            RadiusCloud.Azure => providers?.Azure is not null,
            RadiusCloud.Aws => providers?.Aws is not null,
            _ => false,
        };

        if (!configured)
        {
            var providerCall = cloud == RadiusCloud.Azure ? "WithAzureProvider(...)" : "WithAwsProvider(...)";
            throw new ArgumentException(
                $"Resource '{target.Name}' is marked cloud-managed for {cloud}, but no {cloud} provider is " +
                $"configured on this Radius environment. Call {providerCall} on the environment before " +
                $"marking resources cloud-managed for {cloud}. Diagnostic: ASPIRERADIUS020.",
                paramName);
        }
    }

    /// <summary>
    /// <c>ASPIRERADIUS021</c>: when the recipe location clearly declares a cloud,
    /// it must match the explicitly selected <paramref name="cloud"/>.
    /// </summary>
    internal static void ValidateCloudRecipeMatch(
        IResource target,
        RadiusCloud cloud,
        RadiusRecipe recipe,
        string paramName)
    {
        var declared = InferRecipeCloud(recipe.RecipeLocation);
        if (declared is { } recipeCloud && recipeCloud != cloud)
        {
            throw new ArgumentException(
                $"Resource '{target.Name}' is marked cloud-managed for {cloud}, but its recipe " +
                $"'{recipe.RecipeLocation}' appears to target {recipeCloud}. The explicit cloud and the " +
                "recipe's declared cloud must match. Diagnostic: ASPIRERADIUS021.",
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

    /// <summary>
    /// Best-effort inference of the cloud a recipe targets from its OCI location.
    /// Returns <see langword="null"/> when the location declares no recognizable
    /// cloud token (cloud-agnostic recipe → no conflict).
    /// </summary>
    private static RadiusCloud? InferRecipeCloud(string? recipeLocation)
    {
        if (string.IsNullOrEmpty(recipeLocation))
        {
            return null;
        }

        var hasAzure = recipeLocation.Contains("azure", StringComparison.OrdinalIgnoreCase);
        var hasAws = recipeLocation.Contains("aws", StringComparison.OrdinalIgnoreCase);

        // Only infer when exactly one cloud token is present; an ambiguous
        // location (both or neither) declares no single cloud.
        if (hasAzure && !hasAws)
        {
            return RadiusCloud.Azure;
        }

        if (hasAws && !hasAzure)
        {
            return RadiusCloud.Aws;
        }

        return null;
    }
}

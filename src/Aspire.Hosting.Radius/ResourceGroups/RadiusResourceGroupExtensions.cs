// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Annotations;

namespace Aspire.Hosting;

/// <summary>
/// Routing entry points that assign a resource (compute, backing, or a
/// <see cref="RadiusEnvironmentResource"/>) to a Radius resource group. The
/// integration partitions <c>aspire publish</c> / <c>aspire deploy</c> output
/// by group and orchestrates a dependency-ordered deploy across groups.
/// </summary>
public static class RadiusResourceGroupExtensions
{
    /// <summary>
    /// Routes <paramref name="builder"/>'s resource into the named Radius resource
    /// group. The resource's target environment defaults to the same group. Repeated
    /// calls replace the assignment (a resource resolves to exactly one group).
    /// </summary>
    /// <ats-summary>Routes a resource into a named Radius resource group (target environment defaults to the same group).</ats-summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder to route.</param>
    /// <param name="group">The Radius resource-group name. Must be non-empty/non-whitespace.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="group"/> is empty or whitespace (<c>ASPIRERADIUS033</c>).
    /// </exception>
    [Experimental("ASPIRERADIUS005", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExport]
    public static IResourceBuilder<T> WithRadiusResourceGroup<T>(
        this IResourceBuilder<T> builder,
        string group)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateGroupName(group, nameof(group));

        RadiusResourceGroupAnnotation.Set(builder.Resource, group, environmentGroup: null);
        return builder;
    }

    /// <summary>
    /// Routes <paramref name="builder"/>'s resource into <paramref name="group"/> while
    /// it deploys against an environment in <paramref name="environmentGroup"/>
    /// (a cross-group environment target). The published <c>properties.environment</c>
    /// is emitted as the environment's full UCP ID (FR-005).
    /// </summary>
    /// <ats-summary>Routes a resource into one group while it deploys against another group's environment (cross-group environment target).</ats-summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder to route.</param>
    /// <param name="group">The Radius resource-group name the resource lands in.</param>
    /// <param name="environmentGroup">The group whose environment the resource deploys against.</param>
    /// <returns>The same builder for chaining.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="group"/> or <paramref name="environmentGroup"/> is empty/whitespace
    /// (<c>ASPIRERADIUS033</c>), or the two-argument overload is used on a
    /// <see cref="RadiusEnvironmentResource"/> builder (an environment does not deploy
    /// against another environment).
    /// </exception>
    [Experimental("ASPIRERADIUS005", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExport("withRadiusResourceGroupEnvironment")]
    public static IResourceBuilder<T> WithRadiusResourceGroup<T>(
        this IResourceBuilder<T> builder,
        string group,
        string environmentGroup)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ValidateGroupName(group, nameof(group));
        ValidateGroupName(environmentGroup, nameof(environmentGroup));

        if (builder.Resource is RadiusEnvironmentResource)
        {
            throw new ArgumentException(
                "The environment-group overload of WithRadiusResourceGroup cannot be used on a " +
                "RadiusEnvironmentResource: an environment does not deploy against another environment. " +
                "Route the environment into its own group with the single-argument overload.",
                nameof(environmentGroup));
        }

        RadiusResourceGroupAnnotation.Set(builder.Resource, group, environmentGroup);
        return builder;
    }

    private static void ValidateGroupName(string group, string paramName)
    {
        if (string.IsNullOrWhiteSpace(group))
        {
            throw new ArgumentException(
                $"A Radius resource-group name must be non-empty and non-whitespace. Diagnostic: ASPIRERADIUS033.",
                paramName);
        }
    }
}

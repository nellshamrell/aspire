// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Azure.Provisioning;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Exposes strongly-typed mutable collections of Radius construct classes for
/// AST customization via the <c>ConfigureRadiusInfrastructure</c> callback.
/// </summary>
/// <remarks>
/// <b>Preview surface.</b> The construct shapes exposed here mirror the still-
/// evolving Radius preview schemas and are expected to change as those schemas
/// stabilize. Treat <c>ConfigureRadiusInfrastructure</c> and the construct types
/// referenced by this options bag as an advanced escape hatch that may shift
/// shape across releases of <c>Aspire.Hosting.Radius</c>.
/// </remarks>
[Experimental("ASPIRERADIUS004", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public sealed class RadiusInfrastructureOptions
{
    /// <summary>
    /// Gets the list of <c>Radius.Core/environments</c> constructs.
    /// </summary>
    public List<RadiusEnvironmentConstruct> Environments { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Core/applications</c> constructs.
    /// </summary>
    public List<RadiusApplicationConstruct> Applications { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Core/recipePacks</c> constructs.
    /// </summary>
    public List<RadiusRecipePackConstruct> RecipePacks { get; } = [];

    /// <summary>
    /// Gets the list of resource type instance constructs
    /// (e.g., <c>Radius.Data/redisCaches</c>, <c>Radius.Messaging/rabbitMQQueues</c>).
    /// </summary>
    public List<RadiusResourceTypeConstruct> ResourceTypeInstances { get; } = [];

    /// <summary>
    /// Gets the list of <c>Radius.Compute/containers</c> workload constructs.
    /// </summary>
    public List<RadiusContainerConstruct> Containers { get; } = [];

    /// <summary>
    /// Gets the list of legacy <c>Applications.Core/environments</c> constructs
    /// emitted when one or more targeted resources fall back to a legacy
    /// <c>Applications.*</c> type (e.g., Redis, Mongo, RabbitMQ, Dapr).
    /// </summary>
    public List<LegacyApplicationEnvironmentConstruct> LegacyEnvironments { get; } = [];

    /// <summary>
    /// Gets the list of legacy <c>Applications.Core/applications</c> constructs
    /// paired with <see cref="LegacyEnvironments"/>.
    /// </summary>
    public List<LegacyApplicationConstruct> LegacyApplications { get; } = [];

    /// <summary>
    /// Gets the list of legacy <c>Applications.Core/containers</c> workload
    /// constructs. Populated instead of <see cref="Containers"/> when the
    /// environment opts in via <c>WithLegacyContainers()</c>. Used as a
    /// fallback when the <c>Radius.Compute/containers</c> UDT has no recipe
    /// registered in the target Radius environment.
    /// </summary>
    public List<LegacyContainerConstruct> LegacyContainers { get; } = [];

    /// <summary>
    /// Gets the list of <c>Applications.Core/secretStores</c> constructs emitted for the
    /// Radius secret stores declared via <c>AddRadiusSecretStore</c> / <c>WithSecretStore</c>.
    /// </summary>
    public List<RadiusSecretStoreConstruct> SecretStores { get; } = [];

    /// <summary>
    /// Gets the file paths of committed <c>SealedSecret</c> manifests referenced by sealed
    /// secret stores in this scope. The publish step copies each alongside the emitted
    /// <c>app.bicep</c> so the artifact is self-contained (the manifest is already encrypted).
    /// </summary>
    internal List<string> SealedSecretManifestPaths { get; } = [];

    /// <summary>
    /// Gets the Bicep <c>param</c> declarations referenced by recipe parameters that
    /// are bound to an Aspire <c>ParameterResource</c>. Keyed by parameter name so a
    /// parameter referenced by multiple recipe entries is declared once. These are
    /// added to the generated infrastructure; secret-bound parameters are marked secure
    /// so no value is written to the published file.
    /// </summary>
    internal Dictionary<string, ProvisioningParameter> RecipeParameters { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the mapping from emitted Bicep parameter identifier to the originating Aspire
    /// <see cref="ParameterResource"/>. The deploy step uses this to resolve a value for every
    /// valueless <c>param</c> the build emits and forward it via <c>rad deploy --parameters</c>.
    /// </summary>
    internal Dictionary<string, ParameterResource> RecipeParameterBindings { get; } = new(StringComparer.Ordinal);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

#pragma warning disable ASPIRERADIUS002 // RadiusRecipe.Name signals upstream deprecation for callers; internal publisher code still reads it.
#pragma warning disable ASPIRECOMPUTE002 // GetEndpointPropertyExpression/GetHostAddressExpression are experimental compute-environment APIs the publisher relies on.

using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Radius.ResourceMapping;
using Azure.Provisioning;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Primitives;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Builds an Azure.Provisioning Infrastructure AST from a <see cref="DistributedApplicationModel"/>
/// for a specific Radius environment. Generates typed <c>ProvisionableResource</c> constructs
/// (environments, applications, recipe packs, resource type instances, containers) that are
/// compiled to Bicep via <c>Infrastructure.Build().Compile()</c>.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    private readonly RadiusEnvironmentResource _environment;
    private readonly DistributedApplicationModel _model;
    private readonly ResourceTypeMapper _typeMapper;
    private readonly ILogger _logger;

    /// <summary>
    /// When non-<see langword="null"/>, restricts the build to the resources routed to a single
    /// Radius resource group and carries the whole-model group map used to render cross-group
    /// <c>WithReference</c> / environment targets as full UCP IDs (FR-004, FR-005). In the grouped
    /// publish path each <c>groups/&lt;group&gt;/app.bicep</c> is produced by a builder scoped to
    /// that group's resources, materializing the single logical application once per group (FR-007).
    /// When <see langword="null"/> the builder uses the default per-environment deployment-target
    /// scoping (the no-group path, unchanged).
    /// </summary>
    private readonly RadiusGroupContext? _groupContext;

    /// <summary>
    /// Bicep <c>param</c> declarations accumulated for recipe parameter values bound to
    /// an Aspire <see cref="ParameterResource"/>. Keyed by parameter name so each is
    /// declared once; merged into the options bag at the end of <see cref="BuildAsync"/>.
    /// </summary>
    private readonly Dictionary<string, ProvisioningParameter> _recipeParameters = new(StringComparer.Ordinal);

    /// <summary>
    /// Maps each emitted Bicep parameter identifier to the originating Aspire
    /// <see cref="ParameterResource"/>. The deploy step uses this to resolve and forward a
    /// value for every valueless <c>param</c> the build emits (recipe-parameter bindings and
    /// secret/parameter-backed container env vars) via <c>rad deploy --parameters</c>.
    /// </summary>
    private readonly Dictionary<string, ParameterResource> _recipeParameterBindings = new(StringComparer.Ordinal);

    /// <summary>
    /// Publish-mode execution context used to resolve container environment variables and
    /// service-discovery values. Set at the start of <see cref="BuildAsync"/>.
    /// </summary>
    private DistributedApplicationExecutionContext _executionContext = null!;
    private CancellationToken _cancellationToken;

    /// <summary>
    /// Maps each sanitized Bicep identifier to the originating Aspire parameter name, so two
    /// distinct parameter names that sanitize to the same identifier are detected and rejected
    /// (ASPIRERADIUS028) instead of emitting duplicate <c>param</c> declarations.
    /// </summary>
    private readonly Dictionary<string, string> _recipeParameterIdentifiers = new(StringComparer.Ordinal);

    /// <summary>
    /// Default recipe template paths per resource type.
    /// </summary>
    private static readonly Dictionary<string, string> s_defaultRecipeTemplates = new(StringComparer.Ordinal)
    {
        [RadiusResourceTypes.RedisCaches] = "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest",
        [RadiusResourceTypes.SqlDatabases] = "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest",
        [RadiusResourceTypes.PostgreSqlDatabases] = "ghcr.io/radius-project/recipes/local-dev/postgresqldatabases:latest",
        [RadiusResourceTypes.MongoDatabases] = "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest",
        [RadiusResourceTypes.RabbitMQQueues] = "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest",
        // The Radius.Compute/containers UDT needs a recipe registered in the env's recipe pack;
        // shipped Radius does not include one by default, so register the published container
        // recipe so native containers deploy without a manually-authored recipe.
        [RadiusResourceTypes.Containers] = "ghcr.io/radius-project/kube-recipes/containers:latest",
        // Legacy fallback types also get default recipes
        [RadiusResourceTypes.LegacyRedisCaches] = "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest",
        [RadiusResourceTypes.LegacyMongoDatabases] = "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest",
        [RadiusResourceTypes.LegacyRabbitMQQueues] = "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest",
        [RadiusResourceTypes.LegacyDaprStateStores] = "ghcr.io/radius-project/recipes/local-dev/daprstatestores:latest",
        [RadiusResourceTypes.LegacyDaprPubSubBrokers] = "ghcr.io/radius-project/recipes/local-dev/daprpubsubbrokers:latest",
    };

    internal RadiusInfrastructureBuilder(
        RadiusEnvironmentResource environment,
        DistributedApplicationModel model,
        ResourceTypeMapper typeMapper,
        ILogger logger,
        RadiusGroupContext? groupContext = null)
    {
        _environment = environment;
        _model = model;
        _typeMapper = typeMapper;
        _logger = logger;
        _groupContext = groupContext;
    }

    /// <summary>
    /// Builds the Bicep AST and populates a <see cref="RadiusInfrastructureOptions"/> with
    /// typed constructs. Runs <c>ConfigureRadiusInfrastructure</c> callbacks last (last-write-wins).
    /// </summary>
    /// <param name="executionContext">
    /// Publish-mode execution context used to resolve container environment variables and
    /// service-discovery values from the application model.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the build.</param>
    internal async Task<RadiusInfrastructureOptions> BuildAsync(
        DistributedApplicationExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        _executionContext = executionContext;
        _cancellationToken = cancellationToken;

        var options = new RadiusInfrastructureOptions();
        var envIdentifier = BicepPostProcessor.SanitizeIdentifier(_environment.Name);

        // Classify resources for this environment. ResolveResourceType is computed once per
        // resource here and reused below — calling it repeatedly would re-emit the
        // ResourceTypeMapper Info/Warning logs (legacy fallback / unmapped type) for every
        // resource, producing duplicate noise on every publish.
        var (radiusResources, computeResources, resolvedTypes) = ClassifyResources();

        // Cross-resource invariant: every cloud-managed selection requires the matching cloud
        // provider to be configured on this environment. Validated here (not when
        // WithManagedResource is called) so authoring is insensitive to builder call order —
        // the provider may be configured before or after the selection (ASPIRERADIUS020).
        ValidateManagedSelectionProviders();

        // 1. UDT recipe pack (created first so environment can reference its ID)
        var recipePackIdentifier = "recipepack";
        var udtRecipeEntries = new Dictionary<string, RecipeEntry>(StringComparer.Ordinal);
        var legacyRecipeEntries = new Dictionary<string, Dictionary<string, RecipeEntry>>(StringComparer.Ordinal);
        var typeInstancesByResourceName = new Dictionary<string, RadiusResourceTypeConstruct>(StringComparer.Ordinal);

        // Radius binds one recipe per resource type per environment. Legacy Applications.* types
        // resolve that binding through the legacy environment, which still supports multiple
        // *named* recipes per type, so divergent legacy instances bind per instance (a distinct
        // named recipe) while the type-level entry keeps the in-cluster default. User-defined
        // (Radius.*) types resolve their binding through the shared recipe pack, which maps a
        // type to exactly one recipe: UDT types support neither per-instance recipe overrides
        // nor named recipes, so instances of one UDT type cannot diverge to different recipes.
        // That divergence is rejected for UDT types below (see ASPIRERADIUS026). Compute the set
        // of distinct effective recipe locations per type so the "divergent" case can be detected.
        // A null entry means an in-cluster / default-recipe instance. Both cloud-managed
        // selections and customization recipes count, so a custom in-cluster sibling of a managed
        // resource (or two managed instances with different recipes) is detected (FR-007, INV-5).
        var effectiveLocationsByType = new Dictionary<string, HashSet<string?>>(StringComparer.Ordinal);
        // For native (UDT) types, recipe *parameters* also bind once per type via the shared pack.
        // Two instances with the same recipe location but different intrinsic parameters would
        // last-write-win silently, so track a full signature (location + parameters) per type to
        // detect that parameter divergence too (the location-only set above can't see it).
        var effectiveSignaturesByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var resource in radiusResources)
        {
            var rt = resolvedTypes[resource].ResourceType;
            var effectiveRecipe = GetManagedRecipe(resource) ?? GetCustomization(resource)?.Recipe;
            var location = NormalizeRecipeLocation(effectiveRecipe?.RecipeLocation);
            if (!effectiveLocationsByType.TryGetValue(rt, out var locations))
            {
                locations = new HashSet<string?>(StringComparer.Ordinal);
                effectiveLocationsByType[rt] = locations;
            }
            locations.Add(location);

            if (!effectiveSignaturesByType.TryGetValue(rt, out var signatures))
            {
                signatures = new HashSet<string>(StringComparer.Ordinal);
                effectiveSignaturesByType[rt] = signatures;
            }
            signatures.Add(ComputeRecipeSignature(location, effectiveRecipe));
        }

        // A type is "divergent" when its instances do not all share a single effective recipe.
        bool IsDivergentType(string resourceType)
            => effectiveLocationsByType.TryGetValue(resourceType, out var locations) && locations.Count > 1;

        // Reject divergence on user-defined (Radius.*) types. Radius recipe packs bind exactly one
        // recipe per resource type per environment, and UDT types support neither per-instance
        // recipe overrides nor named recipes. Emitting a `properties.recipe.recipeLocation` per UDT
        // instance is silently ignored by Radius — the instance would deploy with the type's single
        // pack recipe, not its intended one — so a divergent UDT type would deploy incorrectly. The
        // same applies to recipe *parameters*: a single pack entry carries one parameter set per
        // type, so instances diverging only in parameters would last-write-win silently. Both forms
        // of divergence are detected via the full signature (location + parameters). Fail the publish
        // with a clear diagnostic instead. Legacy Applications.* types are exempt because their
        // environment still supports named recipes (see the per-instance legacy binding below).
        // See: https://github.com/radius-project/radius/blob/main/eng/design-notes/extensibility/2025-06-compute-extensibility-feature-spec.md
        // ("only one Recipe per resource type is allowed per Environment") and
        // https://github.com/radius-project/radius/blob/main/eng/design-notes/extensibility/2025-02-user-defined-resource-type-feature-spec.md
        foreach (var (resourceType, signatures) in effectiveSignaturesByType)
        {
            if (signatures.Count > 1 && !IsLegacyResourceType(resourceType))
            {
                var conflicting = radiusResources
                    .Where(r => string.Equals(resolvedTypes[r].ResourceType, resourceType, StringComparison.Ordinal))
                    .Select(r => r.Name);
                throw new InvalidOperationException(
                    $"Resources of Radius type '{resourceType}' ({string.Join(", ", conflicting)}) resolve to " +
                    "different recipes or recipe parameters, but Radius binds exactly one recipe (with one parameter " +
                    "set) per resource type per environment for user-defined types (per-instance recipe overrides and " +
                    "named recipes are not supported). Use the same recipe and parameters (or cloud-managed selection) " +
                    "for all instances of this type, or model them as different resource types. Diagnostic: ASPIRERADIUS026.");
            }
        }

        // Native (Radius.*) types bind exactly one recipe per type per environment and do not
        // support per-instance recipe names or parameters. Reject user-supplied per-instance
        // recipe configuration on native types (including native compute containers) with an
        // actionable diagnostic instead of silently ignoring it (ASPIRERADIUS027).
        RejectPerInstanceRecipeOnNativeTypes(radiusResources, computeResources, resolvedTypes);

        // Collect recipe entries: UDT types go into the recipe pack, legacy
        // Applications.* types are stashed for inline emission on the legacy env.
        foreach (var resource in radiusResources)
        {
            var (resourceType, _) = resolvedTypes[resource];
            var customization = GetCustomization(resource);

            // A cloud-managed selection on this environment overrides the resource's
            // own customization recipe (and the local-dev default) so the type binds
            // to the cloud-targeting recipe (FR-008).
            var managedRecipe = GetManagedRecipe(resource);
            var effectiveRecipe = managedRecipe ?? customization?.Recipe;

            if (IsLegacyResourceType(resourceType))
            {
                // Legacy types can diverge: when this type's instances resolve to different
                // recipes, a nameless effective recipe (e.g. a cloud-managed, location-only
                // recipe) needs a synthetic per-instance recipe name (its resource name) so it
                // doesn't collapse onto the shared "default" recipe (FR-007, INV-5). A recipe
                // that already carries an explicit Name keeps it, so distinct named recipes for
                // the same legacy type still register side by side.
                var bindPerInstance = NormalizeRecipeLocation(effectiveRecipe?.RecipeLocation) is not null
                    && IsDivergentType(resourceType);
                var recipeNameOverride = bindPerInstance && string.IsNullOrEmpty(effectiveRecipe?.Name)
                    ? resource.Name
                    : null;
                AddLegacyRecipeEntry(legacyRecipeEntries, resourceType, effectiveRecipe, recipeNameOverride);
            }
            else
            {
                // UDT types bind exactly one recipe per type via the shared recipe pack.
                // Divergence was already rejected above (ASPIRERADIUS026), so every instance of
                // this type shares the same effective recipe.
                AddRecipeEntry(udtRecipeEntries, resourceType, effectiveRecipe);
            }
        }

        // Partition flags.
        var hasUdtResources = radiusResources.Any(r =>
            !IsLegacyResourceType(resolvedTypes[r].ResourceType));
        var hasLegacyResources = radiusResources.Any(r =>
            IsLegacyResourceType(resolvedTypes[r].ResourceType));
        var hasComputeResources = computeResources.Any();
        var useLegacyContainers = _environment.UseLegacyContainers;

        // Compute workloads can route to either the UDT compute container type
        // or the legacy Applications.Core/containers type. When containers go
        // legacy they don't force the UDT chain (only legacy parents).
        var computeForcesUdtChain = hasComputeResources && !useLegacyContainers;
        var computeForcesLegacyChain = hasComputeResources && useLegacyContainers;

        // 2. UDT environment + application — emitted only when we have UDT
        // radius resources or any UDT-bound compute workload. Pure-legacy
        // publishes (Redis-only, or compute-with-WithLegacyContainers) skip
        // the UDT chain entirely so older Radius installs aren't forced to
        // understand `Radius.Core/*`.
        RadiusRecipePackConstruct? recipePackConstruct = null;
        RadiusEnvironmentConstruct? envConstruct = null;
        RadiusApplicationConstruct? appConstruct = null;
        var appIdentifier = "app";

        // When this group's application deploys against an environment owned by another group,
        // that environment (and its recipe pack) is materialized in the other group's app.bicep.
        // This group must not re-declare it — every environment reference is the cross-group
        // environment's full UCP ID instead (FR-005); only the single logical application chain
        // for this group's resources is emitted here.
        var isCrossGroupEnvironment = _groupContext?.CrossGroupEnvironmentId is not null;

        if (hasUdtResources || computeForcesUdtChain)
        {
            if (isCrossGroupEnvironment)
            {
                // Cross-group environment target: emit only the application, wired to the
                // cross-group environment's UCP ID. No local environment or recipe pack.
                appConstruct = CreateApplicationConstruct(appIdentifier, null);
                options.Applications.Add(appConstruct);
            }
            else
            {
                // UDT containers route to Radius.Compute/containers, which the control plane
                // provisions through a recipe. Register the default container recipe in the
                // pack so native containers deploy on shipped Radius without a hand-authored
                // recipe — mirroring how backing resources get their default recipes.
                if (computeForcesUdtChain)
                {
                    AddRecipeEntry(udtRecipeEntries, RadiusResourceTypes.Containers, null);
                }

                recipePackConstruct = CreateRecipePackConstruct(recipePackIdentifier, udtRecipeEntries);
                options.RecipePacks.Add(recipePackConstruct);

                envConstruct = CreateEnvironmentConstruct(envIdentifier, recipePackConstruct);
                options.Environments.Add(envConstruct);

                appConstruct = CreateApplicationConstruct(appIdentifier, envConstruct);
                options.Applications.Add(appConstruct);
            }
        }

        // 3. Legacy parents are emitted lazily — only if any legacy resource is
        // present, or the env opts into legacy containers. Legacy env/app share
        // the *resource name* with the UDT pair so Radius still sees them as
        // the same logical app/environment; only the Bicep identifiers differ.
        LegacyApplicationEnvironmentConstruct? legacyEnvConstruct = null;
        LegacyApplicationConstruct? legacyAppConstruct = null;

        if (hasLegacyResources || computeForcesLegacyChain)
        {
            // If the UDT chain is also emitted we suffix legacy identifiers with
            // `_legacy`; otherwise (pure-legacy publish) legacy can claim the
            // unsuffixed identifiers.
            var legacyEnvIdentifier = (hasUdtResources || computeForcesUdtChain)
                ? envIdentifier + "_legacy" : envIdentifier;
            var legacyAppIdentifier = (hasUdtResources || computeForcesUdtChain)
                ? appIdentifier + "_legacy" : appIdentifier;

            if (isCrossGroupEnvironment)
            {
                // FR-005: the target environment is owned by another group, so this group must
                // not declare a local legacy environment. Legacy applications still deploy by
                // referencing that cross-group environment's full UCP ID.
                legacyAppConstruct = CreateLegacyApplicationConstruct(
                    legacyAppIdentifier, appIdentifier, ResolveEnvironmentId(null));
            }
            else
            {
                legacyEnvConstruct = CreateLegacyEnvironmentConstruct(
                    legacyEnvIdentifier, legacyRecipeEntries);
                options.LegacyEnvironments.Add(legacyEnvConstruct);

                legacyAppConstruct = CreateLegacyApplicationConstruct(
                    legacyAppIdentifier, appIdentifier, BuildIdExpression(legacyEnvConstruct));
            }

            options.LegacyApplications.Add(legacyAppConstruct);
        }

        // 4. Resource type instances — parent wiring depends on legacy vs UDT.
        // Track each builder-created instance's parent pair so RewireIdReferences
        // can re-resolve `.id` after callbacks without clobbering resources that
        // a callback added itself.
        var instanceParents = new Dictionary<RadiusResourceTypeConstruct, (ProvisionableResource? Env, ProvisionableResource App)>();

        foreach (var resource in radiusResources)
        {
            var (resourceType, apiVersion) = resolvedTypes[resource];
            var identifier = BicepPostProcessor.SanitizeIdentifier(resource.Name);

            var customization = GetCustomization(resource);
            var isLegacy = IsLegacyResourceType(resourceType);

            // Per-instance recipe binding applies only to legacy types: a cloud-managed selection
            // wins over the resource's customization recipe, and divergent legacy instances (same
            // type, different recipes) reference a distinct named recipe (FR-007, INV-5). UDT
            // divergence was rejected earlier (ASPIRERADIUS026), so UDT instances always bind their
            // type's single recipe through the recipe pack and never carry an instance-level recipe.
            var managedRecipe = GetManagedRecipe(resource);
            var effectiveRecipe = managedRecipe ?? customization?.Recipe;

            // Legacy instances reference a synthetic per-instance recipe name (their resource
            // name) only when the type is divergent and the effective recipe is nameless; a recipe
            // with an explicit Name is referenced by that Name (see the recipe-collection loop above).
            var bindPerInstance = NormalizeRecipeLocation(effectiveRecipe?.RecipeLocation) is not null
                && IsDivergentType(resourceType);
            var instanceRecipeNameOverride = isLegacy && bindPerInstance && string.IsNullOrEmpty(effectiveRecipe?.Name)
                ? resource.Name
                : null;

            // In the cross-group environment path there is no local UDT or legacy environment
            // construct (the environment lives in another group); the instance's environment is
            // the cross-group UCP ID resolved by CreateResourceTypeConstruct, so parentEnv is null.
            ProvisionableResource? parentEnv = isLegacy ? legacyEnvConstruct : envConstruct;
            ProvisionableResource parentApp = isLegacy ? legacyAppConstruct! : appConstruct!;

            var typeInstance = CreateResourceTypeConstruct(
                identifier, resource.Name, resourceType, apiVersion,
                parentApp, parentEnv, isLegacy, effectiveRecipe,
                instanceRecipeNameOverride);
            options.ResourceTypeInstances.Add(typeInstance);
            typeInstancesByResourceName[resource.Name] = typeInstance;
            instanceParents[typeInstance] = (parentEnv, parentApp);
        }

        // 5. Container workloads. By default, route to the UDT compute container
        // type parented to the UDT application. When `WithLegacyContainers()` is
        // set, route to legacy `Applications.Core/containers` parented to the
        // legacy application instead — useful when the target Radius install
        // has no recipe registered for `Radius.Compute/containers`.
        var containerConnectionTargets = new Dictionary<RadiusContainerConstruct, Dictionary<string, RadiusResourceTypeConstruct>>();
        var legacyContainerConnectionTargets = new Dictionary<LegacyContainerConstruct, Dictionary<string, RadiusResourceTypeConstruct>>();
        foreach (var resource in computeResources)
        {
            var identifier = BicepPostProcessor.SanitizeIdentifier(resource.Name);
            var image = GetContainerImage(resource);
            var connectionTargets = GetConnectionTargets(resource, radiusResources, typeInstancesByResourceName);
            var crossGroupConnections = GetCrossGroupConnections(resource);

            WarnIfImageMayNotPull(resource.Name, image);

            // Resolve the resource's environment variables (config, connection strings, OTEL_*,
            // WithEnvironment, and `services__*` service discovery) and its endpoint ports the
            // same way the Kubernetes publisher does, so the deployed container behaves like the
            // local run. Secret/parameter values are routed to Bicep `param`s (never literals).
            var env = await ResolveEnvironmentAsync(resource).ConfigureAwait(false);
            var ports = ResolvePorts(resource);

            if (useLegacyContainers)
            {
                var legacyContainerConstruct = CreateLegacyContainerConstruct(
                    identifier, resource.Name, image, legacyAppConstruct!, connectionTargets, crossGroupConnections, env, ports);
                options.LegacyContainers.Add(legacyContainerConstruct);
                legacyContainerConnectionTargets[legacyContainerConstruct] = connectionTargets;
            }
            else
            {
                var containerConstruct = CreateContainerConstruct(
                    identifier, resource.Name, image, appConstruct!, envConstruct, connectionTargets, crossGroupConnections, env, ports);
                options.Containers.Add(containerConstruct);
                containerConnectionTargets[containerConstruct] = connectionTargets;
            }
        }

        // 6. Snapshot every identifier that rewiring depends on, then run
        // ConfigureRadiusInfrastructure callbacks (last-write-wins). We only
        // re-resolve a `.id` reference below if its target was *renamed* by a
        // callback; references the callback set explicitly are preserved.
        var identifierSnapshot = new IdentifierSnapshot(
            envConstruct?.BicepIdentifier,
            appConstruct?.BicepIdentifier,
            legacyEnvConstruct?.BicepIdentifier,
            legacyAppConstruct?.BicepIdentifier,
            options.RecipePacks.ToDictionary(p => p, p => p.BicepIdentifier),
            instanceParents.ToDictionary(
                kv => kv.Key,
                kv => (EnvId: kv.Value.Env?.BicepIdentifier,
                       AppId: kv.Value.App.BicepIdentifier)),
            containerConnectionTargets.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToDictionary(
                    tkv => tkv.Key, tkv => tkv.Value.BicepIdentifier)),
            legacyContainerConnectionTargets.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToDictionary(
                    tkv => tkv.Key, tkv => tkv.Value.BicepIdentifier)));

        RunConfigureCallbacks(options);

        // 7. Rewire `.id` cross-references for targets whose BicepIdentifier
        // was changed by a callback; leave everything else (including callback
        // edits to references) alone.
        RewireIdReferences(options, appConstruct, envConstruct,
            legacyAppConstruct, legacyEnvConstruct, instanceParents,
            containerConnectionTargets, legacyContainerConnectionTargets,
            identifierSnapshot);

        // Surface recipe-parameter scopes that target a resource type with no emitted
        // recipe entry (FR-011), and register any ParameterResource-backed Bicep params.
        WarnUnmatchedResourceTypeScopes(udtRecipeEntries.Keys.Concat(legacyRecipeEntries.Keys));
        foreach (var (name, parameter) in _recipeParameters)
        {
            options.RecipeParameters[name] = parameter;
        }

        // Surface the param-identifier -> ParameterResource bindings so the deploy step can
        // resolve a value for every valueless `param` at deploy time (rad deploy --parameters).
        foreach (var (identifier, parameter) in _recipeParameterBindings)
        {
            options.RecipeParameterBindings[identifier] = parameter;
        }

        return options;
    }

    /// <summary>
    /// Pre-callback snapshot of every construct identifier the builder wired
    /// references against. After callbacks run, <see cref="RewireIdReferences"/>
    /// compares each target's current identifier against the snapshot and only
    /// rewires the ones that changed — preserving any direct reference edits a
    /// callback performed.
    /// </summary>
    private sealed record IdentifierSnapshot(
        string? EnvId,
        string? AppId,
        string? LegacyEnvId,
        string? LegacyAppId,
        Dictionary<RadiusRecipePackConstruct, string> RecipePackIds,
        Dictionary<RadiusResourceTypeConstruct, (string? EnvId, string AppId)> InstanceParentIds,
        Dictionary<RadiusContainerConstruct, Dictionary<string, string>> ContainerConnectionTargetIds,
        Dictionary<LegacyContainerConstruct, Dictionary<string, string>> LegacyContainerConnectionTargetIds);

    /// <summary>
    /// Returns <c>true</c> when <paramref name="resourceType"/> is a legacy
    /// <c>Applications.*</c> type that should be parented to
    /// <c>Applications.Core/environments</c> rather than <c>Radius.Core/environments</c>.
    /// </summary>
    private static bool IsLegacyResourceType(string resourceType) =>
        resourceType.StartsWith("Applications.", StringComparison.Ordinal);

    /// <summary>
    /// After callbacks run, re-resolve each builder-created <c>.id</c>
    /// cross-reference only when its target's <c>BicepIdentifier</c> was
    /// actually changed by a callback. References the callback edited directly
    /// (without renaming the target) are preserved — honouring the public
    /// "last-write-wins" contract on <c>ConfigureRadiusInfrastructure</c>.
    /// </summary>
    private static void RewireIdReferences(
        RadiusInfrastructureOptions options,
        RadiusApplicationConstruct? appConstruct,
        RadiusEnvironmentConstruct? envConstruct,
        LegacyApplicationConstruct? legacyAppConstruct,
        LegacyApplicationEnvironmentConstruct? legacyEnvConstruct,
        Dictionary<RadiusResourceTypeConstruct, (ProvisionableResource? Env, ProvisionableResource App)> instanceParents,
        Dictionary<RadiusContainerConstruct, Dictionary<string, RadiusResourceTypeConstruct>> containerConnectionTargets,
        Dictionary<LegacyContainerConstruct, Dictionary<string, RadiusResourceTypeConstruct>> legacyContainerConnectionTargets,
        IdentifierSnapshot snapshot)
    {
        // UDT env → recipe packs. Rebuild only if any builder-created pack was
        // renamed. (New packs added by a callback and removed packs are left to
        // the callback to wire up — this method only fixes broken refs.)
        if (envConstruct is not null)
        {
            var anyPackRenamed = false;
            foreach (var (pack, snapId) in snapshot.RecipePackIds)
            {
                if (!string.Equals(pack.BicepIdentifier, snapId, StringComparison.Ordinal))
                {
                    anyPackRenamed = true;
                    break;
                }
            }

            if (anyPackRenamed)
            {
                envConstruct.RecipePacks.Clear();
                foreach (var pack in options.RecipePacks)
                {
                    envConstruct.RecipePacks.Add(BuildIdExpression(pack));
                }
            }
        }

        // UDT app → UDT env.
        if (appConstruct is not null && envConstruct is not null &&
            IdentifierChanged(envConstruct, snapshot.EnvId))
        {
            appConstruct.EnvironmentId = BuildIdExpression(envConstruct);
        }

        // Legacy app → legacy env.
        if (legacyAppConstruct is not null && legacyEnvConstruct is not null &&
            IdentifierChanged(legacyEnvConstruct, snapshot.LegacyEnvId))
        {
            legacyAppConstruct.EnvironmentId = BuildIdExpression(legacyEnvConstruct);
        }

        // Resource type instances: rewire each parent ref only if *that*
        // parent's identifier was renamed.
        foreach (var instance in options.ResourceTypeInstances)
        {
            if (!instanceParents.TryGetValue(instance, out var parents))
            {
                continue;
            }

            if (!snapshot.InstanceParentIds.TryGetValue(instance, out var snapIds))
            {
                continue;
            }

            if (!string.Equals(parents.App.BicepIdentifier, snapIds.AppId, StringComparison.Ordinal))
            {
                instance.ApplicationId = BuildIdExpression(parents.App);
            }

            if (parents.Env is not null &&
                !string.Equals(parents.Env.BicepIdentifier, snapIds.EnvId, StringComparison.Ordinal))
            {
                instance.EnvironmentId = BuildIdExpression(parents.Env);
            }
        }

        // Containers — rewire ApplicationId only if the UDT app was renamed;
        // rewire each connection source only if its target was renamed.
        foreach (var container in options.Containers)
        {
            if (!containerConnectionTargets.TryGetValue(container, out var targets))
            {
                // Callback-added container; leave its refs alone.
                continue;
            }

            if (appConstruct is not null && IdentifierChanged(appConstruct, snapshot.AppId))
            {
                container.ApplicationId = BuildIdExpression(appConstruct);
            }

            if (envConstruct is not null && IdentifierChanged(envConstruct, snapshot.EnvId))
            {
                container.EnvironmentId = BuildIdExpression(envConstruct);
            }

            if (targets.Count == 0 ||
                !snapshot.ContainerConnectionTargetIds.TryGetValue(container, out var targetSnapIds))
            {
                continue;
            }

            foreach (var (connectionName, targetConstruct) in targets)
            {
                if (!targetSnapIds.TryGetValue(connectionName, out var snapTargetId))
                {
                    continue;
                }

                if (string.Equals(targetConstruct.BicepIdentifier, snapTargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                // Target was renamed — replace the stale connection entry.
                container.Connections[connectionName] = new ConnectionConstruct
                {
                    Source = BuildIdExpression(targetConstruct),
                };
            }
        }

        // Legacy containers — rewire ApplicationId only if the legacy app was
        // renamed; rewire each connection source only if its target was renamed.
        foreach (var container in options.LegacyContainers)
        {
            if (!legacyContainerConnectionTargets.TryGetValue(container, out var targets))
            {
                continue;
            }

            if (legacyAppConstruct is not null && IdentifierChanged(legacyAppConstruct, snapshot.LegacyAppId))
            {
                container.ApplicationId = BuildIdExpression(legacyAppConstruct);
            }

            if (targets.Count == 0 ||
                !snapshot.LegacyContainerConnectionTargetIds.TryGetValue(container, out var targetSnapIds))
            {
                continue;
            }

            foreach (var (connectionName, targetConstruct) in targets)
            {
                if (!targetSnapIds.TryGetValue(connectionName, out var snapTargetId))
                {
                    continue;
                }

                if (string.Equals(targetConstruct.BicepIdentifier, snapTargetId, StringComparison.Ordinal))
                {
                    continue;
                }

                container.Connections[connectionName] = new ConnectionConstruct
                {
                    Source = BuildIdExpression(targetConstruct),
                };
            }
        }
    }

    private static bool IdentifierChanged(ProvisionableResource resource, string? snapshotId)
        => !string.Equals(resource.BicepIdentifier, snapshotId, StringComparison.Ordinal);

    /// <summary>
    /// Resolves the Radius resource type and API version for a resource, checking for
    /// custom type overrides from <see cref="RadiusResourceCustomization"/> before falling
    /// back to the <see cref="ResourceTypeMapper"/>.
    /// </summary>
    private (string ResourceType, string ApiVersion) ResolveResourceType(IResource resource)
    {
        var customization = GetCustomization(resource);
        if (customization?.TypeOverride is { } typeOverride)
        {
            var apiVersion = typeOverride.ApiVersion ?? RadiusResourceTypes.RadiusApiVersion;
            _logger.LogInformation(
                "Resource '{ResourceName}' using custom type override '{RadiusType}' (API {ApiVersion}).",
                resource.Name, typeOverride.Type, apiVersion);
            return (typeOverride.Type, apiVersion);
        }

        return _typeMapper.MapResource(resource);
    }

    private (List<IResource> radiusResources, List<IResource> computeResources, Dictionary<IResource, (string ResourceType, string ApiVersion)> resolvedTypes) ClassifyResources()
    {
        var radiusTypes = new List<IResource>();
        var compute = new List<IResource>();
        var resolved = new Dictionary<IResource, (string ResourceType, string ApiVersion)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in _model.Resources)
        {
            // Skip the Radius environment itself
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            if (_groupContext is not null)
            {
                // Grouped publish path: include the resource only when it is routed to the group
                // this builder is scoped to (FR-007). Group routing supersedes the per-environment
                // deployment-target filter; resource-to-group resolution is owned by the
                // RadiusGroupOrchestrator, whose partition names are passed in here.
                if (!_groupContext.ResourceNames.Contains(ResolveToParent(resource).Name))
                {
                    continue;
                }
            }
            // Check deployment target: only include resources targeted to this environment
            // or resources with no explicit target (default to this environment)
            else if (!IsTargetedToThisEnvironment(resource))
            {
                continue;
            }

            // Resolve child resources to parent
            var resolvedResource = ResolveToParent(resource);
            if (resolvedResource != resource)
            {
                // Child resources (e.g., SqlServerDatabaseResource) are represented
                // via their parent; skip the child itself
                continue;
            }

            // Avoid duplicates
            if (!seen.Add(resource.Name))
            {
                continue;
            }

            // Use ResourceTypeMapper to determine classification:
            // - Explicit container/project resources with Containers mapping → compute workloads
            // - Resources with a specific resource type mapping → resource type instances
            // - Unmapped resources (ParameterResource, etc.) → skip
            var resolvedType = ResolveResourceType(resource);
            resolved[resource] = resolvedType;
            var resourceType = resolvedType.ResourceType;

            if (resource is ProjectResource ||
                (resource is ContainerResource && resourceType == RadiusResourceTypes.Containers))
            {
                compute.Add(resource);
            }
            else if (resourceType != RadiusResourceTypes.Containers)
            {
                radiusTypes.Add(resource);
            }
            // else: unmapped resource (e.g., ParameterResource) — skip
        }

        return (radiusTypes, compute, resolved);
    }

    private bool IsTargetedToThisEnvironment(IResource resource)
    {
        // The PrepareDeploymentTargets pipeline step (RadiusInfrastructure.PrepareDeploymentTargetsAsync)
        // attaches a DeploymentTargetAnnotation to every compute resource that belongs to this
        // environment, with ComputeEnvironment set to OwningComputeEnvironment ?? this. With multiple
        // compute environments in the model, untargeted resources are rejected upstream by
        // ValidateComputeEnvironments before this code runs.
        //
        // Use the framework's canonical lookup (Aspire.Hosting.ApplicationModel.ResourceExtensions
        // .GetDeploymentTargetAnnotation) so behaviour stays in sync with manifest/publish paths
        // and so the lookup honours ComputeEnvironmentAnnotation overrides set via WithComputeEnvironment.
        var targetComputeEnvironment = _environment.OwningComputeEnvironment ?? _environment;
        return resource.GetDeploymentTargetAnnotation(targetComputeEnvironment) is not null;
    }

    /// <summary>
    /// Resolves a child resource (e.g., SqlServerDatabaseResource) to its parent.
    /// Returns the resource itself if it has no parent.
    /// </summary>
    private static IResource ResolveToParent(IResource resource)
    {
        if (resource is IResourceWithParent childResource)
        {
            return childResource.Parent;
        }

        return resource;
    }

    private static RadiusResourceCustomization? GetCustomization(IResource resource)
    {
        var annotation = resource.Annotations.OfType<RadiusResourceCustomizationAnnotation>().LastOrDefault();
        return annotation?.Customization;
    }

    /// <summary>
    /// Rejects user-supplied per-instance recipe configuration (a <see cref="RadiusRecipe.Name"/>
    /// or <see cref="RadiusRecipe.Parameters"/>) on native (Radius.*) types — both backing UDT
    /// resources and native compute containers. Radius binds exactly one recipe per resource type
    /// per environment for user-defined types; per-instance recipe overrides and named recipes are
    /// unsupported, so Radius would silently ignore the per-instance values. Fail the publish with
    /// an actionable diagnostic (ASPIRERADIUS027) that points at the environment-level
    /// <c>WithRecipeParameters(resourceType, ...)</c> API instead. The check is scoped to the user
    /// customization recipe (not the system-supplied cloud-managed recipe) to avoid false positives.
    /// </summary>
    private void RejectPerInstanceRecipeOnNativeTypes(
        List<IResource> radiusResources,
        List<IResource> computeResources,
        Dictionary<IResource, (string ResourceType, string ApiVersion)> resolvedTypes)
    {
        static bool CarriesUserRecipeConfig(RadiusResourceCustomization? customization)
            => customization?.Recipe is { } recipe
                && (recipe.Parameters.Count > 0 || !string.IsNullOrEmpty(recipe.Name));

        foreach (var resource in radiusResources)
        {
            var resourceType = resolvedTypes[resource].ResourceType;
            if (!IsLegacyResourceType(resourceType) && CarriesUserRecipeConfig(GetCustomization(resource)))
            {
                throw PerInstanceRecipeNotSupported(resource.Name, resourceType);
            }
        }

        // Native compute containers route to Radius.Compute/containers (a UDT) unless the
        // environment opts into legacy Applications.Core/containers via WithLegacyContainers().
        // Their customization recipe is not emitted on the container construct today, so reject it
        // explicitly rather than dropping it silently.
        if (!_environment.UseLegacyContainers)
        {
            foreach (var resource in computeResources)
            {
                if (CarriesUserRecipeConfig(GetCustomization(resource)))
                {
                    throw PerInstanceRecipeNotSupported(resource.Name, RadiusResourceTypes.Containers);
                }
            }
        }
    }

    private static InvalidOperationException PerInstanceRecipeNotSupported(string resourceName, string resourceType) =>
        new($"Resource '{resourceName}' (Radius type '{resourceType}') declares a per-instance recipe name or " +
            "parameters, but Radius binds exactly one recipe per resource type per environment for user-defined " +
            "(Radius.*) types — per-instance recipe overrides and named recipes are not supported. Declare recipe " +
            $"parameters at the environment level with WithRecipeParameters(\"{resourceType}\", ...) instead. " +
            "Diagnostic: ASPIRERADIUS027.");

    /// <summary>
    /// Returns the cloud-targeting recipe for <paramref name="resource"/> when it is
    /// marked cloud-managed on the environment being published (via
    /// <c>WithManagedResource</c>), or <see langword="null"/> when it is in-cluster.
    /// The selection is read from the <em>specific</em> environment's annotation so a
    /// resource can be in-cluster in one environment and cloud-managed in another
    /// (FR-006); the annotation is never shared between environments.
    /// </summary>
    private RadiusRecipe? GetManagedRecipe(IResource resource)
    {
        var annotation = _environment.Annotations
            .OfType<Annotations.RadiusManagedResourcesAnnotation>()
            .FirstOrDefault();

        if (annotation is null)
        {
            return null;
        }

        return annotation.Selections.TryGetValue(resource.Name, out var selection)
            ? selection.Recipe
            : null;
    }

    /// <summary>
    /// <c>ASPIRERADIUS020</c>: each cloud-managed selection on this environment requires the
    /// matching cloud provider (<c>WithAzureProvider</c>/<c>WithAwsProvider</c>) to be configured.
    /// Runs at publish time — when the environment's provider set is final — so the check is
    /// independent of the order in which providers and selections were configured.
    /// </summary>
    private void ValidateManagedSelectionProviders()
    {
        var selections = _environment.Annotations
            .OfType<Annotations.RadiusManagedResourcesAnnotation>()
            .FirstOrDefault();

        if (selections is null || selections.Selections.Count == 0)
        {
            return;
        }

        var providers = _environment.Annotations
            .OfType<Annotations.RadiusCloudProvidersAnnotation>()
            .FirstOrDefault();

        foreach (var (resourceName, selection) in selections.Selections)
        {
            var configured = selection.Cloud switch
            {
                CloudProviders.RadiusCloud.Azure => providers?.Azure is not null,
                CloudProviders.RadiusCloud.Aws => providers?.Aws is not null,
                _ => false,
            };

            if (!configured)
            {
                var providerCall = selection.Cloud == CloudProviders.RadiusCloud.Azure ? "WithAzureProvider(...)" : "WithAwsProvider(...)";
                throw new InvalidOperationException(
                    $"Resource '{resourceName}' is marked cloud-managed for {selection.Cloud}, but no " +
                    $"{selection.Cloud} provider is configured on Radius environment '{_environment.Name}'. " +
                    $"Call {providerCall} on the environment to deploy cloud-managed resources for " +
                    $"{selection.Cloud}. Diagnostic: ASPIRERADIUS020.");
            }
        }
    }

    // Treat null and empty/whitespace recipe locations as "no location" (an in-cluster /
    // default-recipe instance) so divergence detection and per-instance binding agree on
    // what counts as a real recipe location.
    private static string? NormalizeRecipeLocation(string? location)
        => string.IsNullOrWhiteSpace(location) ? null : location;

    /// <summary>
    /// Builds a <c>.id</c> expression for a resource, e.g., <c>envIdentifier.id</c>.
    /// </summary>
    private static BicepExpression BuildIdExpression(Azure.Provisioning.Primitives.ProvisionableResource resource)
    {
        return new MemberExpression(new IdentifierExpression(resource.BicepIdentifier), "id");
    }

    /// <summary>
    /// Resolves the value emitted for a construct's <c>properties.environment</c>. When the group
    /// this builder emits deploys against an environment owned by another group, that environment
    /// is materialized in the other group's <c>app.bicep</c>, so the reference is the environment's
    /// full UCP ID (FR-005). Otherwise the bare in-group <c>.id</c> expression is emitted, unchanged.
    /// </summary>
    private BicepValue<string> ResolveEnvironmentId(Azure.Provisioning.Primitives.ProvisionableResource? envConstruct)
    {
        if (_groupContext?.CrossGroupEnvironmentId is { } ucpEnvironmentId)
        {
            return ucpEnvironmentId;
        }

        return BuildIdExpression(envConstruct!);
    }

    /// <summary>
    /// A cross-group connection: the referenced resource's name and the full UCP ID emitted as the
    /// connection <c>source</c> because the target is routed to a different group (FR-004).
    /// </summary>
    private readonly record struct CrossGroupConnectionSource(string Name, string UcpSourceId);

    /// <summary>
    /// Computes the cross-group connections for a compute resource: each <c>WithReference</c> whose
    /// target is routed to a different group emits its connection <c>source</c> as the target's full
    /// UCP ID (FR-004). In-group references keep the bare identifier via
    /// <see cref="GetConnectionTargets"/>. Returns empty outside the grouped publish path.
    /// </summary>
    private List<CrossGroupConnectionSource> GetCrossGroupConnections(IResource resource)
    {
        var result = new List<CrossGroupConnectionSource>();
        if (_groupContext is null)
        {
            return result;
        }

        var references = resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(static r => r.Type == "Reference");

        foreach (var reference in references)
        {
            var target = ResolveToParent(reference.Resource);

            if (!_groupContext.ReferenceByResourceName.TryGetValue(target.Name, out var targetReference)
                || string.Equals(targetReference.Group, _groupContext.Group, StringComparison.Ordinal))
            {
                // Unrouted or in-group target: handled by GetConnectionTargets (bare identifier).
                continue;
            }

            // Only backing (non-compute) Radius resource-type targets carry a connection source.
            var (resourceType, _) = ResolveResourceType(target);
            if (string.Equals(resourceType, RadiusResourceTypes.Containers, StringComparison.Ordinal))
            {
                continue;
            }

            result.Add(new CrossGroupConnectionSource(
                target.Name,
                targetReference.ToUcpResourceId(resourceType, target.Name)));
        }

        return result;
    }

    private RadiusEnvironmentConstruct CreateEnvironmentConstruct(
        string identifier, RadiusRecipePackConstruct recipePackConstruct)
    {
        var construct = new RadiusEnvironmentConstruct(identifier);
        construct.EnvironmentName = _environment.Name;
        construct.KubernetesNamespace = _environment.Namespace;
        construct.RecipePacks.Add(BuildIdExpression(recipePackConstruct));
        ApplyCloudProviders(construct);
        return construct;
    }

    private void ApplyCloudProviders(RadiusEnvironmentConstruct construct)
    {
        var annotation = _environment.Annotations
            .OfType<Annotations.RadiusCloudProvidersAnnotation>()
            .FirstOrDefault();
        if (annotation is null)
        {
            return;
        }

        if (annotation.Azure is { } azure)
        {
            construct.AzureSubscriptionId = azure.SubscriptionId;
            construct.AzureResourceGroupName = azure.ResourceGroup;
        }

        if (annotation.Aws is { } aws)
        {
            construct.AwsAccountId = aws.AccountId;
            construct.AwsRegion = aws.Region;
        }
    }

    // The legacy Applications.Core/environments schema carries cloud providers under the
    // same properties.providers.{azure,aws}.scope paths as the UDT environment. Apply them
    // here too so a pure-legacy publish (e.g. a managed Redis with no UDT compute) still
    // emits the provider configuration that the publish-time ASPIRERADIUS020 check requires.
    private void ApplyCloudProviders(LegacyApplicationEnvironmentConstruct construct)
    {
        var annotation = _environment.Annotations
            .OfType<Annotations.RadiusCloudProvidersAnnotation>()
            .FirstOrDefault();
        if (annotation is null)
        {
            return;
        }

        if (annotation.Azure is { } azure)
        {
            construct.AzureScope = BuildAzureScope(azure);
        }

        if (annotation.Aws is { } aws)
        {
            construct.AwsScope = BuildAwsScope(aws);
        }
    }

    private static string BuildAzureScope(CloudProviders.AzureRadiusProviderConfig azure)
        => $"/subscriptions/{azure.SubscriptionId}/resourceGroups/{azure.ResourceGroup}";

    private static string BuildAwsScope(CloudProviders.AwsRadiusProviderConfig aws)
        => $"/planes/aws/aws/accounts/{aws.AccountId}/regions/{aws.Region}";

    private RadiusApplicationConstruct CreateApplicationConstruct(
        string identifier, RadiusEnvironmentConstruct? envConstruct)
    {
        var construct = new RadiusApplicationConstruct(identifier);
        construct.ApplicationName = identifier;
        construct.EnvironmentId = ResolveEnvironmentId(envConstruct);
        return construct;
    }

    private RadiusResourceTypeConstruct CreateResourceTypeConstruct(
        string identifier, string resourceName, string resourceType, string apiVersion,
        ProvisionableResource appConstruct, ProvisionableResource? envConstruct,
        bool isLegacy, RadiusRecipe? recipe, string? instanceRecipeNameOverride = null)
    {
        var construct = new RadiusResourceTypeConstruct(identifier, resourceType, apiVersion);
        construct.ResourceName = resourceName;
        construct.ApplicationId = BuildIdExpression(appConstruct);
        construct.EnvironmentId = ResolveEnvironmentId(envConstruct);

        // Instance-level recipe name/parameters apply only to legacy Applications.* types, whose
        // environment still supports named recipes. Native (Radius.*) UDT instances bind their
        // type's single recipe through the shared recipe pack and must never emit a per-instance
        // recipe block — Radius silently ignores it. Their parameters are declared at the
        // environment level via WithRecipeParameters(resourceType, ...). User per-instance misuse
        // on native types is rejected earlier (ASPIRERADIUS027); this guard also prevents a
        // cloud-managed recipe's parameters from leaking onto a native instance.
        if (isLegacy)
        {
            // Divergent same-type materialization on a legacy type: bind this instance to its
            // own named recipe (registered on the legacy environment) so it doesn't share
            // the type's in-cluster "default" recipe (FR-007, INV-5).
            var hasRecipeNameOverride = !string.IsNullOrEmpty(instanceRecipeNameOverride);
            if (hasRecipeNameOverride)
            {
                construct.RecipeName = instanceRecipeNameOverride!;
            }

            if (recipe is not null)
            {
                var hasExplicitName = !string.IsNullOrEmpty(recipe.Name);
                var hasParameters = recipe.Parameters.Count > 0;

                // Only emit the instance-level recipe name/parameters when the recipe
                // carries them. A location-only recipe (e.g. a cloud-managed override)
                // that is bound through the recipe pack needs no instance recipe block,
                // and emitting an empty `parameters: {}` would produce Bicep the writer
                // cannot serialize.
                if (hasExplicitName || hasParameters)
                {
                    // A legacy per-instance name override (a divergent same-type instance bound
                    // to its own named recipe registered under its resource name) must win over
                    // the recipe's own Name; otherwise the instance would reference a recipe name
                    // that was never registered. Parameters are still emitted in either case.
                    if (!hasRecipeNameOverride)
                    {
                        construct.RecipeName = hasExplicitName ? recipe.Name : "default";
                    }

                    if (hasParameters)
                    {
                        var sink = (IDictionary<string, IBicepValue>)construct.RecipeParameters;
                        foreach (var (key, value) in recipe.Parameters)
                        {
                            // Route through ConvertRecipeParameterValue so per-resource params get
                            // the same handling as environment-level params: ParameterResource
                            // bindings emit a (secure) Bicep param reference and never a resolved
                            // secret, and RadiusProviderReference resolves against the configured
                            // cloud provider.
                            sink[key] = ConvertRecipeParameterValue(value);
                        }
                    }
                }
            }
        }

        return construct;
    }

    private void AddRecipeEntry(
        Dictionary<string, RecipeEntry> entries,
        string resourceType,
        RadiusRecipe? recipe)
    {
        // A native (Radius.*) type binds one recipe per type via the shared recipe pack, so a
        // recipe's intrinsic parameters (e.g. a cloud-managed selection's RadiusRecipe.Parameters)
        // belong on the pack entry, not on the resource instance. Snapshot them here so they can be
        // emitted under the recipe pack; environment-level WithRecipeParameters(type, ...) is merged
        // on top in CreateRecipePackConstruct. (User per-instance recipe parameters on native types
        // are rejected earlier — ASPIRERADIUS027 — so this path only carries system/managed params.)
        var parameters = recipe is { Parameters.Count: > 0 }
            ? new Dictionary<string, object>(recipe.Parameters, StringComparer.Ordinal)
            : null;

        // Custom recipe with template path
        if (recipe?.RecipeLocation is not null)
        {
            entries[resourceType] = new RecipeEntry("bicep", recipe.RecipeLocation, parameters);
            return;
        }

        // Default recipe template
        if (s_defaultRecipeTemplates.TryGetValue(resourceType, out var defaultTemplate))
        {
            // Don't overwrite a custom entry
            entries.TryAdd(resourceType, new RecipeEntry("bicep", defaultTemplate, parameters));
        }
        else
        {
            _logger.LogWarning(
                "No default recipe template found for resource type '{ResourceType}'. " +
                "Consider providing a custom recipe via PublishAsRadiusResource().",
                resourceType);
        }
    }

    /// <summary>
    /// Computes a stable signature for an effective recipe (its normalized location plus its
    /// intrinsic parameters) so that native (UDT) instances of the same type can be checked for
    /// divergence. Native types bind one recipe — and one parameter set — per type via the shared
    /// recipe pack, so any difference in this signature between same-type instances is a conflict.
    /// </summary>
    private static string ComputeRecipeSignature(string? normalizedLocation, RadiusRecipe? recipe)
    {
        // Each dynamic field is length-prefixed (`<len>:<value>`) so the concatenation is
        // unambiguous even when a value contains the characters used as structural delimiters
        // — two different field sets can never produce the same signature.
        var builder = new StringBuilder();
        AppendSignatureSegment(builder, normalizedLocation ?? string.Empty);
        AppendSignatureSegment(builder, recipe?.Name ?? string.Empty);
        AppendSignatureSegment(
            builder,
            recipe is { Parameters.Count: > 0 }
                ? FormatRecipeParameterForSignature(recipe.Parameters)
                : string.Empty);

        return builder.ToString();
    }

    /// <summary>
    /// Appends a length-prefixed segment (<c>&lt;length&gt;:&lt;value&gt;</c>) so concatenated
    /// signature segments stay unambiguous regardless of the characters they contain.
    /// </summary>
    private static void AppendSignatureSegment(StringBuilder builder, string value)
    {
        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
    }

    /// <summary>Returns the length-prefixed encoding of a single signature segment.</summary>
    private static string SignatureSegment(string value)
        => $"{value.Length}:{value}";

    /// <summary>
    /// Formats a recipe parameter value for the divergence signature. Equal values must produce
    /// equal text and distinct values distinct text (the formatter must be injective across every
    /// supported value shape), so composite values are formatted recursively with length-prefixed
    /// segments and bound <see cref="ParameterResource"/> values / provider references are
    /// represented by their identity rather than a resolved literal. Mirrors the value shapes
    /// <see cref="ConvertRecipeParameterValue"/> accepts.
    /// </summary>
    private static string FormatRecipeParameterForSignature(object? value)
        => value switch
        {
            null => "null",
            IResourceBuilder<ParameterResource> parameterBuilder => $"param:{parameterBuilder.Resource.Name}",
            ParameterResource parameterResource => $"param:{parameterResource.Name}",
            // Provider references share a runtime type, so distinguish them by their identity
            // (cloud + scope field), not the type name a default ToString() would produce.
            RadiusProviderReference providerReference => $"provider:{providerReference.Cloud}:{providerReference.Field}",
            string s => $"s:{s}",
            bool b => $"b:{(b ? "true" : "false")}",
            // Objects are string-keyed maps; sort keys so member order doesn't affect the signature.
            // Each key and child encoding is length-prefixed so embedded delimiters can't collide.
            IReadOnlyDictionary<string, object> map =>
                "{" + string.Concat(map.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => SignatureSegment(kv.Key) + SignatureSegment(FormatRecipeParameterForSignature(kv.Value)))) + "}",
            // Arrays are order-sensitive, so preserve element order in the signature.
            System.Collections.IEnumerable sequence =>
                "[" + string.Concat(sequence.Cast<object?>().Select(v => SignatureSegment(FormatRecipeParameterForSignature(v)))) + "]",
            // Numbers and other scalars: format invariantly so culture can't change the signature.
            IFormattable formattable => $"v:{formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture)}",
            _ => $"v:{value}",
        };

    private RadiusRecipePackConstruct CreateRecipePackConstruct(
        string identifier, Dictionary<string, RecipeEntry> recipeEntries)
    {
        var construct = new RadiusRecipePackConstruct(identifier);
        construct.PackName = "default";

        foreach (var (type, entry) in recipeEntries)
        {
            var recipeEntry = new RecipeEntryConstruct();
            recipeEntry.RecipeKind = entry.RecipeKind;
            recipeEntry.RecipeLocation = entry.RecipeLocation;

            // Merge the recipe's intrinsic parameters (carried from a managed/custom recipe) with
            // environment-level WithRecipeParameters; the environment-level values override on key
            // collision (the explicit user override wins).
            var parameters = MergeRecipeParameters(entry.Parameters, GetEffectiveRecipeParameters(type));
            if (parameters is not null)
            {
                ApplyRecipeParameters(recipeEntry.Parameters, parameters);
            }

            construct.Recipes[type] = recipeEntry;
        }

        return construct;
    }

    /// <summary>
    /// Merges a recipe's intrinsic parameters (the base layer) with the environment-level recipe
    /// parameters (the override layer). Environment-level values win on key collision. Returns
    /// <see langword="null"/> when neither layer contributes a parameter.
    /// </summary>
    private static IReadOnlyDictionary<string, object>? MergeRecipeParameters(
        IReadOnlyDictionary<string, object>? recipeParameters,
        IReadOnlyDictionary<string, object>? environmentParameters)
    {
        if (recipeParameters is null or { Count: 0 })
        {
            return environmentParameters;
        }

        if (environmentParameters is null or { Count: 0 })
        {
            return recipeParameters;
        }

        var merged = new Dictionary<string, object>(recipeParameters, StringComparer.Ordinal);
        foreach (var (key, value) in environmentParameters)
        {
            merged[key] = value;
        }

        return merged;
    }

    /// <summary>
    /// Computes the effective recipe parameter set for a resource type by merging the
    /// environment-wide parameters with any parameters scoped to that resource type.
    /// Resource-type-scoped values win on key collision (FR-006). Returns
    /// <see langword="null"/> when no parameters apply.
    /// </summary>
    private IReadOnlyDictionary<string, object>? GetEffectiveRecipeParameters(string resourceType)
    {
        var annotation = _environment.Annotations
            .OfType<Annotations.RadiusRecipeParametersAnnotation>()
            .FirstOrDefault();
        if (annotation is null)
        {
            return null;
        }

        var effective = new Dictionary<string, object>(annotation.EnvironmentWide, StringComparer.Ordinal);

        if (annotation.ByResourceType.TryGetValue(resourceType, out var scoped))
        {
            foreach (var (key, value) in scoped)
            {
                if (effective.ContainsKey(key))
                {
                    _logger.LogDebug(
                        "Recipe parameter '{Key}' scoped to resource type '{ResourceType}' overrides the environment-wide value.",
                        key, resourceType);
                }

                effective[key] = value;
            }
        }

        return effective.Count == 0 ? null : effective;
    }

    /// <summary>
    /// Serializes each effective recipe parameter into <paramref name="target"/>,
    /// preserving Bicep type fidelity and emitting parameter references for bound
    /// <see cref="ParameterResource"/> values and provider references.
    /// </summary>
    private void ApplyRecipeParameters(BicepDictionary<object> target, IReadOnlyDictionary<string, object> parameters)
    {
        var sink = (IDictionary<string, IBicepValue>)target;
        foreach (var (key, value) in parameters)
        {
            sink[key] = ConvertRecipeParameterValue(value);
        }
    }

    /// <summary>
    /// Converts a single recipe parameter value to a Bicep value. Handles
    /// <see cref="ParameterResource"/> bindings (emitted as a Bicep <c>param</c>
    /// reference, never a resolved secret — FR-003a), provider-scope references
    /// (FR-008), and literal/array/object values (FR-003).
    /// </summary>
    private IBicepValue ConvertRecipeParameterValue(object value)
    {
        switch (value)
        {
            case IResourceBuilder<ParameterResource> parameterBuilder:
                return ParameterReference(GetOrAddRecipeParameter(parameterBuilder.Resource));
            case ParameterResource parameterResource:
                return ParameterReference(GetOrAddRecipeParameter(parameterResource));
            case RadiusProviderReference providerReference:
                return BicepPostProcessor.ToBicepValue(ResolveProviderReference(providerReference));
            default:
                return BicepPostProcessor.ToBicepValue(value);
        }
    }

    /// <summary>
    /// Wraps a Bicep <c>param</c> declaration as a value usable inside a recipe
    /// <c>parameters</c> object (a reference to the parameter identifier).
    /// </summary>
    private static BicepValue<object> ParameterReference(ProvisioningParameter parameter)
    {
        BicepValue<object> reference = parameter;
        return reference;
    }

    /// <summary>
    /// Returns (creating once) the Bicep <c>param</c> declaration for an Aspire
    /// <see cref="ParameterResource"/>. Secret parameters are declared secure so no
    /// value is written to the published artifact (FR-003a).
    /// </summary>
    private ProvisioningParameter GetOrAddRecipeParameter(ParameterResource parameter)
    {
        if (!_recipeParameters.TryGetValue(parameter.Name, out var provisioningParameter))
        {
            var identifier = BicepPostProcessor.SanitizeIdentifier(parameter.Name);

            // Two distinct parameter names can sanitize to the same Bicep identifier (e.g.
            // "my-key" and "my.key" both become "my_key"). Emitting two `param my_key`
            // declarations produces invalid Bicep, so fail with an actionable diagnostic
            // (ASPIRERADIUS028) instead.
            if (_recipeParameterIdentifiers.TryGetValue(identifier, out var existingName))
            {
                throw new InvalidOperationException(
                    $"Recipe parameters bound to Aspire parameters '{existingName}' and '{parameter.Name}' both " +
                    $"map to the Bicep identifier '{identifier}'. Rename one of the parameters so they produce " +
                    "distinct Bicep identifiers. Diagnostic: ASPIRERADIUS028.");
            }

            provisioningParameter = new ProvisioningParameter(identifier, typeof(string))
            {
                IsSecure = parameter.Secret,
            };
            _recipeParameters[parameter.Name] = provisioningParameter;
            _recipeParameterIdentifiers[identifier] = parameter.Name;
            // Remember the originating ParameterResource keyed by the Bicep identifier so the
            // deploy step can pass `--parameters <identifier>=<value>` for this valueless param.
            _recipeParameterBindings[identifier] = parameter;
        }

        return provisioningParameter;
    }

    /// <summary>
    /// Rolls back any recipe parameters allocated after <paramref name="parameterNamesBefore"/>
    /// was snapshotted. Used to keep per-environment-variable resolution all-or-nothing: a
    /// variable that ultimately can't be emitted must not leave its secure <c>param</c> (and the
    /// associated deploy-time binding) behind in the artifact.
    /// </summary>
    private void RollbackRecipeParameters(HashSet<string> parameterNamesBefore)
    {
        var addedNames = _recipeParameters.Keys.Where(name => !parameterNamesBefore.Contains(name)).ToList();
        foreach (var name in addedNames)
        {
            var provisioningParameter = _recipeParameters[name];
            _recipeParameters.Remove(name);

            var identifier = provisioningParameter.BicepIdentifier;
            _recipeParameterIdentifiers.Remove(identifier);
            _recipeParameterBindings.Remove(identifier);
        }
    }

    /// <summary>
    /// Resolves a <see cref="RadiusProviderReference"/> to the corresponding scope value
    /// from the cloud provider configured on this environment. Throws when the referenced
    /// provider is not configured (FR-008).
    /// </summary>
    private string ResolveProviderReference(RadiusProviderReference reference)
    {
        var providers = _environment.Annotations
            .OfType<Annotations.RadiusCloudProvidersAnnotation>()
            .FirstOrDefault();

        return reference.Field switch
        {
            RadiusProviderScopeField.Region =>
                providers?.Aws?.Region ?? throw MissingProviderReference("AWS", "WithAwsProvider"),
            RadiusProviderScopeField.AccountId =>
                providers?.Aws?.AccountId ?? throw MissingProviderReference("AWS", "WithAwsProvider"),
            RadiusProviderScopeField.SubscriptionId =>
                providers?.Azure?.SubscriptionId ?? throw MissingProviderReference("Azure", "WithAzureProvider"),
            RadiusProviderScopeField.ResourceGroup =>
                providers?.Azure?.ResourceGroup ?? throw MissingProviderReference("Azure", "WithAzureProvider"),
            _ => throw new NotSupportedException($"Unknown provider scope field '{reference.Field}'."),
        };
    }

    private InvalidOperationException MissingProviderReference(string cloud, string configureMethod) =>
        new($"A recipe parameter on Radius environment '{_environment.Name}' references {cloud} provider " +
            $"configuration, but no {cloud} provider is configured. Call {configureMethod}(...) on the environment.");

    /// <summary>
    /// Emits a non-fatal warning for each resource-type-scoped parameter set whose
    /// resource type has no recipe entry in the emitted recipe pack (FR-011).
    /// </summary>
    private void WarnUnmatchedResourceTypeScopes(IEnumerable<string> emittedResourceTypes)
    {
        var annotation = _environment.Annotations
            .OfType<Annotations.RadiusRecipeParametersAnnotation>()
            .FirstOrDefault();
        if (annotation is null)
        {
            return;
        }

        var emitted = new HashSet<string>(emittedResourceTypes, StringComparer.Ordinal);
        foreach (var resourceType in annotation.ByResourceType.Keys)
        {
            if (!emitted.Contains(resourceType))
            {
                _logger.LogWarning(
                    "Recipe parameters were scoped to resource type '{ResourceType}' on Radius environment " +
                    "'{Environment}', but no recipe entry of that type exists in the emitted recipe pack; " +
                    "those parameters were ignored.",
                    resourceType, _environment.Name);
            }
        }
    }

    private void AddLegacyRecipeEntry(
        Dictionary<string, Dictionary<string, RecipeEntry>> entries,
        string resourceType,
        RadiusRecipe? recipe,
        string? recipeNameOverride = null)
    {
        // A managed/location-only recipe leaves Name as string.Empty (its default),
        // which `?? "default"` does NOT catch. An empty recipe-name key would emit an
        // empty-string Bicep property (`recipes: { type: { '': {...} } }`) and crash the
        // Azure.Provisioning writer, so collapse empty names to "default" here too.
        // A caller-supplied override (e.g. a cloud-managed resource's name) wins so a
        // mixed-materialization type can hold both the in-cluster "default" recipe and
        // a per-instance cloud recipe (FR-007, INV-5).
        var recipeName = !string.IsNullOrEmpty(recipeNameOverride)
            ? recipeNameOverride
            : string.IsNullOrEmpty(recipe?.Name) ? "default" : recipe.Name;

        if (!entries.TryGetValue(resourceType, out var byName))
        {
            byName = new Dictionary<string, RecipeEntry>(StringComparer.Ordinal);
            entries[resourceType] = byName;
        }

        if (recipe?.RecipeLocation is not null)
        {
            // Custom recipe: last write wins *for the same (type, recipeName)*.
            byName[recipeName] = new RecipeEntry("bicep", recipe.RecipeLocation);
            return;
        }

        if (s_defaultRecipeTemplates.TryGetValue(resourceType, out var defaultTemplate))
        {
            byName.TryAdd(recipeName, new RecipeEntry("bicep", defaultTemplate));
        }
        else
        {
            _logger.LogWarning(
                "No default recipe template found for legacy resource type '{ResourceType}'. " +
                "Consider providing a custom recipe via PublishAsRadiusResource().",
                resourceType);
        }
    }

    private LegacyApplicationEnvironmentConstruct CreateLegacyEnvironmentConstruct(
        string identifier,
        Dictionary<string, Dictionary<string, RecipeEntry>> legacyRecipeEntries)
    {
        var construct = new LegacyApplicationEnvironmentConstruct(identifier);
        // Resource name intentionally matches the UDT environment so Radius
        // treats both parents as the same logical environment scope.
        construct.EnvironmentName = _environment.Name;
        construct.ComputeKind = "kubernetes";
        construct.ComputeNamespace = _environment.Namespace;
        ApplyCloudProviders(construct);

        foreach (var (resourceType, byName) in legacyRecipeEntries)
        {
            var inner = new BicepDictionary<LegacyRecipeEntryConstruct>();
            var parameters = GetEffectiveRecipeParameters(resourceType);
            foreach (var (recipeName, entry) in byName)
            {
                var legacyEntry = new LegacyRecipeEntryConstruct
                {
                    TemplateKind = entry.RecipeKind,
                    TemplatePath = entry.RecipeLocation,
                };

                if (parameters is not null)
                {
                    ApplyRecipeParameters(legacyEntry.Parameters, parameters);
                }

                inner[recipeName] = legacyEntry;
            }
            construct.Recipes[resourceType] = inner;
        }

        return construct;
    }

    private static LegacyApplicationConstruct CreateLegacyApplicationConstruct(
        string identifier, string applicationName,
        BicepValue<string> environmentId)
    {
        var construct = new LegacyApplicationConstruct(identifier);
        // Share the UDT application's `name:` — rubber-duck feedback: only the
        // Bicep identifier is suffixed with `_legacy`.
        construct.ApplicationName = applicationName;
        construct.EnvironmentId = environmentId;
        return construct;
    }

    private static string GetContainerImage(IResource resource)
    {
        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();

        if (imageAnnotation is not null)
        {
            var image = imageAnnotation.Image;
            if (!string.IsNullOrEmpty(imageAnnotation.Tag))
            {
                image = $"{image}:{imageAnnotation.Tag}";
            }

            if (!string.IsNullOrEmpty(imageAnnotation.Registry))
            {
                image = $"{imageAnnotation.Registry}/{image}";
            }

            return image;
        }

        // ProjectResource has no ContainerImageAnnotation by default — the integration does
        // not (yet) build and push project images. Failing fast at publish time with a clear
        // remediation prevents the silent `aspire publish && aspire deploy` → in-cluster
        // ImagePullBackOff failure mode, which is opaque to the user (Radius/Kubernetes
        // surface it, not Aspire). Mirrors the CLI behaviour guideline that errors should
        // name the specific action the user must take.
        if (resource is ProjectResource)
        {
            throw new InvalidOperationException(
                $"Project resource '{resource.Name}' cannot be published to Radius because no container image " +
                "has been associated with it. The Aspire.Hosting.Radius integration does not yet build or push " +
                "project images. As a workaround, build and push an image to a registry the target cluster can " +
                "pull from, then attach it via WithContainerImage(\"<registry>/<image>:<tag>\") on the project " +
                "resource. Tracking issue: https://github.com/microsoft/aspire/issues/16844.");
        }

        // Non-project, non-container resources reach this path only in misconfiguration
        // (the resource type mapping would normally skip them). Fall back to a placeholder
        // image with a logged warning via WarnIfImageMayNotPull so the publish still
        // produces inspectable output.
        return $"{resource.Name}:latest";
    }

    private static Dictionary<string, RadiusResourceTypeConstruct> GetConnectionTargets(
        IResource resource,
        List<IResource> radiusResources,
        Dictionary<string, RadiusResourceTypeConstruct> typeInstancesByResourceName)
    {
        var connections = new Dictionary<string, RadiusResourceTypeConstruct>(StringComparer.Ordinal);

        // Find all ResourceRelationshipAnnotation with type "Reference"
        var references = resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(r => r.Type == "Reference");

        foreach (var reference in references)
        {
            var referencedResource = reference.Resource;

            // Resolve child resources (e.g., SqlServerDatabaseResource) to parent
            if (referencedResource is IResourceWithParent childResource)
            {
                referencedResource = childResource.Parent;
            }

            // Only create connections for Radius resource type instances (non-compute)
            if (radiusResources.Any(p => p.Name == referencedResource.Name)
                && typeInstancesByResourceName.TryGetValue(referencedResource.Name, out var targetConstruct))
            {
                connections[referencedResource.Name] = targetConstruct;
            }
        }

        return connections;
    }

    private RadiusContainerConstruct CreateContainerConstruct(
        string identifier, string resourceName, string image,
        RadiusApplicationConstruct appConstruct,
        RadiusEnvironmentConstruct? envConstruct,
        Dictionary<string, RadiusResourceTypeConstruct> connectionTargets,
        IReadOnlyList<CrossGroupConnectionSource> crossGroupConnections,
        IReadOnlyDictionary<string, ContainerEnvVarConstruct> env,
        IReadOnlyDictionary<string, ContainerPortConstruct> ports)
    {
        var construct = new RadiusContainerConstruct(identifier, resourceName);
        construct.ContainerName = resourceName;
        construct.Image = image;
        construct.ApplicationId = BuildIdExpression(appConstruct);
        construct.EnvironmentId = ResolveEnvironmentId(envConstruct);

        if (connectionTargets.Count > 0)
        {
            foreach (var (name, targetConstruct) in connectionTargets)
            {
                var connectionConstruct = new ConnectionConstruct();
                connectionConstruct.Source = BuildIdExpression(targetConstruct);
                construct.Connections[name] = connectionConstruct;
            }
        }

        foreach (var crossGroup in crossGroupConnections)
        {
            var connectionConstruct = new ConnectionConstruct();
            connectionConstruct.Source = crossGroup.UcpSourceId;
            construct.Connections[crossGroup.Name] = connectionConstruct;
        }

        foreach (var (name, envVar) in env)
        {
            construct.Env[name] = envVar;
        }

        foreach (var (name, port) in ports)
        {
            construct.Ports[name] = port;
        }

        return construct;
    }

    private static LegacyContainerConstruct CreateLegacyContainerConstruct(
        string identifier, string resourceName, string image,
        LegacyApplicationConstruct legacyAppConstruct,
        Dictionary<string, RadiusResourceTypeConstruct> connectionTargets,
        IReadOnlyList<CrossGroupConnectionSource> crossGroupConnections,
        IReadOnlyDictionary<string, ContainerEnvVarConstruct> env,
        IReadOnlyDictionary<string, ContainerPortConstruct> ports)
    {
        var construct = new LegacyContainerConstruct(identifier);
        construct.ContainerName = resourceName;
        construct.Image = image;
        construct.ApplicationId = BuildIdExpression(legacyAppConstruct);

        if (connectionTargets.Count > 0)
        {
            foreach (var (name, targetConstruct) in connectionTargets)
            {
                var connectionConstruct = new ConnectionConstruct();
                connectionConstruct.Source = BuildIdExpression(targetConstruct);
                construct.Connections[name] = connectionConstruct;
            }
        }

        foreach (var crossGroup in crossGroupConnections)
        {
            var connectionConstruct = new ConnectionConstruct();
            connectionConstruct.Source = crossGroup.UcpSourceId;
            construct.Connections[crossGroup.Name] = connectionConstruct;
        }

        foreach (var (name, envVar) in env)
        {
            construct.Env[name] = envVar;
        }

        foreach (var (name, port) in ports)
        {
            construct.Ports[name] = port;
        }

        return construct;
    }

    /// <summary>
    /// Maps a compute resource's <see cref="EndpointAnnotation"/>s to Radius container ports,
    /// keyed by endpoint name. Uses the target (container) port when specified, otherwise the
    /// allocated/declared port. Endpoints with no resolvable port are skipped.
    /// </summary>
    private static Dictionary<string, ContainerPortConstruct> ResolvePorts(IResource resource)
    {
        var ports = new Dictionary<string, ContainerPortConstruct>(StringComparer.Ordinal);
        if (!resource.TryGetAnnotationsOfType<EndpointAnnotation>(out var endpoints))
        {
            return ports;
        }

        foreach (var endpoint in endpoints)
        {
            // Prefer the target (in-container) port; fall back to the declared host port. In
            // publish mode neither may be allocated yet, in which case there is nothing to emit.
            var portValue = endpoint.TargetPort ?? endpoint.Port;
            if (portValue is not int containerPort)
            {
                continue;
            }

            var port = new ContainerPortConstruct
            {
                ContainerPort = containerPort,
                Protocol = endpoint.Protocol == ProtocolType.Udp ? "UDP" : "TCP",
            };
            ports[endpoint.Name] = port;
        }

        return ports;
    }

    /// <summary>
    /// Resolves a compute resource's environment variables into Radius container <c>env</c>
    /// entries. Mirrors the Kubernetes publisher: HTTPS service-discovery variables are dropped
    /// (no in-cluster TLS), endpoint references become cluster-FQDN URLs via the environment's
    /// <see cref="RadiusEnvironmentResource.GetHostAddressExpression"/>, and secret/parameter
    /// values are routed to Bicep <c>param</c>s so no literal secret is written to the artifact.
    /// </summary>
    private async Task<Dictionary<string, ContainerEnvVarConstruct>> ResolveEnvironmentAsync(IResource resource)
    {
        var result = new Dictionary<string, ContainerEnvVarConstruct>(StringComparer.Ordinal);
        if (resource is not IResourceWithEnvironment)
        {
            return result;
        }

        var context = new EnvironmentCallbackContext(_executionContext, resource, cancellationToken: _cancellationToken)
        {
            Logger = _logger,
        };

        if (resource.TryGetAnnotationsOfType<EnvironmentCallbackAnnotation>(out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                await callback.Callback(context).ConfigureAwait(false);
            }
        }

        // Drop HTTPS service-discovery variables: containers in the cluster don't terminate TLS
        // (ingress/service mesh does), so an https `services__*` URL would be unreachable. This
        // matches RemoveHttpsServiceDiscoveryVariables in the Kubernetes/Docker Compose publishers.
        var httpsServiceKeys = context.EnvironmentVariables
            .Where(kvp => kvp.Value is EndpointReference epRef
                && epRef.Scheme == "https"
                && kvp.Key.StartsWith("services__", StringComparison.Ordinal))
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var key in httpsServiceKeys)
        {
            context.EnvironmentVariables.Remove(key);
        }

        foreach (var (key, rawValue) in context.EnvironmentVariables)
        {
            var parts = new List<EnvPart>();

            // Snapshot the recipe-parameter keys so that, if this variable fails to resolve, any
            // secure `param` allocated mid-resolution (e.g. a backing resource's password) can be
            // rolled back. Otherwise a skipped variable would still leak its parameter into the
            // artifact. Resolution must be all-or-nothing per variable.
            var parameterKeysBefore = new HashSet<string>(_recipeParameters.Keys, StringComparer.Ordinal);

            try
            {
                await ResolveEnvPartsAsync(rawValue, resource, parts).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                // Some endpoint references cannot be resolved at publish time — e.g. a non-HTTP
                // scheme (redis, tcp, ...) whose port isn't allocated yet. Those backing resources
                // are surfaced through Radius `connection`s instead of literal container env vars,
                // so skip the variable rather than failing the whole build.
                RollbackRecipeParameters(parameterKeysBefore);
                _logger.LogDebug(ex, "Skipping environment variable '{Key}' on resource '{Resource}': value could not be resolved at publish time.", key, resource.Name);
                continue;
            }

            result[key] = new ContainerEnvVarConstruct { Value = BuildEnvBicepValue(parts) };
        }

        return result;
    }

    /// <summary>
    /// A single resolved fragment of an environment-variable value: either a literal string
    /// chunk or a reference to a Bicep <c>param</c> (used for secret/parameter-backed values so
    /// the literal never lands in the artifact).
    /// </summary>
    private readonly record struct EnvPart(string? Literal, ProvisioningParameter? Parameter)
    {
        public static EnvPart FromLiteral(string literal) => new(literal, null);
        public static EnvPart FromParameter(ProvisioningParameter parameter) => new(null, parameter);
    }

    /// <summary>
    /// Recursively flattens an environment-variable value into ordered <see cref="EnvPart"/>s.
    /// Endpoint references resolve to cluster-FQDN URLs, parameter resources resolve to Bicep
    /// <c>param</c> references, and composite reference expressions are spliced together so a
    /// mixed literal/secret value is preserved precisely.
    /// </summary>
    private async Task ResolveEnvPartsAsync(object? value, IResource owner, List<EnvPart> parts)
    {
        switch (value)
        {
            case null:
                return;
            case string s:
                parts.Add(EnvPart.FromLiteral(s));
                return;
            case bool b:
                parts.Add(EnvPart.FromLiteral(b ? "true" : "false"));
                return;
            case ParameterResource param:
                parts.Add(EnvPart.FromParameter(GetOrAddRecipeParameter(param)));
                return;
            case IResourceBuilder<ParameterResource> paramBuilder:
                parts.Add(EnvPart.FromParameter(GetOrAddRecipeParameter(paramBuilder.Resource)));
                return;
            case EndpointReference endpointReference:
                parts.Add(EnvPart.FromLiteral(ResolveEndpointUrl(endpointReference)));
                return;
            case EndpointReferenceExpression endpointReferenceExpression:
                parts.Add(EnvPart.FromLiteral(ResolveEndpointProperty(endpointReferenceExpression)));
                return;
            case ConnectionStringReference connectionStringReference:
                await ResolveEnvPartsAsync(connectionStringReference.Resource.ConnectionStringExpression, owner, parts).ConfigureAwait(false);
                return;
            case IResourceWithConnectionString resourceWithConnectionString:
                await ResolveEnvPartsAsync(resourceWithConnectionString.ConnectionStringExpression, owner, parts).ConfigureAwait(false);
                return;
            case ReferenceExpression referenceExpression:
                await ResolveReferenceExpressionPartsAsync(referenceExpression, owner, parts).ConfigureAwait(false);
                return;
            case IFormattable formattable:
                parts.Add(EnvPart.FromLiteral(formattable.ToString(null, CultureInfo.InvariantCulture)));
                return;
            default:
                // Fall back to publish-mode resolution (e.g. manifest expression providers) and
                // capture whatever literal string the framework produces.
                if (value is IValueProvider valueProvider)
                {
                    var context = new ValueProviderContext { ExecutionContext = _executionContext, Caller = owner };
                    var resolved = await valueProvider.GetValueAsync(context, _cancellationToken).ConfigureAwait(false);
                    parts.Add(EnvPart.FromLiteral(resolved ?? string.Empty));
                    return;
                }

                parts.Add(EnvPart.FromLiteral(value.ToString() ?? string.Empty));
                return;
        }
    }

    /// <summary>
    /// Splices a composite <see cref="ReferenceExpression"/> into ordered parts by interleaving
    /// its literal <see cref="ReferenceExpression.Format"/> chunks with the recursively-resolved
    /// parts of each value provider (matching the <c>{0}</c>, <c>{1}</c>, ... placeholders).
    /// </summary>
    private async Task ResolveReferenceExpressionPartsAsync(ReferenceExpression expression, IResource owner, List<EnvPart> parts)
    {
        // No providers: the format string is already the literal value (after un-escaping braces).
        if (expression.ValueProviders.Count == 0)
        {
            parts.Add(EnvPart.FromLiteral(UnescapeBraces(expression.Format)));
            return;
        }

        // Pre-resolve each provider's parts so the placeholder splice is a simple lookup.
        var providerParts = new List<EnvPart>[expression.ValueProviders.Count];
        for (var i = 0; i < expression.ValueProviders.Count; i++)
        {
            var inner = new List<EnvPart>();
            await ResolveEnvPartsAsync(expression.ValueProviders[i], owner, inner).ConfigureAwait(false);
            providerParts[i] = inner;
        }

        // Walk the format string, emitting literal text and substituting `{i}` placeholders.
        // Braces are escaped as `{{`/`}}` in composite expression formats.
        var format = expression.Format;
        var literal = new StringBuilder();
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (c == '{')
            {
                if (i + 1 < format.Length && format[i + 1] == '{')
                {
                    literal.Append('{');
                    i++;
                    continue;
                }

                var close = format.IndexOf('}', i + 1);
                var indexText = format.Substring(i + 1, close - i - 1);
                var index = int.Parse(indexText, CultureInfo.InvariantCulture);

                if (literal.Length > 0)
                {
                    parts.Add(EnvPart.FromLiteral(literal.ToString()));
                    literal.Clear();
                }

                parts.AddRange(providerParts[index]);
                i = close;
                continue;
            }

            if (c == '}' && i + 1 < format.Length && format[i + 1] == '}')
            {
                literal.Append('}');
                i++;
                continue;
            }

            literal.Append(c);
        }

        if (literal.Length > 0)
        {
            parts.Add(EnvPart.FromLiteral(literal.ToString()));
        }
    }

    private static string UnescapeBraces(string format) =>
        format.Replace("{{", "{", StringComparison.Ordinal).Replace("}}", "}", StringComparison.Ordinal);

    /// <summary>
    /// Builds a Bicep value from ordered env parts: a pure-literal value emits a plain string,
    /// a single parameter emits a bare <c>param</c> reference, and a mix emits an interpolated
    /// Bicep string so secret parameters stay out of the literal artifact.
    /// </summary>
    private static BicepValue<string> BuildEnvBicepValue(List<EnvPart> parts)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        if (parts.Count == 1)
        {
            var only = parts[0];
            if (only.Parameter is { } singleParameter)
            {
                BicepValue<string> reference = singleParameter;
                return reference;
            }

            return only.Literal ?? string.Empty;
        }

        if (parts.All(p => p.Parameter is null))
        {
            return string.Concat(parts.Select(p => p.Literal ?? string.Empty));
        }

        // Mixed literal/parameter content: build a FormattableString whose literal text is the
        // concatenated literal chunks (with braces escaped) and whose args are the parameters,
        // then compile it to an interpolated Bicep string via BicepFunction.Interpolate.
        var formatBuilder = new StringBuilder();
        var args = new List<object>();
        foreach (var part in parts)
        {
            if (part.Parameter is { } parameter)
            {
                formatBuilder.Append('{').Append(args.Count.ToString(CultureInfo.InvariantCulture)).Append('}');
                args.Add(parameter);
            }
            else
            {
                formatBuilder.Append((part.Literal ?? string.Empty)
                    .Replace("{", "{{", StringComparison.Ordinal)
                    .Replace("}", "}}", StringComparison.Ordinal));
            }
        }

        var formattable = FormattableStringFactory.Create(formatBuilder.ToString(), args.ToArray());
        return BicepFunction.Interpolate(formattable);
    }

    /// <summary>
    /// Resolves an <see cref="EndpointReference"/> to a cluster-FQDN URL (<c>scheme://host:port</c>)
    /// using the environment's <see cref="RadiusEnvironmentResource.GetHostAddressExpression"/> so
    /// the namespace-qualified service name is used.
    /// </summary>
    private string ResolveEndpointUrl(EndpointReference endpointReference) =>
        ResolveHostExpression(((IComputeEnvironmentResource)_environment).GetEndpointPropertyExpression(endpointReference.Property(EndpointProperty.Url)));

    private string ResolveEndpointProperty(EndpointReferenceExpression endpointReferenceExpression) =>
        ResolveHostExpression(((IComputeEnvironmentResource)_environment).GetEndpointPropertyExpression(endpointReferenceExpression));

    /// <summary>
    /// Resolves a <see cref="ReferenceExpression"/> produced by the environment's endpoint
    /// helpers to a literal string. The host address is a literal cluster FQDN, so the whole
    /// expression resolves synchronously without needing the run-mode value pipeline.
    /// </summary>
    private static string ResolveHostExpression(ReferenceExpression expression) =>
        expression.GetValueAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult() ?? string.Empty;

    /// <summary>
    /// Warns when a container image may not pull correctly without <c>imagePullPolicy</c>.
    /// The container v2 schema removes <c>imagePullPolicy</c>, so users of kind clusters or
    /// local images need to ensure images are pre-loaded and use explicit tags.
    /// </summary>
    private void WarnIfImageMayNotPull(string resourceName, string image)
    {
        if (image.EndsWith(":latest", StringComparison.Ordinal) || !image.Contains(':'))
        {
            _logger.LogWarning(
                "Resource '{ResourceName}' uses image '{Image}' which may default to 'Always' pull policy " +
                "in Kubernetes. The Radius container v2 schema no longer supports imagePullPolicy. " +
                "For kind clusters, pre-load images with 'kind load docker-image' and use explicit tags.",
                resourceName, image);
        }

        if (!image.Contains('/'))
        {
            _logger.LogWarning(
                "Resource '{ResourceName}' uses image '{Image}' without a registry prefix. " +
                "Ensure the image is available in the target cluster (e.g., pre-loaded via 'kind load docker-image').",
                resourceName, image);
        }
    }

    private void RunConfigureCallbacks(RadiusInfrastructureOptions options)
    {
        var callbacks = _environment.Annotations
            .OfType<RadiusInfrastructureConfigureAnnotation>()
            .ToArray();

        foreach (var callback in callbacks)
        {
            callback.Configure(options);
        }
    }

    internal readonly record struct RecipeEntry(string RecipeKind, string RecipeLocation, IReadOnlyDictionary<string, object>? Parameters = null);
}

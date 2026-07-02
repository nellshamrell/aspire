// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Secret-store consumer edges reference the experimental store resource.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;

namespace Aspire.Hosting.Radius.ResourceGroups;

/// <summary>
/// The single, app-model-scoped owner of multi-resource-group publish and deploy
/// (research Decision 10). When any resource is routed via
/// <c>WithRadiusResourceGroup</c>, this orchestrator resolves and validates the
/// routing, partitions the model into one <see cref="RadiusGroupPartition"/> per
/// group (materializing every environment routed to that group), and computes the
/// dependency-ordered deploy sequence. It is keyed by <b>group</b>, not environment,
/// so each group is emitted and deployed exactly once regardless of how many
/// environments it holds (FR-007, FR-012). The per-environment publish/deploy steps
/// continue to own the no-group default path unchanged (SC-004).
/// </summary>
internal sealed class RadiusGroupOrchestrator
{
    private RadiusGroupOrchestrator(
        IReadOnlyList<RadiusGroupPartition> partitions,
        IReadOnlyList<string> deployOrder,
        IReadOnlyDictionary<string, RadiusResourceGroupReference> referenceByResourceName)
    {
        Partitions = partitions;
        DeployOrder = deployOrder;
        ReferenceByResourceName = referenceByResourceName;
    }

    /// <summary>One partition per group that carries at least one routed resource, in declaration order.</summary>
    internal IReadOnlyList<RadiusGroupPartition> Partitions { get; }

    /// <summary>
    /// The groups in dependency-ordered deploy sequence — every group appears after all
    /// the groups it depends on (SC-002), ties broken by declaration order.
    /// </summary>
    internal IReadOnlyList<string> DeployOrder { get; }

    /// <summary>
    /// Resolved group assignment for every routed resource, keyed by resource name. Emission
    /// uses this to decide whether a cross-group <c>WithReference</c> target or environment
    /// target must be rendered as a full UCP ID rather than a bare in-group name (FR-004, FR-005).
    /// </summary>
    internal IReadOnlyDictionary<string, RadiusResourceGroupReference> ReferenceByResourceName { get; }

    /// <summary>
    /// Cheap check: is any resource in <paramref name="model"/> routed to a Radius resource
    /// group? When <see langword="false"/>, the default (no-group) path is taken unchanged.
    /// </summary>
    internal static bool IsRoutingActive(DistributedApplicationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return model.Resources.Any(static r => r.Annotations.OfType<RadiusResourceGroupAnnotation>().Any());
    }

    /// <summary>
    /// Resolves and validates group routing over the whole application model, returning the
    /// per-group partitions and deploy order. Throws on invalid routing.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A resource is orphaned (<c>ASPIRERADIUS031</c>) or ambiguously assigned to more than
    /// one group or environment group (<c>ASPIRERADIUS032</c>), a routed group name is invalid
    /// (<c>ASPIRERADIUS033</c>), a cross-group environment target is unresolvable
    /// (<c>ASPIRERADIUS034</c>), the group dependency graph contains a cycle
    /// (<c>ASPIRERADIUS035</c>), a group resolves to more than one environment
    /// (<c>ASPIRERADIUS036</c>) or to none (<c>ASPIRERADIUS037</c>), or two group names differ
    /// only by case (<c>ASPIRERADIUS038</c>).
    /// </exception>
    internal static RadiusGroupOrchestrator Create(DistributedApplicationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var routable = GetRoutableResources(model, out var routingAnnotations);

        // Resolve exactly one group per routable resource (ASPIRERADIUS031 / ASPIRERADIUS032).
        var groupOf = new Dictionary<IResource, RadiusResourceGroupReference>();
        var declarationOrder = new List<string>();
        foreach (var resource in routable)
        {
            // Routing annotations may have been placed on a child resource (e.g. a database on its
            // server); GetRoutableResources accumulates those onto the resolved parent so child-level
            // WithRadiusResourceGroup(...) is honored rather than silently ignored.
            var annotations = routingAnnotations.TryGetValue(resource, out var collected)
                ? collected
                : [];
            var distinctGroups = annotations.Select(static a => a.Group).Distinct(StringComparer.Ordinal).ToList();

            if (distinctGroups.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' is not assigned to any Radius resource group. " +
                    "Route it with WithRadiusResourceGroup(...). Diagnostic: ASPIRERADIUS031.");
            }

            if (distinctGroups.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' is ambiguously assigned to multiple Radius resource groups " +
                    $"({string.Join(", ", distinctGroups)}). A resource must resolve to exactly one group. " +
                    "Diagnostic: ASPIRERADIUS032.");
            }

            // Routing declared on a child and its parent can agree on the group yet disagree on the
            // environment group (e.g. WithRadiusResourceGroup("a") on the parent and ("a","b") on the
            // child). annotations[^1] would silently pick the last one in model order, so surface the
            // conflict instead of resolving it order-dependently.
            var distinctEnvironmentGroups = annotations
                .Select(static a => a.EffectiveEnvironmentGroup)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctEnvironmentGroups.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' is ambiguously assigned to a single Radius resource group " +
                    $"('{distinctGroups[0]}') but to more than one environment group " +
                    $"({string.Join(", ", distinctEnvironmentGroups)}); a route declared on a child resource " +
                    "applies to its parent, so a parent/child (or child/child) environment-group disagreement " +
                    "trips this. A resource must resolve to exactly one environment. Diagnostic: ASPIRERADIUS032.");
            }

            var annotation = annotations[^1];

            // Defence in depth: RadiusResourceGroupAnnotation stores names unvalidated, so routing set
            // through the annotation directly (bypassing the public overloads) is validated here too.
            if (!RadiusResourceGroupReference.IsValidName(annotation.Group))
            {
                throw new InvalidOperationException(BuildInvalidNameMessage(resource.Name, annotation.Group));
            }
            if (!RadiusResourceGroupReference.IsValidName(annotation.EffectiveEnvironmentGroup))
            {
                throw new InvalidOperationException(BuildInvalidNameMessage(resource.Name, annotation.EffectiveEnvironmentGroup));
            }

            var reference = new RadiusResourceGroupReference(annotation.Group, annotation.EnvironmentGroup);
            groupOf[resource] = reference;

            if (!declarationOrder.Contains(reference.Group))
            {
                declarationOrder.Add(reference.Group);
            }
            if (!declarationOrder.Contains(reference.EnvironmentGroup))
            {
                declarationOrder.Add(reference.EnvironmentGroup);
            }
        }

        // Radius resource-group names are case-insensitive, so two names that differ only by case
        // (e.g. "shared" and "Shared") collide server-side while our internal routing keeps them
        // ordinal-distinct — they would map to the same UCP resource group with unpredictable
        // results. Reject the collision up front. declarationOrder already holds every distinct
        // group and environment-group name in first-seen order.
        var seenByCaseInsensitiveName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in declarationOrder)
        {
            if (seenByCaseInsensitiveName.TryGetValue(name, out var existing)
                && !string.Equals(existing, name, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Radius resource-group names '{existing}' and '{name}' differ only by case. Radius " +
                    "resource-group names are case-insensitive, so these would collide. Use a single " +
                    "consistent spelling. Diagnostic: ASPIRERADIUS038.");
            }
            seenByCaseInsensitiveName[name] = name;
        }

        // Groups that own at least one environment.
        var environmentGroups = new HashSet<string>(
            groupOf.Where(static kv => kv.Key is RadiusEnvironmentResource)
                   .Select(static kv => kv.Value.Group),
            StringComparer.Ordinal);

        // ASPIRERADIUS034: an explicit cross-group environment target must resolve to a group
        // that actually owns an environment.
        foreach (var (resource, reference) in groupOf)
        {
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            if (reference.IsCrossGroupEnvironment && !environmentGroups.Contains(reference.EnvironmentGroup))
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' deploys against an environment in group " +
                    $"'{reference.EnvironmentGroup}', but no Radius environment is routed to that group. " +
                    "Route an environment there or correct the environment group. Diagnostic: ASPIRERADIUS034.");
            }
        }

        // ASPIRERADIUS036 / ASPIRERADIUS037: the emission and deploy model resolves each group to a
        // single environment and applies it to every resource in the group (RadiusGroupContext carries
        // one CrossGroupEnvironmentId). Validate that invariant here so violations fail fast with a
        // clear diagnostic instead of silently emitting resources against the wrong environment
        // (ASPIRERADIUS036) or dropping them at emission time when nothing resolves (ASPIRERADIUS037).
        var membersByGroup = groupOf
            .Where(static kv => kv.Key is not RadiusEnvironmentResource)
            .GroupBy(static kv => kv.Value.Group, StringComparer.Ordinal);

        foreach (var members in membersByGroup)
        {
            var group = members.Key;
            var groupOwnsEnvironment = environmentGroups.Contains(group);

            // The environment groups the group's resources ask to deploy against. A group that owns an
            // environment always deploys against its own (bare, in-group) environment, so any member
            // requesting a *different* environment group is a conflict.
            var distinctEnvironmentGroups = members
                .Select(static kv => kv.Value.EnvironmentGroup)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (groupOwnsEnvironment)
            {
                var conflicting = distinctEnvironmentGroups
                    .Where(e => !string.Equals(e, group, StringComparison.Ordinal))
                    .ToList();
                if (conflicting.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"Resources in Radius resource group '{group}' resolve to more than one environment: " +
                        $"the group owns an environment (deploying in-group), but one or more resources target a " +
                        $"different environment group ({string.Join(", ", conflicting)}). All resources in a group " +
                        "must deploy against a single environment. Diagnostic: ASPIRERADIUS036.");
                }
            }
            else if (distinctEnvironmentGroups.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Resources in Radius resource group '{group}' resolve to more than one environment group " +
                    $"({string.Join(", ", distinctEnvironmentGroups)}). All resources in a group must deploy against " +
                    "a single environment. Diagnostic: ASPIRERADIUS036.");
            }

            // The environment group this group actually resolves against: its own when it owns an
            // environment, otherwise the (now single) environment group its resources agreed on.
            var resolvedEnvironmentGroup = groupOwnsEnvironment ? group : distinctEnvironmentGroups[0];
            if (!environmentGroups.Contains(resolvedEnvironmentGroup))
            {
                throw new InvalidOperationException(
                    $"Radius resource group '{group}' carries resources but resolves to no Radius environment " +
                    $"(neither the group nor its environment group '{resolvedEnvironmentGroup}' owns one). Route a " +
                    "Radius environment into the group, or target a group that owns one. Diagnostic: ASPIRERADIUS037.");
            }
        }

        // ASPIRERADIUS035: reject cycles over the unioned group dependency graph.
        var edges = BuildEdges(routable, groupOf);
        var graph = RadiusGroupGraph.Build(declarationOrder, edges);
        var cycle = graph.FindCycle();
        if (cycle is not null)
        {
            throw new InvalidOperationException(
                "Cyclic cross-group dependency detected among Radius resource groups: " +
                $"{string.Join(" → ", cycle)}. Break the cycle before publishing or deploying. " +
                "Diagnostic: ASPIRERADIUS035.");
        }

        var partitions = BuildPartitions(routable, groupOf, declarationOrder);
        var deployOrder = graph.TopologicalOrder();

        var referenceByResourceName = groupOf.ToDictionary(
            static kv => kv.Key.Name,
            static kv => kv.Value,
            StringComparer.Ordinal);

        return new RadiusGroupOrchestrator(partitions, deployOrder, referenceByResourceName);
    }

    // Shared message for a routed group/environment-group name that fails validation when read
    // back from an annotation (the public overloads reject the same names at the call site with
    // an ArgumentException). Diagnostic: ASPIRERADIUS033.
    private static string BuildInvalidNameMessage(string resourceName, string groupName) =>
        $"Resource '{resourceName}' is routed to the invalid Radius resource-group name '{groupName}'. " +
        $"A group name must be 1-{RadiusResourceGroupReference.MaxNameLength} characters of ASCII letters, " +
        "digits, '-', '_', or '.', may not start or end with '.', may not contain '..', and may not be a " +
        "reserved device name (e.g. CON, NUL). Diagnostic: ASPIRERADIUS033.";

    /// <summary>
    /// Builds one partition per group that carries at least one routed resource, preserving
    /// declaration order. Each partition materializes the environments routed to that group
    /// and records the group's first-declared environment (the deterministic, inert
    /// <c>--environment</c> default, FR-011).
    /// </summary>
    private static IReadOnlyList<RadiusGroupPartition> BuildPartitions(
        IReadOnlyList<IResource> routable,
        IReadOnlyDictionary<IResource, RadiusResourceGroupReference> groupOf,
        IReadOnlyList<string> declarationOrder)
    {
        var environmentsByGroup = new Dictionary<string, List<RadiusEnvironmentResource>>(StringComparer.Ordinal);
        var resourcesByGroup = new Dictionary<string, List<IResource>>(StringComparer.Ordinal);

        // Iterate routable in model order so environments/resources keep a stable, deterministic
        // order within each group (first-declared wins for the --environment default).
        foreach (var resource in routable)
        {
            if (!groupOf.TryGetValue(resource, out var reference))
            {
                continue;
            }

            if (resource is RadiusEnvironmentResource environment)
            {
                if (!environmentsByGroup.TryGetValue(reference.Group, out var envs))
                {
                    envs = new List<RadiusEnvironmentResource>();
                    environmentsByGroup[reference.Group] = envs;
                }
                envs.Add(environment);
            }
            else
            {
                if (!resourcesByGroup.TryGetValue(reference.Group, out var resources))
                {
                    resources = new List<IResource>();
                    resourcesByGroup[reference.Group] = resources;
                }
                resources.Add(resource);
            }
        }

        var partitions = new List<RadiusGroupPartition>();
        foreach (var group in declarationOrder)
        {
            var envs = environmentsByGroup.TryGetValue(group, out var e)
                ? (IReadOnlyList<RadiusEnvironmentResource>)e
                : Array.Empty<RadiusEnvironmentResource>();
            var resources = resourcesByGroup.TryGetValue(group, out var r)
                ? (IReadOnlyList<IResource>)r
                : Array.Empty<IResource>();

            // Skip groups that only appear as a cross-group environment-target name but carry
            // no routed resources or environments of their own.
            if (envs.Count == 0 && resources.Count == 0)
            {
                continue;
            }

            partitions.Add(new RadiusGroupPartition(
                group,
                envs,
                resources,
                envs.Count > 0 ? envs[0].Name : null));
        }

        return partitions;
    }

    /// <summary>
    /// The set of resources that require a group assignment: Radius environments, compute
    /// workloads (projects/containers), and backing resources (connection-string resources),
    /// plus any resource explicitly routed via <c>WithRadiusResourceGroup</c>. Child resources
    /// are resolved to their parent and de-duplicated by name.
    /// </summary>
    private static IReadOnlyList<IResource> GetRoutableResources(
        DistributedApplicationModel model,
        out Dictionary<IResource, List<RadiusResourceGroupAnnotation>> routingAnnotations)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var routable = new List<IResource>();
        routingAnnotations = new Dictionary<IResource, List<RadiusResourceGroupAnnotation>>();

        foreach (var resource in model.Resources)
        {
            var resolved = resource is IResourceWithParent child ? child.Parent : resource;

            // WithRadiusResourceGroup annotates the resource it is called on, which may be a child
            // (e.g. a database on its server). A child's routing applies to the resolved parent, so
            // read the *original* resource's annotations here (not just the parent's) and treat a
            // child-declared route as explicit routing that pulls the parent into the model.
            var annotations = resource.Annotations.OfType<RadiusResourceGroupAnnotation>().ToList();
            if (!IsRoutable(resolved) && annotations.Count == 0)
            {
                continue;
            }

            if (seen.Add(resolved.Name))
            {
                routable.Add(resolved);
            }

            // Accumulate every routing annotation (from the parent itself and from any of its
            // children) onto the resolved parent. Keeping them all — rather than last-write-wins —
            // preserves ASPIRERADIUS032 ambiguity detection when a child and parent (or two children)
            // disagree on the group.
            if (annotations.Count > 0)
            {
                if (!routingAnnotations.TryGetValue(resolved, out var list))
                {
                    list = new List<RadiusResourceGroupAnnotation>();
                    routingAnnotations[resolved] = list;
                }

                list.AddRange(annotations);
            }
        }

        return routable;
    }

    private static bool IsRoutable(IResource resource) =>
        resource is RadiusEnvironmentResource
        || resource is ProjectResource
        || resource is ContainerResource
        || resource is IResourceWithConnectionString;

    /// <summary>
    /// Builds the union of cross-group <c>WithReference</c> edges and cross-group
    /// environment-target edges. An edge <c>from → to</c> means group <c>from</c> depends
    /// on group <c>to</c>.
    /// </summary>
    private static IEnumerable<(string From, string To)> BuildEdges(
        IReadOnlyList<IResource> routable,
        IReadOnlyDictionary<IResource, RadiusResourceGroupReference> groupOf)
    {
        var byName = routable.ToDictionary(static r => r.Name, StringComparer.Ordinal);
        var edges = new List<(string From, string To)>();

        foreach (var resource in routable)
        {
            if (resource is RadiusEnvironmentResource || !groupOf.TryGetValue(resource, out var reference))
            {
                continue;
            }

            // Cross-group WithReference edges.
            var references = resource.Annotations
                .OfType<ResourceRelationshipAnnotation>()
                .Where(static r => r.Type == "Reference");

            foreach (var relationship in references)
            {
                var target = relationship.Resource;
                if (target is IResourceWithParent child)
                {
                    target = child.Parent;
                }

                if (byName.TryGetValue(target.Name, out var targetResource) &&
                    groupOf.TryGetValue(targetResource, out var targetReference) &&
                    !string.Equals(reference.Group, targetReference.Group, StringComparison.Ordinal))
                {
                    edges.Add((reference.Group, targetReference.Group));
                }
            }

            // Cross-group environment-target edge.
            if (reference.IsCrossGroupEnvironment)
            {
                edges.Add((reference.Group, reference.EnvironmentGroup));
            }
        }

        // Cross-group secret-store consumer edges: a recipeConfig / envSecrets consumer on an
        // environment references a secret store; when the store is routed to another group, the
        // consuming environment's group depends on the store's group so the store deploys first
        // (FR-014).
        foreach (var resource in routable)
        {
            if (resource is not RadiusEnvironmentResource ||
                !groupOf.TryGetValue(resource, out var envReference))
            {
                continue;
            }

            var annotation = resource.Annotations.OfType<RadiusSecretStoresAnnotation>().FirstOrDefault();
            if (annotation is null)
            {
                continue;
            }

            foreach (var consumer in annotation.Consumers)
            {
                if (byName.TryGetValue(consumer.Store.Name, out var storeResource) &&
                    groupOf.TryGetValue(storeResource, out var storeReference) &&
                    !string.Equals(envReference.Group, storeReference.Group, StringComparison.Ordinal))
                {
                    edges.Add((envReference.Group, storeReference.Group));
                }
            }
        }

        return edges;
    }
}

/// <summary>
/// The resolved contents of a single Radius resource group: the environments and resources
/// routed to it, plus the deterministic first-declared environment used as the inert
/// <c>--environment</c> default at deploy time (FR-011).
/// </summary>
internal sealed record RadiusGroupPartition(
    string Group,
    IReadOnlyList<RadiusEnvironmentResource> Environments,
    IReadOnlyList<IResource> Resources,
    string? FirstDeclaredEnvironmentName);

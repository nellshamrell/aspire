// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.ResourceGroups;

/// <summary>
/// Per-group emission context handed to a group-scoped
/// <c>RadiusInfrastructureBuilder</c>. Carries the group's identity, the set of
/// resource names routed to it (the emission scope, FR-007), the whole-model
/// resource → group map (so cross-group <c>WithReference</c> targets can be
/// rendered as full UCP IDs, FR-004), and — when the group's application deploys
/// against an environment owned by another group — the environment's full UCP ID
/// (FR-005).
/// </summary>
internal sealed record RadiusGroupContext
{
    /// <summary>The group this builder emits.</summary>
    public required string Group { get; init; }

    /// <summary>Names of the resources routed to <see cref="Group"/> (the emission scope).</summary>
    public required IReadOnlySet<string> ResourceNames { get; init; }

    /// <summary>Resolved group assignment for every routed resource, keyed by resource name.</summary>
    public required IReadOnlyDictionary<string, RadiusResourceGroupReference> ReferenceByResourceName { get; init; }

    /// <summary>
    /// The full UCP ID of the environment this group's application deploys against when that
    /// environment is owned by a different group (FR-005); <see langword="null"/> when the
    /// group owns its environment and the bare in-group reference is emitted.
    /// </summary>
    public string? CrossGroupEnvironmentId { get; init; }
}

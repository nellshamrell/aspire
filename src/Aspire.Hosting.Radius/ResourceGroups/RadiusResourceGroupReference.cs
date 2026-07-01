// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.ResourceGroups;

/// <summary>
/// Immutable resolved group assignment for a resource. Encapsulates the logic
/// that turns a "group + optional environment-group" pair into the
/// fully-qualified UCP identifiers the Bicep AST and the <c>rad</c> CLI need
/// when a reference or environment target crosses group boundaries
/// (FR-004, FR-005).
/// </summary>
internal sealed record RadiusResourceGroupReference
{
    internal RadiusResourceGroupReference(string group, string? environmentGroup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        Group = group;
        EnvironmentGroup = string.IsNullOrWhiteSpace(environmentGroup) ? group : environmentGroup!;
    }

    /// <summary>The owning group of the resource / application.</summary>
    public string Group { get; }

    /// <summary>
    /// The group that owns the environment this resource deploys against
    /// (defaults to <see cref="Group"/>).
    /// </summary>
    public string EnvironmentGroup { get; }

    /// <summary>
    /// <see langword="true"/> when the environment this resource deploys against
    /// lives in a different group than the resource itself, requiring a
    /// full-UCP-ID <c>properties.environment</c> (FR-005).
    /// </summary>
    public bool IsCrossGroupEnvironment => !string.Equals(EnvironmentGroup, Group, StringComparison.Ordinal);

    /// <summary>
    /// Returns the fully-qualified UCP resource ID for a resource of
    /// <paramref name="resourceType"/> named <paramref name="name"/> in this
    /// reference's <see cref="Group"/> (FR-004), e.g.
    /// <c>/planes/radius/local/resourceGroups/&lt;group&gt;/providers/&lt;type&gt;/&lt;name&gt;</c>.
    /// </summary>
    public string ToUcpResourceId(string resourceType, string name) =>
        ToUcpResourceId(Group, resourceType, name);

    /// <summary>
    /// Returns the fully-qualified UCP ID of the <c>Applications.Core/environments</c>
    /// resource named <paramref name="environmentName"/> in this reference's
    /// <see cref="EnvironmentGroup"/> (FR-005).
    /// </summary>
    public string ToUcpEnvironmentId(string environmentName) =>
        ToUcpResourceId(EnvironmentGroup, "Applications.Core/environments", environmentName);

    /// <summary>
    /// Builds a fully-qualified UCP resource ID for an arbitrary group / type / name.
    /// </summary>
    internal static string ToUcpResourceId(string group, string resourceType, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return $"/planes/radius/local/resourceGroups/{group}/providers/{resourceType}/{name}";
    }
}

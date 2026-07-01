// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

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

    /// <summary>
    /// The maximum length of a Radius resource-group name, matching the UCP/Azure
    /// Resource Manager resource-group limit.
    /// </summary>
    internal const int MaxNameLength = 90;

    // Windows reserved device names. A resource-group name is used verbatim as a
    // `groups/<group>/` artifact directory, so a name like `CON` or `NUL` (with or
    // without an extension, e.g. `CON.bicep`) cannot be materialized on Windows even
    // though Radius/UCP itself would accept it. Matching is case-insensitive.
    private static readonly HashSet<string> s_reservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// Validates that <paramref name="name"/> is a safe Radius resource-group name.
    /// This is the single source of truth for group-name validation and is called
    /// from both the public <c>WithRadiusResourceGroup</c> overloads and the
    /// orchestrator, so routing set through internal annotations cannot bypass it.
    /// </summary>
    /// <remarks>
    /// A group name is used verbatim both as a filesystem path segment (the per-group
    /// <c>groups/&lt;group&gt;/</c> artifact directory) and as a segment of the Radius
    /// UCP resource ID (<c>/planes/radius/local/resourceGroups/&lt;group&gt;/...</c>).
    /// The allowed grammar mirrors UCP/Azure Resource Manager resource-group names:
    /// 1-90 characters of ASCII letters, digits, <c>-</c>, <c>_</c>, and <c>.</c>, not
    /// starting or ending with <c>.</c>, and with no consecutive <c>.</c>. This is not
    /// stricter than what Radius accepts, except that Windows reserved device names are
    /// additionally rejected so the artifact directory can be created on every platform.
    /// </remarks>
    internal static bool IsValidName([NotNullWhen(true)] string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return false;
        }

        // "." and ".." are relative-path tokens; a leading/trailing '.' is also disallowed
        // by ARM/UCP and would produce a surprising directory segment.
        if (name is "." or ".." || name[0] == '.' || name[^1] == '.')
        {
            return false;
        }

        if (name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.'))
            {
                return false;
            }
        }

        // A reserved device name is reserved regardless of any extension, so compare the
        // base name before the first '.' (e.g. `CON.bicep` -> `CON`).
        var dotIndex = name.IndexOf('.', StringComparison.Ordinal);
        var baseName = dotIndex < 0 ? name : name[..dotIndex];
        return !s_reservedDeviceNames.Contains(baseName);
    }
}

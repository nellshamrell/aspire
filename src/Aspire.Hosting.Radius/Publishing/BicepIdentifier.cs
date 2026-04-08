// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Utility for sanitizing resource names to valid Bicep identifiers.
/// </summary>
internal static class BicepIdentifier
{
    private static readonly HashSet<string> s_reservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "radius", "resource", "module", "param", "var", "output", "type",
        "import", "targetScope", "metadata", "extension", "provider"
    };

    /// <summary>
    /// Sanitizes a resource name for use as a Bicep identifier.
    /// Handles collisions with the <c>extension radius</c> directive and reserved words.
    /// </summary>
    public static string Sanitize(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Replace hyphens and dots with underscores for identifier use
        var sanitized = name.Replace('-', '_').Replace('.', '_');

        // Handle collision with 'radius' (used by extension radius directive)
        if (string.Equals(sanitized, "radius", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = "radiusenv";
        }
        else if (s_reservedWords.Contains(sanitized))
        {
            sanitized = $"{sanitized}_res";
        }

        // Ensure starts with letter
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
        {
            sanitized = $"r{sanitized}";
        }

        return sanitized;
    }

    /// <summary>
    /// Quotes a name for use as a Bicep property key (e.g., connection names with hyphens).
    /// </summary>
    public static string QuotePropertyName(string name)
    {
        // If the name contains characters invalid for bare identifiers, quote it
        if (name.Contains('-') || name.Contains('.') || name.Contains(' '))
        {
            return $"'{name}'";
        }
        return name;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Utility for sanitizing resource names into valid Bicep identifiers.
/// </summary>
internal static partial class BicepIdentifier
{
    // Reserved Bicep keywords and extension names that cannot be used as identifiers.
    private static readonly HashSet<string> s_reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "radius", "resource", "param", "var", "output", "module",
        "import", "metadata", "targetScope", "existing", "type",
        "true", "false", "null", "if", "for", "in"
    };

    /// <summary>
    /// Sanitizes a resource name into a valid Bicep identifier.
    /// </summary>
    /// <param name="name">The resource name to sanitize.</param>
    /// <returns>A valid Bicep identifier.</returns>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "resource_";
        }

        // Replace non-alphanumeric characters (except underscore) with underscores
        var sanitized = NonAlphaNumericRegex().Replace(name, "_");

        // If name starts with a digit, prefix with 'r'
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "r" + sanitized;
        }

        // Handle reserved identifier collisions
        if (s_reserved.Contains(sanitized))
        {
            sanitized = sanitized + "env";
        }

        return sanitized;
    }

    [GeneratedRegex("[^a-zA-Z0-9_]")]
    private static partial Regex NonAlphaNumericRegex();
}

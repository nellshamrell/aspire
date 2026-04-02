// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Utility for sanitizing identifiers for use in Bicep templates.
/// </summary>
internal static partial class BicepIdentifier
{
    private static readonly HashSet<string> s_reservedWords =
    [
        "true", "false", "null", "if", "else", "for", "in",
        "var", "param", "output", "resource", "module", "import",
        "existing", "type", "targetScope", "metadata", "func",
        "extension", "radius",
    ];

    /// <summary>
    /// Sanitizes a name for use as a Bicep identifier.
    /// Replaces invalid characters with underscores and resolves collisions with reserved words.
    /// </summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "_empty";
        }

        // Replace hyphens and other invalid chars with underscores
        var sanitized = InvalidChars().Replace(name, "_");

        // Ensure it starts with a letter or underscore
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        // Resolve reserved word collisions
        if (s_reservedWords.Contains(sanitized))
        {
            sanitized += "Res";
        }

        return sanitized;
    }

    [GeneratedRegex("[^a-zA-Z0-9_]")]
    private static partial Regex InvalidChars();
}

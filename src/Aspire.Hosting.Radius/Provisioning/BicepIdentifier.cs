// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.RegularExpressions;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Utilities for creating valid Bicep identifiers from resource names.
/// </summary>
internal static partial class BicepIdentifier
{
    private static readonly HashSet<string> s_reservedWords =
    [
        "if", "else", "true", "false", "null", "resource", "module", "param",
        "var", "output", "for", "in", "existing", "import", "as", "type",
        "with", "metadata", "targetScope", "extension", "func", "assert",
        "radius", "environment", "application"
    ];

    /// <summary>
    /// Sanitizes a resource name to produce a valid Bicep identifier.
    /// </summary>
    /// <param name="name">The resource name to sanitize.</param>
    /// <returns>A valid Bicep identifier.</returns>
    public static string Sanitize(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Replace non-alphanumeric characters with underscores
        var sanitized = InvalidCharsRegex().Replace(name, "_");

        // Ensure it starts with a letter or underscore
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
        {
            sanitized = "_" + sanitized;
        }

        // Remove leading/trailing underscores that look odd
        sanitized = sanitized.Trim('_');

        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "resource";
        }

        // Avoid reserved words by appending a suffix
        if (s_reservedWords.Contains(sanitized))
        {
            sanitized += "_res";
        }

        return sanitized;
    }

    [GeneratedRegex("[^a-zA-Z0-9_]")]
    private static partial Regex InvalidCharsRegex();
}

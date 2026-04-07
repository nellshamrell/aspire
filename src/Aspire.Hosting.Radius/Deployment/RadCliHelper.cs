// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Helper for detecting and invoking the Radius CLI (<c>rad</c>).
/// </summary>
internal static class RadCliHelper
{
    private const string RadCliName = "rad";
    private const string RadCliDownloadUrl = "https://docs.radapp.io/installation/";

    /// <summary>
    /// Checks whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    /// <returns><see langword="true"/> if <c>rad</c> is found; otherwise <see langword="false"/>.</returns>
    public static bool IsRadCliAvailable()
    {
        return GetRadCliPathOrDefault() is not null;
    }

    /// <summary>
    /// Returns the full path to the <c>rad</c> executable.
    /// </summary>
    /// <returns>The full path to the <c>rad</c> executable.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the <c>rad</c> CLI is not found on PATH.</exception>
    public static string GetRadCliPath()
    {
        var path = GetRadCliPathOrDefault();

        if (path is null)
        {
            throw new InvalidOperationException(
                $"The Radius CLI ('{RadCliName}') was not found on your PATH. " +
                $"Install it from {RadCliDownloadUrl} and ensure it is available in your terminal.");
        }

        return path;
    }

    /// <summary>
    /// Constructs the arguments for a <c>rad deploy</c> command.
    /// </summary>
    /// <param name="bicepFilePath">The path to the Bicep file to deploy.</param>
    /// <param name="outputFormat">The output format (default is <c>"json"</c>).</param>
    /// <returns>The command line arguments string.</returns>
    public static string ConstructDeployCommand(string bicepFilePath, string outputFormat = "json")
    {
        return $"deploy \"{bicepFilePath}\" --output {outputFormat}";
    }

    private static string? GetRadCliPathOrDefault()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat" }
            : Array.Empty<string>();

        foreach (var directory in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            // Check exact name (Unix) or with extensions (Windows)
            var candidatePath = Path.Combine(directory, RadCliName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }

            foreach (var ext in extensions)
            {
                var candidateWithExt = Path.Combine(directory, RadCliName + ext);
                if (File.Exists(candidateWithExt))
                {
                    return candidateWithExt;
                }
            }
        }

        return null;
    }
}

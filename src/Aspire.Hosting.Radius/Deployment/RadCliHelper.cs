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
    private const string DownloadUrl = "https://docs.radapp.io/installation/";

    /// <summary>
    /// Checks whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    /// <returns><c>true</c> if <c>rad</c> is found; otherwise <c>false</c>.</returns>
    public static bool IsRadCliAvailable()
    {
        try
        {
            var path = GetRadCliPathOrDefault();
            return path is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the full path to the <c>rad</c> executable, or throws if not found.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when <c>rad</c> is not on PATH.</exception>
    public static string GetRadCliPath()
    {
        var path = GetRadCliPathOrDefault();
        if (path is null)
        {
            throw new InvalidOperationException(
                $"The Radius CLI ('rad') was not found on your PATH. " +
                $"Install it from {DownloadUrl} and ensure it is accessible from your terminal.");
        }
        return path;
    }

    /// <summary>
    /// Constructs the <c>rad deploy</c> command-line arguments for the given Bicep file.
    /// </summary>
    /// <param name="bicepFilePath">Absolute path to the <c>app.bicep</c> file.</param>
    /// <param name="outputFormat">Output format (default: <c>json</c>).</param>
    /// <returns>The full argument string for the <c>rad deploy</c> command.</returns>
    public static string ConstructDeployCommand(string bicepFilePath, string outputFormat = "json")
    {
        ArgumentException.ThrowIfNullOrEmpty(bicepFilePath);
        return $"deploy \"{bicepFilePath}\" --output {outputFormat}";
    }

    /// <summary>
    /// Gets the helpful error message shown when the <c>rad</c> CLI is missing.
    /// </summary>
    public static string GetNotFoundMessage()
    {
        return $"The Radius CLI ('rad') was not found on your PATH. " +
               $"Install it from {DownloadUrl} and ensure it is accessible from your terminal.";
    }

    /// <summary>
    /// Returns the full path to the <c>rad</c> executable, or <c>null</c> if not found.
    /// </summary>
    private static string? GetRadCliPathOrDefault()
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
        {
            return null;
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { ".exe", ".cmd", ".bat", "" }
            : new[] { "" };

        foreach (var directory in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                var fullPath = Path.Combine(directory, RadCliName + ext);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Helper for detecting and invoking the <c>rad</c> CLI.
/// </summary>
internal static class RadCliHelper
{
    private const string RadExecutableName = "rad";
    private const string RadInstallUrl = "https://docs.radapp.io/installation/";

    /// <summary>
    /// Checks whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    /// <returns><c>true</c> if <c>rad</c> is found on PATH; otherwise <c>false</c>.</returns>
    public static bool IsRadCliAvailable()
    {
        try
        {
            var path = FindExecutableOnPath(GetPlatformExecutableName());
            return path is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the full path to the <c>rad</c> executable.
    /// </summary>
    /// <returns>The absolute path to the <c>rad</c> executable.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>rad</c> is not found on PATH.</exception>
    public static string GetRadCliPath()
    {
        var path = FindExecutableOnPath(GetPlatformExecutableName());

        if (path is null)
        {
            throw new InvalidOperationException(
                $"The 'rad' CLI was not found on the system PATH. " +
                $"The Radius CLI is required for deployment. " +
                $"Install it from: {RadInstallUrl}");
        }

        return path;
    }

    /// <summary>
    /// Constructs the command-line arguments for <c>rad deploy</c>.
    /// </summary>
    /// <param name="bicepFilePath">The full path to the Bicep file to deploy.</param>
    /// <param name="outputFormat">The output format for progress reporting. Pass <c>null</c> to omit.</param>
    /// <returns>The command-line argument string for <c>rad deploy</c>.</returns>
    public static string ConstructDeployCommand(string bicepFilePath, string? outputFormat = "json")
    {
        ArgumentException.ThrowIfNullOrEmpty(bicepFilePath);

        var args = $"deploy {bicepFilePath}";

        if (!string.IsNullOrEmpty(outputFormat))
        {
            args += $" --output {outputFormat}";
        }

        return args;
    }

    private static string GetPlatformExecutableName()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? $"{RadExecutableName}.exe"
            : RadExecutableName;
    }

    private static string? FindExecutableOnPath(string executableName)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';

        foreach (var directory in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(directory.Trim(), executableName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}

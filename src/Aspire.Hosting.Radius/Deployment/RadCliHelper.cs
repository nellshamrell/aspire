// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Helper for detecting and invoking the Radius CLI (<c>rad</c>).
/// </summary>
internal static class RadCliHelper
{
    /// <summary>
    /// The error message displayed when the <c>rad</c> CLI is not found.
    /// </summary>
    public const string RadCliNotFoundMessage =
        "The 'rad' CLI was not found on the system PATH. " +
        "Install it from https://docs.radapp.io/installation/ and ensure it is available on your PATH.";

    private static readonly string s_radExecutableName =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "rad.exe" : "rad";

    /// <summary>
    /// Checks whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    /// <returns><c>true</c> if <c>rad</c> is found; otherwise <c>false</c>.</returns>
    public static bool IsRadCliAvailable()
    {
        return GetRadCliPath() is not null;
    }

    /// <summary>
    /// Gets the full path to the <c>rad</c> executable, or <c>null</c> if not found.
    /// </summary>
    /// <returns>The full path to <c>rad</c>, or <c>null</c>.</returns>
    public static string? GetRadCliPath()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
        {
            return null;
        }

        var separator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
        foreach (var directory in pathVar.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(directory.Trim(), s_radExecutableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the full path to the <c>rad</c> executable, or throws if not found.
    /// </summary>
    /// <returns>The full path to <c>rad</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>rad</c> is not on PATH.</exception>
    public static string GetRequiredRadCliPath()
    {
        return GetRadCliPath()
            ?? throw new InvalidOperationException(RadCliNotFoundMessage);
    }

    /// <summary>
    /// Constructs the command-line arguments for <c>rad deploy</c>.
    /// </summary>
    /// <param name="bicepFilePath">The absolute path to the Bicep file to deploy.</param>
    /// <param name="outputFormat">The output format (default: <c>json</c>).</param>
    /// <returns>The command-line argument string.</returns>
    public static string ConstructDeployCommand(string bicepFilePath, string outputFormat = "json")
    {
        ArgumentException.ThrowIfNullOrEmpty(bicepFilePath);

        // Quote the path to handle spaces
        return $"deploy \"{bicepFilePath}\" --output {outputFormat}";
    }

    /// <summary>
    /// Runs <c>rad deploy</c> for the given Bicep file and streams output to the provided callbacks.
    /// </summary>
    /// <param name="radCliPath">Full path to the <c>rad</c> executable.</param>
    /// <param name="bicepFilePath">The absolute path to the Bicep file.</param>
    /// <param name="onOutput">Callback for standard output lines.</param>
    /// <param name="onError">Callback for standard error lines.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The process exit code.</returns>
    public static async Task<int> RunDeployAsync(
        string radCliPath,
        string bicepFilePath,
        Action<string>? onOutput = null,
        Action<string>? onError = null,
        CancellationToken cancellationToken = default)
    {
        var args = ConstructDeployCommand(bicepFilePath);

        var psi = new ProcessStartInfo
        {
            FileName = radCliPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        // Stream stdout
        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                onOutput?.Invoke(line);
            }
        }, cancellationToken);

        // Stream stderr
        var stderrTask = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                onError?.Invoke(line);
            }
        }, cancellationToken);

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        return process.ExitCode;
    }
}

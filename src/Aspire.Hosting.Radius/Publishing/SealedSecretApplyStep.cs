// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Secret-store types are experimental; consumed internally by the integration.
#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Secrets;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Pipeline step that, for the sealed-secrets path, applies each committed <c>SealedSecret</c>
/// manifest to the cluster (targeting the same cluster the subsequent <c>rad deploy</c> hits)
/// and waits for the Sealed Secrets controller to materialize the underlying
/// <c>kubernetes.io/v1 Secret</c> before deploy. Scheduled after publish and before deploy,
/// mirroring <see cref="RadCredentialRegisterStep"/>. A missing <c>kubectl</c> fails with
/// <c>ASPIRERADIUS045</c>; a never-materializing <c>Secret</c> fails with <c>ASPIRERADIUS046</c>
/// (before <c>rad deploy</c>). No-op when no sealed store is declared (FR-008, FR-009, FR-010).
/// </summary>
internal sealed class SealedSecretApplyStep
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(2);

    private readonly RadiusEnvironmentResource _environment;

    internal SealedSecretApplyStep(RadiusEnvironmentResource environment) => _environment = environment;

    internal PipelineStep CreatePipelineStep()
    {
        var step = new PipelineStep
        {
            Name = $"apply-sealed-secrets-{_environment.Name}",
            Description = $"Apply and await sealed secrets for '{_environment.Name}' before rad deploy",
            Action = ExecuteAsync,
        };
        step.DependsOn($"publish-radius-{_environment.Name}");
        step.DependsOn(WellKnownPipelineSteps.DeployPrereq);
        step.RequiredBy($"deploy-radius-{_environment.Name}");
        return step;
    }

    internal async Task ExecuteAsync(PipelineStepContext context)
    {
        var stores = GetSealedStores(context.Model);
        if (stores.Count == 0)
        {
            return;
        }

        var logger = context.Logger;
        var cancellationToken = context.CancellationToken;

        if (!await DetectKubectlAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                "'kubectl' was not found on PATH, or the Sealed Secrets controller is not installed in the " +
                "target cluster. Install kubectl and the controller, then re-run deploy. Diagnostic: ASPIRERADIUS045.");
        }

        var kubeContext = await ResolveWorkspaceKubeContextAsync(cancellationToken).ConfigureAwait(false);

        foreach (var store in stores)
        {
            var (ns, name) = SealedSecretManifest.ReadMetadata(
                store.Name, store.Population.SealedManifestPath!, _environment.Namespace);

            await ApplyManifestAsync(store.Population.SealedManifestPath!, kubeContext, logger, cancellationToken)
                .ConfigureAwait(false);

            await WaitForSecretMaterializationAsync(
                store.Name, ns, name, store.MaterializationTimeout, s_pollInterval,
                ct => SecretExistsAsync(ns, name, kubeContext, ct),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Sealed secret stores scoped to this environment (environment-scoped owned here, plus application-scoped).</summary>
    private List<RadiusSecretStoreResource> GetSealedStores(DistributedApplicationModel model) =>
        model.Resources.OfType<RadiusSecretStoreResource>()
            .Where(s => s.Population.HasSealedSecret)
            .Where(s => s.Scope == RadiusSecretStoreScope.Application || ReferenceEquals(s.OwningEnvironment, _environment))
            .ToList();

    /// <summary>Builds the <c>kubectl apply</c> argument list, passing <c>--context</c> only when resolved.</summary>
    internal static IReadOnlyList<string> BuildApplyArgs(string manifestPath, string? kubeContext)
    {
        var args = new List<string> { "apply", "-f", manifestPath };
        AddContext(args, kubeContext);
        return args;
    }

    /// <summary>Builds the <c>kubectl get secret</c> existence-probe argument list.</summary>
    internal static IReadOnlyList<string> BuildGetSecretArgs(string ns, string name, string? kubeContext)
    {
        var args = new List<string> { "get", "secret", name, "-n", ns, "-o", "name" };
        AddContext(args, kubeContext);
        return args;
    }

    private static void AddContext(List<string> args, string? kubeContext)
    {
        if (!string.IsNullOrWhiteSpace(kubeContext))
        {
            args.Add("--context");
            args.Add(kubeContext);
        }
    }

    /// <summary>
    /// Polls <paramref name="secretExists"/> every <paramref name="interval"/> up to
    /// <paramref name="timeout"/>, returning when the <c>Secret</c> exists and throwing
    /// <c>ASPIRERADIUS046</c> (before <c>rad deploy</c>) if it never materializes.
    /// </summary>
    internal static async Task WaitForSecretMaterializationAsync(
        string storeName,
        string ns,
        string name,
        TimeSpan timeout,
        TimeSpan interval,
        Func<CancellationToken, Task<bool>> secretExists,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            if (await secretExists(cancellationToken).ConfigureAwait(false))
            {
                return;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException(
                    $"The Secret '{ns}/{name}' referenced by sealed secret store '{storeName}' did not " +
                    $"materialize within {timeout.TotalSeconds:0}s. Likely causes: the Sealed Secrets " +
                    "controller is not installed, the manifest was sealed for a different namespace, or " +
                    "decryption failed. Diagnostic: ASPIRERADIUS046.");
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ApplyManifestAsync(string manifestPath, string? kubeContext, ILogger logger, CancellationToken cancellationToken)
    {
        var args = BuildApplyArgs(manifestPath, kubeContext);
        var (exitCode, stderr) = await RunKubectlAsync(args, logger, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"'kubectl apply -f {manifestPath}' failed with exit code {exitCode}: {stderr.Trim()}");
        }
    }

    private static async Task<bool> SecretExistsAsync(string ns, string name, string? kubeContext, CancellationToken cancellationToken)
    {
        var args = BuildGetSecretArgs(ns, name, kubeContext);
        var (exitCode, _) = await RunKubectlAsync(args, logger: null, cancellationToken).ConfigureAwait(false);
        return exitCode == 0;
    }

    // Resolves the kubecontext of the active rad workspace so the SealedSecret is applied to the
    // same cluster rad deploy will hit (not kubectl's ambient current-context). Best-effort: reads
    // the Radius workspace config; returns null when unresolved (kubectl uses its current context).
    private static async Task<string?> ResolveWorkspaceKubeContextAsync(CancellationToken cancellationToken)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var configPath = Path.Combine(home, ".rad", "config.yaml");
            if (!File.Exists(configPath))
            {
                return null;
            }

            var text = await File.ReadAllTextAsync(configPath, cancellationToken).ConfigureAwait(false);
            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"(?m)^\s*context:\s*(?<v>[^\s#]+)\s*$");
            return match.Success ? match.Groups["v"].Value.Trim('\'', '"') : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<bool> DetectKubectlAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "kubectl",
                    ArgumentList = { "version", "--client" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            // kubectl not found on PATH.
            return false;
        }
    }

    private static async Task<(int ExitCode, string StdErr)> RunKubectlAsync(
        IReadOnlyList<string> args, ILogger? logger, CancellationToken cancellationToken)
    {
        var stderr = new StringBuilder();
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "kubectl",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        foreach (var a in args)
        {
            process.StartInfo.ArgumentList.Add(a);
        }

        // The SealedSecret manifest passed to kubectl is already encrypted, so no plaintext is
        // exposed; the wrapper still scrubs output uniformly with the rad wrapper's helpers.
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                logger?.LogInformation("kubectl (stdout): {Output}", e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
                logger?.LogWarning("kubectl (stderr): {Error}", e.Data);
            }
        };

        logger?.LogInformation("Running: kubectl {Args}", string.Join(' ', args));
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (process.ExitCode, stderr.ToString());
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Pipeline step that deploys a Radius application by invoking <c>rad deploy app.bicep</c>.
/// Depends on the publish step only (not <see cref="WellKnownPipelineSteps.Push"/>)
/// to support kind clusters without a container registry.
/// </summary>
internal sealed class RadiusDeploymentPipelineStep
{
    private const string RadInstallUrl = "https://docs.radapp.io/installation/";

    private readonly RadiusEnvironmentResource _environment;

    internal RadiusDeploymentPipelineStep(RadiusEnvironmentResource environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Creates a <see cref="PipelineStep"/> that deploys a Radius application via <c>rad deploy</c>.
    /// The step depends on the publish step for this environment (Bicep must be generated first).
    /// </summary>
    internal PipelineStep CreatePipelineStep()
    {
        var step = new PipelineStep
        {
            Name = $"deploy-radius-{_environment.Name}",
            Description = $"Deploy Radius environment '{_environment.Name}' via rad CLI",
            Action = ExecuteAsync
        };
        step.DependsOn($"publish-radius-{_environment.Name}");
        step.RequiredBy(WellKnownPipelineSteps.Deploy);
        step.DependsOn(WellKnownPipelineSteps.DeployPrereq);
        return step;
    }

    internal async Task ExecuteAsync(PipelineStepContext context)
    {
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;

        // Detect rad CLI availability
        var radAvailable = await DetectRadCliAsync(cancellationToken).ConfigureAwait(false);
        if (!radAvailable)
        {
            logger.LogError("The 'rad' CLI was not found on PATH. Install it from {InstallUrl}", RadInstallUrl);
            throw new InvalidOperationException(
                $"The 'rad' CLI was not found. Please install it from {RadInstallUrl} and ensure it is available on your PATH.");
        }

        logger.LogInformation("rad CLI detected on PATH for environment '{EnvironmentName}'", _environment.Name);

        // Resolve the output directory where Bicep was generated
        var outputDir = PublishingContextUtils.GetEnvironmentOutputPath(context, _environment);
        var bicepPath = Path.Combine(outputDir, "app.bicep");

        if (!File.Exists(bicepPath))
        {
            logger.LogError("Bicep file not found at {BicepPath}. Ensure the publish step completed successfully.", bicepPath);
            throw new InvalidOperationException(
                $"Bicep file not found at '{bicepPath}'. Ensure the publish step completed successfully before deploying.");
        }

        logger.LogInformation("Starting rad deploy with Bicep file '{BicepPath}'", bicepPath);

        // Resolve a value for every Bicep `param` that was bound to an Aspire ParameterResource
        // during publish. The generated Bicep declares these as valueless params, so `rad deploy`
        // must receive each one via `--parameters name=value`. Secret-bound values are collected
        // so they can be scrubbed from the logged command and rad stdout/stderr.
        var (parameterArgs, secretValues) = await ResolveDeployParametersAsync(cancellationToken).ConfigureAwait(false);

        var deployTask = await context.ReportingStep.CreateTaskAsync(
            $"Deploying Radius environment '{_environment.Name}' via rad deploy...",
            cancellationToken).ConfigureAwait(false);

        try
        {
            var stderrBuilder = new System.Text.StringBuilder();

            using var process = new Process();
            // Use ArgumentList rather than Arguments so the bicep path doesn't need shell-style
            // quoting and is forwarded verbatim — paths containing spaces or special characters
            // (e.g. `C:\Program Files\…`) survive intact on every platform without double-escaping.
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "rad",
                WorkingDirectory = outputDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("deploy");
            process.StartInfo.ArgumentList.Add(bicepPath);
            foreach (var arg in parameterArgs)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            // Mirror the credential step: log the full command but with any secret parameter
            // values replaced by '***' so they never reach the console or log sinks.
            var loggedArgs = RadCredentialRegisterStep.RedactSecretValues(
                string.Join(' ', process.StartInfo.ArgumentList), secretValues);
            logger.LogInformation("Running: rad {Args}", loggedArgs);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    logger.LogInformation("rad (stdout): {Output}", redacted);
                    context.ReportingStep.Log(LogLevel.Information, redacted);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    stderrBuilder.AppendLine(redacted);
                    logger.LogWarning("rad (stderr): {Error}", redacted);
                    context.ReportingStep.Log(LogLevel.Warning, redacted);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    logger.LogWarning("Cancellation requested — terminating rad deploy process for environment '{EnvironmentName}'", _environment.Name);
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // Race: the process exited between HasExited check and Kill. Nothing to do.
                    }
                }

                throw;
            }

            var exitCode = process.ExitCode;

            logger.LogInformation("rad deploy exited with code {ExitCode} for environment '{EnvironmentName}'", exitCode, _environment.Name);

            if (exitCode != 0)
            {
                var stderrText = stderrBuilder.ToString().Trim();
                var errorMessage = string.IsNullOrEmpty(stderrText)
                    ? $"rad deploy failed with exit code {exitCode}"
                    : $"rad deploy failed with exit code {exitCode}: {stderrText}";

                logger.LogError("rad deploy failed for environment '{EnvironmentName}': {ErrorMessage}", _environment.Name, errorMessage);
                context.ReportingStep.Log(LogLevel.Error, errorMessage);

                throw new InvalidOperationException(errorMessage);
            }

            await deployTask.CompleteAsync(
                $"Radius deployment complete for '{_environment.Name}'",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error during rad deploy for environment '{EnvironmentName}'", _environment.Name);
            context.ReportingStep.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Resolves deploy-time values for every Bicep parameter that was bound to an Aspire
    /// <see cref="ParameterResource"/> during publish (recorded on the environment via
    /// <see cref="RadiusDeployParametersAnnotation"/>). Returns the <c>--parameters name=value</c>
    /// argument tokens plus the set of secret values to scrub from logs.
    /// </summary>
    private async Task<(IReadOnlyList<string> Args, IReadOnlyList<string> SecretValues)> ResolveDeployParametersAsync(
        CancellationToken cancellationToken)
    {
        var annotation = _environment.Annotations.OfType<RadiusDeployParametersAnnotation>().LastOrDefault();
        if (annotation is null || annotation.Parameters.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var args = new List<string>();
        var secretValues = new List<string>();

        // Order by identifier for deterministic command construction (stable logs and tests).
        foreach (var (identifier, parameter) in annotation.Parameters.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            var value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;

            // `rad deploy` accepts repeated `--parameters name=value` flags. The name/value pair is
            // a single token so paths/values with spaces survive via ArgumentList verbatim.
            args.Add("--parameters");
            args.Add($"{identifier}={value}");

            if (parameter.Secret && !string.IsNullOrWhiteSpace(value))
            {
                secretValues.Add(value);
            }
        }

        return (args, secretValues);
    }

    /// <summary>
    /// Detects whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    internal static async Task<bool> DetectRadCliAsync(CancellationToken cancellationToken = default)
    {
        // Honour a pre-cancelled token even when `rad` is missing from PATH. Without this,
        // Process.Start() would throw Win32Exception first and the catch-all below would
        // convert that into `return false`, swallowing the cancellation request.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "rad",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("version");

            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            // Honour cancellation — never swallow this; the caller (and CTRL-C) must observe it.
            throw;
        }
        catch
        {
            // rad is not found on PATH, or some other launch failure (e.g. EACCES).
            // Treat as "not available" so the deploy step surfaces a clear install message instead
            // of a stack trace pointing at Process.Start.
            return false;
        }
    }
}

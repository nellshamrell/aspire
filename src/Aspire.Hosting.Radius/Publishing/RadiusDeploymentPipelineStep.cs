// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable CA1305

using System.Diagnostics;
using System.Text;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Deploy pipeline step that executes <c>rad deploy app.bicep</c> against the Radius environment.
/// </summary>
internal sealed class RadiusDeploymentPipelineStep
{
    private const string RadCliDownloadUrl = "https://docs.radapp.io/getting-started/";

    private readonly RadiusEnvironmentResource _environment;

    public RadiusDeploymentPipelineStep(RadiusEnvironmentResource environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Creates the deploy pipeline step for this environment.
    /// </summary>
    public PipelineStep CreateStep(string publishStepName)
    {
        var step = new PipelineStep
        {
            Name = $"deploy-radius-{_environment.Name}",
            Description = $"Deploys the Radius application for environment {_environment.Name}.",
            Action = DeployAsync
        };
        step.DependsOn(publishStepName);
        step.DependsOn(WellKnownPipelineSteps.Push);
        step.RequiredBy(WellKnownPipelineSteps.Deploy);

        return step;
    }

    private async Task DeployAsync(PipelineStepContext context)
    {
        var logger = context.Logger;
        var cancellationToken = context.CancellationToken;

        // T054: Check for rad CLI availability (FR-016)
        var radPath = FindRadCli();
        if (radPath is null)
        {
            throw new InvalidOperationException(
                $"The Radius CLI ('rad') was not found on the system PATH. " +
                $"The 'rad' CLI is required to deploy applications to a Radius environment. " +
                $"Install it from: {RadCliDownloadUrl}");
        }

        logger.LogInformation("Found Radius CLI at '{RadPath}'.", radPath);

        // Determine output path where app.bicep was written
        var outputPath = PublishingContextUtils.GetEnvironmentOutputPath(context, _environment);
        var bicepPath = Path.Combine(outputPath, "app.bicep");

        if (!File.Exists(bicepPath))
        {
            throw new InvalidOperationException(
                $"Bicep template not found at '{bicepPath}'. " +
                $"Ensure the publish step completed successfully before deploy.");
        }

        // T055: Execute rad deploy with progress streaming (FR-017)
        var deployTask = await context.ReportingStep
            .CreateTaskAsync($"Deploying to Radius environment '{_environment.Name}'...", cancellationToken)
            .ConfigureAwait(false);

        await using (deployTask.ConfigureAwait(false))
        {
            try
            {
                var exitCode = await ExecuteRadDeployAsync(
                    radPath,
                    bicepPath,
                    _environment.Name,
                    logger,
                    deployTask,
                    cancellationToken).ConfigureAwait(false);

                if (exitCode != 0)
                {
                    await deployTask.CompleteAsync(
                        $"Deployment failed with exit code {exitCode}. Check the output above for details.",
                        CompletionState.CompletedWithError,
                        cancellationToken).ConfigureAwait(false);

                    throw new InvalidOperationException(
                        $"'rad deploy' exited with code {exitCode}. " +
                        $"Check the Radius CLI output for error details and remediation steps.");
                }

                await deployTask.CompleteAsync(
                    "Deployment completed successfully.",
                    CompletionState.Completed,
                    cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Radius deployment completed for environment '{EnvironmentName}'.", _environment.Name);
            }
            catch (OperationCanceledException)
            {
                await deployTask.CompleteAsync(
                    "Deployment was cancelled.",
                    CompletionState.CompletedWithError,
                    cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
    }

    /// <summary>
    /// Finds the <c>rad</c> CLI executable on the system PATH.
    /// </summary>
    internal static string? FindRadCli()
    {
        var fileName = OperatingSystem.IsWindows() ? "rad.exe" : "rad";

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null)
        {
            return null;
        }

        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        foreach (var dir in pathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var fullPath = Path.Combine(dir, fileName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static async Task<int> ExecuteRadDeployAsync(
        string radPath,
        string bicepPath,
        string environmentName,
        ILogger logger,
        IReportingTask reportingTask,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = radPath,
            Arguments = $"deploy \"{bicepPath}\" --environment {environmentName} --output json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        var lastUpdate = DateTimeOffset.UtcNow;

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            outputBuilder.AppendLine(e.Data);
            logger.LogInformation("[rad] {Output}", e.Data);

            // Update progress every 5 seconds (FR-017)
            var now = DateTimeOffset.UtcNow;
            if (now - lastUpdate >= TimeSpan.FromSeconds(5))
            {
                lastUpdate = now;
                _ = reportingTask.UpdateAsync($"Deploying... {e.Data}");
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                return;
            }

            errorBuilder.AppendLine(e.Data);
            logger.LogWarning("[rad stderr] {Error}", e.Data);
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
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Process may have already exited
            }

            throw;
        }

        if (errorBuilder.Length > 0)
        {
            logger.LogWarning("Radius CLI stderr output:\n{StdErr}", errorBuilder.ToString());
        }

        return process.ExitCode;
    }
}

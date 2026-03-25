// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIRECOMPUTE002

using System.Diagnostics;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Pipeline step that orchestrates Radius deployment via <c>rad deploy</c>.
/// Generates the Bicep template and then invokes the <c>rad</c> CLI to deploy it.
/// </summary>
internal static class RadiusDeploymentPipelineStep
{
    /// <summary>
    /// Executes the deployment step: writes Bicep to disk and runs <c>rad deploy</c>.
    /// </summary>
    internal static async Task ExecuteAsync(PipelineStepContext context, RadiusEnvironmentResource environment)
    {
        var logger = context.Logger;
        var cancellationToken = context.CancellationToken;

        logger.LogInformation("Starting Radius deployment for environment '{EnvironmentName}'", environment.Name);

        // 1. Get output path and generate Bicep
        var outputPath = PublishingContextUtils.GetEnvironmentOutputPath(context, environment);
        var bicepFilePath = Path.Combine(outputPath, "app.bicep");

        // The publish step should have already written the Bicep file.
        // If not, generate it now as a safety net.
        if (!File.Exists(bicepFilePath))
        {
            logger.LogInformation("Bicep file not found at '{BicepFilePath}', generating now...", bicepFilePath);

            var publishingContext = new RadiusBicepPublishingContext(
                context.ExecutionContext,
                outputPath,
                logger,
                cancellationToken);

            await publishingContext.WriteModelAsync(context.Model, environment, environment.ConfigureInfrastructureCallback).ConfigureAwait(false);
        }

        if (!File.Exists(bicepFilePath))
        {
            throw new InvalidOperationException(
                $"Bicep file was not generated at '{bicepFilePath}'. " +
                "Ensure the application model contains resources to deploy.");
        }

        // 2. Verify rad CLI is available
        if (!RadCliHelper.IsRadCliAvailable())
        {
            throw new InvalidOperationException(
                "The 'rad' CLI is required for deployment but was not found on the system PATH. " +
                $"Install it from: https://docs.radapp.io/installation/");
        }

        var radPath = RadCliHelper.GetRadCliPath();
        var deployArgs = RadCliHelper.ConstructDeployCommand(bicepFilePath);

        logger.LogInformation("Executing: {RadPath} {DeployArgs}", radPath, deployArgs);

        // 3. Execute rad deploy and stream progress
        await RunRadDeployAsync(radPath, deployArgs, logger, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Radius deployment completed for environment '{EnvironmentName}'", environment.Name);

        context.Summary.Add("🚀 Radius Deploy", $"Deployed to environment '{environment.Name}'");
        context.Summary.Add("📄 Bicep Template", bicepFilePath);
    }

    private static async Task RunRadDeployAsync(
        string radPath,
        string arguments,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = radPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        var outputLines = new List<string>();
        var errorLines = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (string.IsNullOrEmpty(e.Data))
            {
                return;
            }

            outputLines.Add(e.Data);

            var progress = RadDeploymentProgress.ParseProgressEvent(e.Data);
            if (progress is not null)
            {
                logger.LogInformation("{ProgressMessage}", progress.ToDisplayString());
            }
            else
            {
                logger.LogInformation("[rad] {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorLines.Add(e.Data);
                logger.LogWarning("[rad stderr] {Error}", e.Data);
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
                process.Kill(entireProcessTree: true);
            }
            throw;
        }

        if (process.ExitCode != 0)
        {
            var errorOutput = string.Join(Environment.NewLine, errorLines);
            var stdOutput = string.Join(Environment.NewLine, outputLines);

            throw new InvalidOperationException(
                $"'rad deploy' failed with exit code {process.ExitCode}." +
                (string.IsNullOrEmpty(errorOutput) ? "" : $"{Environment.NewLine}Error output:{Environment.NewLine}{errorOutput}") +
                (string.IsNullOrEmpty(stdOutput) ? "" : $"{Environment.NewLine}Standard output:{Environment.NewLine}{stdOutput}") +
                $"{Environment.NewLine}{Environment.NewLine}Troubleshooting:" +
                $"{Environment.NewLine}  - Verify your Kubernetes cluster is running: kubectl cluster-info" +
                $"{Environment.NewLine}  - Verify Radius is installed: rad env list" +
                $"{Environment.NewLine}  - Check the Bicep file for errors" +
                $"{Environment.NewLine}  - See https://docs.radapp.io/ for documentation");
        }
    }
}

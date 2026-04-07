// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPIPELINES004 // IPipelineOutputService is for evaluation purposes only

using System.Globalization;
using System.Text;
using Aspire.Hosting.Dcp.Process;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Provides pipeline step actions for deploying Radius applications using the <c>rad</c> CLI.
/// </summary>
internal static class RadiusDeploymentPipelineStep
{
    /// <summary>
    /// Validates that the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    internal static async Task ValidateRadCliAsync(PipelineStepContext context)
    {
        var task = await context.ReportingStep.CreateTaskAsync(
            "Checking for Radius CLI (rad)...",
            context.CancellationToken).ConfigureAwait(false);
        await using (task.ConfigureAwait(false))
        {
            if (!RadCliHelper.IsRadCliAvailable())
            {
                await task.FailAsync(
                    "The Radius CLI (rad) was not found. Install it from https://docs.radapp.io/installation/",
                    context.CancellationToken).ConfigureAwait(false);

                throw new InvalidOperationException(
                    "The Radius CLI ('rad') was not found on your PATH. " +
                    "Install it from https://docs.radapp.io/installation/ and ensure it is available in your terminal.");
            }

            await task.CompleteAsync(
                $"Radius CLI found at {RadCliHelper.GetRadCliPath()}",
                CompletionState.Completed,
                context.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Deploys the generated Bicep file using <c>rad deploy</c>.
    /// </summary>
    internal static async Task DeployAsync(PipelineStepContext context)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        var outputDir = outputService.GetOutputDirectory();
        var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aspire.Hosting.Radius.Deployment");

        // Find the environment name from the model
        var environment = context.Model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        var environmentName = environment?.Name ?? "radius";

        var bicepFilePath = Path.Combine(outputDir, environmentName, "app.bicep");

        if (!File.Exists(bicepFilePath))
        {
            throw new InvalidOperationException(
                $"Bicep file not found at '{bicepFilePath}'. " +
                "Ensure the publish step completed successfully before deploying.");
        }

        var deployTask = await context.ReportingStep.CreateTaskAsync(
            new MarkdownString($"Deploying to Radius environment **{environmentName}**..."),
            context.CancellationToken).ConfigureAwait(false);
        await using (deployTask.ConfigureAwait(false))
        {
            try
            {
                var radPath = RadCliHelper.GetRadCliPath();
                var arguments = RadCliHelper.ConstructDeployCommand(bicepFilePath);

                logger.LogInformation("Executing: {RadPath} {Arguments}", radPath, arguments);

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                var lastStatusUpdate = DateTimeOffset.UtcNow;

                var spec = new ProcessSpec(radPath)
                {
                    Arguments = arguments,
                    WorkingDirectory = outputDir,
                    ThrowOnNonZeroReturnCode = false,
                    InheritEnv = true,
                    OnOutputData = output =>
                    {
                        logger.LogDebug("rad deploy (stdout): {Output}", output);

                        var progressEvent = RadDeploymentProgress.TryParseEvent(output);
                        if (progressEvent is not null)
                        {
                            var formatted = RadDeploymentProgress.FormatEvent(progressEvent);
                            outputBuilder.AppendLine(formatted);

                            // Update progress at least every 5 seconds (SC-011)
                            var now = DateTimeOffset.UtcNow;
                            if (now - lastStatusUpdate >= TimeSpan.FromSeconds(5))
                            {
                                lastStatusUpdate = now;
                                _ = deployTask.UpdateAsync(
                                    new MarkdownString(formatted),
                                    context.CancellationToken);
                            }
                        }
                    },
                    OnErrorData = error =>
                    {
                        logger.LogWarning("rad deploy (stderr): {Error}", error);
                        errorBuilder.AppendLine(error);
                    }
                };

                var (pendingResult, processDisposable) = ProcessUtil.Run(spec);

                await using (processDisposable.ConfigureAwait(false))
                {
                    var processResult = await pendingResult
                        .WaitAsync(context.CancellationToken)
                        .ConfigureAwait(false);

                    if (processResult.ExitCode != 0)
                    {
                        var errorMessage = errorBuilder.Length > 0
                            ? errorBuilder.ToString().Trim()
                            : $"rad deploy exited with code {processResult.ExitCode.ToString(CultureInfo.InvariantCulture)}";

                        await deployTask.FailAsync(
                            $"Deployment failed: {errorMessage}",
                            context.CancellationToken).ConfigureAwait(false);

                        throw new InvalidOperationException(
                            $"rad deploy failed with exit code {processResult.ExitCode.ToString(CultureInfo.InvariantCulture)}. " +
                            $"Error: {errorMessage}. " +
                            "Check the Radius CLI output above for details. " +
                            "Common issues: Radius not initialized on the cluster (run 'rad init'), " +
                            "Kubernetes not reachable, or invalid Bicep configuration.");
                    }

                    // Add deployment summary info
                    context.Summary.Add("☸ Target", "Radius");
                    context.Summary.Add("📦 Environment", environmentName);
                    context.Summary.Add("📄 Bicep", bicepFilePath);

                    await deployTask.CompleteAsync(
                        new MarkdownString($"Successfully deployed **{environmentName}** to Radius"),
                        CompletionState.Completed,
                        context.CancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                await deployTask.CompleteAsync(
                    $"Deployment failed: {ex.Message}",
                    CompletionState.CompletedWithError,
                    context.CancellationToken).ConfigureAwait(false);
                throw;
            }
        }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Pipeline API is experimental
#pragma warning disable ASPIREPIPELINES004 // IPipelineOutputService is experimental

using System.Diagnostics;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Pipeline step that deploys a Radius application using <c>rad deploy</c>.
/// Registered via <see cref="PipelineStepAnnotation"/> on the <see cref="RadiusEnvironmentResource"/>.
/// </summary>
internal static class RadiusDeploymentPipelineStep
{
    /// <summary>
    /// Creates the deploy pipeline step for a Radius environment resource.
    /// </summary>
    /// <param name="resource">The Radius environment resource to deploy.</param>
    /// <returns>A <see cref="PipelineStep"/> that runs <c>rad deploy</c>.</returns>
    public static PipelineStep Create(RadiusEnvironmentResource resource)
    {
        return new PipelineStep
        {
            Name = $"radius-deploy-{resource.Name}",
            Description = $"Deploy Radius application for environment '{resource.EnvironmentName}' via rad CLI",
            Action = ctx => ExecuteAsync(ctx, resource),
            // T049a: Do NOT depend on Push — kind clusters don't need a registry.
            // The deploy step depends only on publish (which generates Bicep).
            DependsOnSteps = [WellKnownPipelineSteps.Publish],
            RequiredBySteps = [WellKnownPipelineSteps.Deploy],
            Resource = resource,
        };
    }

    private static async Task ExecuteAsync(PipelineStepContext ctx, RadiusEnvironmentResource resource)
    {
        var logger = ctx.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Aspire.Hosting.Radius.Deployment");
        var outputService = ctx.Services.GetRequiredService<IPipelineOutputService>();

        // Determine the directory containing app.bicep
        var environments = ctx.Model.Resources.OfType<RadiusEnvironmentResource>().ToList();
        string outputDir;
        if (environments.Count > 1)
        {
            outputDir = outputService.GetOutputDirectory(resource);
        }
        else
        {
            outputDir = outputService.GetOutputDirectory();
        }

        var bicepPath = Path.Combine(outputDir, "app.bicep");

        if (!File.Exists(bicepPath))
        {
            throw new InvalidOperationException(
                $"Bicep template not found at '{bicepPath}'. " +
                "Ensure the publish step completed successfully before deploy.");
        }

        // Fail fast if rad is not available
        if (!RadCliHelper.IsRadCliAvailable())
        {
            throw new InvalidOperationException(RadCliHelper.GetNotFoundMessage());
        }

        var radPath = RadCliHelper.GetRadCliPath();
        var args = RadCliHelper.ConstructDeployCommand(bicepPath);

        logger.LogInformation("Deploying Radius application: {RadPath} {Args}", radPath, args);

        var progress = new RadDeploymentProgress(logger);

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = radPath,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();

        // Stream stdout (JSON progress events)
        var stdoutTask = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync(ctx.CancellationToken).ConfigureAwait(false) is { } line)
            {
                progress.ProcessLine(line);
            }
        }, ctx.CancellationToken);

        // Capture stderr for error reporting
        var stderrTask = process.StandardError.ReadToEndAsync(ctx.CancellationToken);

        await process.WaitForExitAsync(ctx.CancellationToken).ConfigureAwait(false);
        await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var errorMessage = string.IsNullOrWhiteSpace(stderr) ? "Unknown error" : stderr.Trim();
            throw new InvalidOperationException(
                $"'rad deploy' failed with exit code {process.ExitCode} for environment '{resource.EnvironmentName}'.\n" +
                $"Error: {errorMessage}\n\n" +
                "Troubleshooting:\n" +
                "  1. Verify your Kubernetes cluster is running: kubectl cluster-info\n" +
                "  2. Verify Radius is installed: rad version\n" +
                "  3. Check Radius environment exists: rad env list\n" +
                $"  4. Review the Bicep template: {bicepPath}");
        }

        logger.LogInformation("Radius deployment completed successfully for environment '{EnvironmentName}'", resource.EnvironmentName);

        ctx.PipelineContext.Summary.Add(
            $"🚀 Radius ({resource.EnvironmentName})",
            $"Deployed to namespace '{resource.Namespace}'");
    }
}

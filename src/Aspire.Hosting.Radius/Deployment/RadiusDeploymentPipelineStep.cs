// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004
#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Creates the deploy pipeline steps for Radius.
/// The deploy pipeline:
///   1. Generates <c>app.bicep</c> and <c>bicepconfig.json</c> (publish step, already registered).
///   2. Verifies the <c>rad</c> CLI is available.
///   3. Executes <c>rad deploy app.bicep --output json</c> and streams progress.
/// </summary>
internal static class RadiusDeploymentPipelineStep
{
    /// <summary>
    /// Creates a <see cref="PipelineStep"/> that executes <c>rad deploy</c> for the given Radius environment.
    /// </summary>
    /// <param name="environmentName">The name of the Radius environment resource.</param>
    /// <param name="publishStepName">The name of the Radius publish step that generates the Bicep file.</param>
    /// <returns>A configured pipeline step.</returns>
    public static PipelineStep Create(string environmentName, string publishStepName)
    {
        var step = new PipelineStep
        {
            Name = $"deploy-radius-{environmentName}",
            Description = $"Deploy Radius application for environment '{environmentName}' using rad CLI",
            Action = context => ExecuteAsync(context, environmentName),
        };

        // Deploy after publish (which generates the Bicep).
        // For kind/local clusters, images are loaded via `kind load` — no registry push needed.
        // When a registry is configured, push steps are added separately via the pipeline.
        step.DependsOn(publishStepName);
        step.RequiredBy(WellKnownPipelineSteps.Deploy);

        return step;
    }

    private static async Task ExecuteAsync(PipelineStepContext context, string environmentName)
    {
        var logger = context.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(RadiusDeploymentPipelineStep));

        // 1. Locate the rad CLI
        var radPath = RadCliHelper.GetRadCliPath();
        if (radPath is null)
        {
            logger.LogError("{Message}", RadCliHelper.RadCliNotFoundMessage);
            throw new InvalidOperationException(RadCliHelper.RadCliNotFoundMessage);
        }

        logger.LogInformation("Using rad CLI at {RadPath}", radPath);

        // 2. Locate the Bicep file in the publish output directory
        var environment = context.Model.Resources
            .OfType<RadiusEnvironmentResource>()
            .FirstOrDefault(env => string.Equals(env.Name, environmentName, StringComparison.Ordinal));

        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        var outputDir = environment is not null && context.Model.Resources.OfType<IComputeEnvironmentResource>().Count() > 1
            ? outputService.GetOutputDirectory(environment)
            : outputService.GetOutputDirectory();
        var bicepPath = Path.Combine(outputDir, "app.bicep");

        if (!File.Exists(bicepPath))
        {
            var msg = $"Bicep file not found at '{bicepPath}'. Ensure the Radius publish step ran successfully.";
            logger.LogError("{Message}", msg);
            throw new InvalidOperationException(msg);
        }

        // 3. Execute rad deploy
        logger.LogInformation(
            "Starting Radius deployment for environment '{EnvironmentName}'",
            environment?.Name ?? environmentName);

        logger.LogInformation("Executing: rad {Command}", RadCliHelper.ConstructDeployCommand(bicepPath));

        var progress = new RadDeploymentProgress(logger);

        var exitCode = await RadCliHelper.RunDeployAsync(
            radPath,
            bicepPath,
            onOutput: progress.ProcessOutputLine,
            onError: progress.ProcessErrorLine,
            cancellationToken: context.CancellationToken).ConfigureAwait(false);

        if (exitCode != 0)
        {
            var msg = $"rad deploy failed with exit code {exitCode}. " +
                      "Check the output above for details. Common fixes:\n" +
                      "  - Verify the Kubernetes cluster is running: kubectl cluster-info\n" +
                      "  - Verify Radius is installed: rad env list\n" +
                      "  - Check controller logs: kubectl logs -n radius-system -l app=radius-controller";
            logger.LogError("{Message}", msg);
            throw new InvalidOperationException(msg);
        }

        logger.LogInformation(
            "Radius deployment completed for environment '{EnvironmentName}'",
            environment?.Name ?? environmentName);

        context.Summary.Add("🚀 Radius Deploy", $"Deployed via rad CLI ({bicepPath})");
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Provisioning;
using Azure.Provisioning;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Context for generating Bicep templates from the distributed application model
/// for Radius deployment. Uses the Azure Provisioning AST for mutable, inspectable
/// Bicep generation.
/// </summary>
internal sealed class RadiusBicepPublishingContext(
    DistributedApplicationExecutionContext executionContext,
    string outputPath,
    ILogger logger,
    CancellationToken cancellationToken = default)
{
    internal string OutputPath { get; } = outputPath;

    private const string BicepConfigContent = """
        {
            "experimentalFeaturesEnabled": {
                "extensibility": true
            },
            "extensions": {
                "radius": "br:biceptypes.azurecr.io/radius:latest",
                "aws": "br:biceptypes.azurecr.io/aws:latest"
            }
        }
        """;

    /// <summary>
    /// Writes the Bicep template for the given model and Radius environment to the output directory.
    /// </summary>
    internal async Task WriteModelAsync(
        DistributedApplicationModel model,
        RadiusEnvironmentResource environment,
        Action<Infrastructure>? configureInfrastructure = null)
    {
        if (!executionContext.IsPublishMode)
        {
            return;
        }

        logger.LogInformation("Starting Radius Bicep generation for environment '{EnvironmentName}'", environment.Name);

        ArgumentNullException.ThrowIfNull(model);

        if (model.Resources.Count == 0)
        {
            logger.LogWarning("No resources found in the application model. Skipping Bicep generation.");
            return;
        }

        var bicepContent = GenerateBicep(model, environment, configureInfrastructure);

        Directory.CreateDirectory(OutputPath);

        // Write bicepconfig.json to enable the Radius Bicep extension
        var bicepConfigPath = Path.Combine(OutputPath, "bicepconfig.json");
        await File.WriteAllTextAsync(bicepConfigPath, BicepConfigContent, cancellationToken).ConfigureAwait(false);

        var bicepFilePath = Path.Combine(OutputPath, "app.bicep");
        await File.WriteAllTextAsync(bicepFilePath, bicepContent, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Radius Bicep template written to '{BicepFilePath}'", bicepFilePath);
    }

    /// <summary>
    /// Generates the Bicep template content from the application model using the
    /// Azure Provisioning AST.
    /// </summary>
    internal string GenerateBicep(
        DistributedApplicationModel model,
        RadiusEnvironmentResource environment,
        Action<Infrastructure>? configureInfrastructure = null)
    {
        configureInfrastructure ??= environment.ConfigureInfrastructureCallback;

        var builder = new RadiusInfrastructureBuilder(logger);
        return builder.GenerateBicep(model, environment, configureInfrastructure);
    }
}

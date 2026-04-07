// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Provisioning;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Orchestrates Bicep generation for Radius publishing by delegating to
/// <see cref="RadiusInfrastructureBuilder"/> for Azure Provisioning SDK-based AST compilation.
/// </summary>
internal sealed class RadiusBicepPublishingContext
{
    private readonly DistributedApplicationModel _model;
    private readonly string _outputDirectory;
    private readonly ILogger _logger;

    public RadiusBicepPublishingContext(
        DistributedApplicationModel model,
        string outputDirectory,
        ILogger logger)
    {
        _model = model;
        _outputDirectory = outputDirectory;
        _logger = logger;
    }

    /// <summary>
    /// Generates Bicep templates for all Radius environments in the model.
    /// </summary>
    /// <param name="configureCallback">Optional AST customization callback.</param>
    public async Task GenerateBicepAsync(Action<RadiusInfrastructureOptions>? configureCallback = null)
    {
        var radiusEnvironments = _model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        if (radiusEnvironments.Length == 0)
        {
            _logger.LogWarning("No Radius environments found in the application model. Skipping Bicep generation.");
            return;
        }

        Directory.CreateDirectory(_outputDirectory);

        foreach (var environment in radiusEnvironments)
        {
            var outputDir = radiusEnvironments.Length > 1
                ? Path.Combine(_outputDirectory, environment.Name)
                : _outputDirectory;

            Directory.CreateDirectory(outputDir);

            _logger.LogInformation(
                "Generating Bicep for Radius environment '{Name}' in '{OutputDir}'.",
                environment.Name,
                outputDir);

            var builder = new RadiusInfrastructureBuilder(_model, environment, _logger);
            var bicep = builder.Build(configureCallback);

            var bicepPath = Path.Combine(outputDir, "app.bicep");
            await File.WriteAllTextAsync(bicepPath, bicep).ConfigureAwait(false);
            _logger.LogInformation("Wrote Bicep template to '{BicepPath}'.", bicepPath);

            // Generate bicepconfig.json to register the Radius extension
            var configPath = Path.Combine(outputDir, "bicepconfig.json");
            await WriteBicepConfigAsync(configPath).ConfigureAwait(false);
            _logger.LogInformation("Wrote Bicep config to '{ConfigPath}'.", configPath);
        }
    }

    private static async Task WriteBicepConfigAsync(string path)
    {
        const string bicepConfig = """
            {
              "experimentalFeaturesEnabled": {
                "extensibility": true
              },
              "extensions": {
                "radius": "br:biceptypes.azurecr.io/radius:latest"
              }
            }
            """;

        await File.WriteAllTextAsync(path, bicepConfig).ConfigureAwait(false);
    }
}

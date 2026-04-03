// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Orchestrates Radius Bicep template generation from an Aspire distributed application model.
/// </summary>
internal sealed class RadiusBicepPublishingContext
{
    private readonly DistributedApplicationModel _model;
    private readonly ILogger _logger;
    private readonly Action<RadiusInfrastructureOptions>? _configureCallback;
    private readonly RadiusEnvironmentResource? _targetEnvironment;

    public RadiusBicepPublishingContext(
        DistributedApplicationModel model,
        ILogger logger,
        Action<RadiusInfrastructureOptions>? configureCallback = null,
        RadiusEnvironmentResource? targetEnvironment = null)
    {
        _model = model;
        _logger = logger;
        _configureCallback = configureCallback;
        _targetEnvironment = targetEnvironment;
    }

    /// <summary>
    /// Generates Bicep templates. When a target environment is set, generates only for that
    /// environment. Otherwise generates for all Radius environments in the model.
    /// </summary>
    public Dictionary<string, string> GenerateBicep()
    {
        var results = new Dictionary<string, string>();
        var allEnvironments = _model.Resources.OfType<RadiusEnvironmentResource>().ToList();

        if (allEnvironments.Count == 0)
        {
            _logger.LogWarning("No RadiusEnvironmentResource found in app model. Skipping Bicep generation.");
            return results;
        }

        var firstEnvironment = allEnvironments[0];

        // Scope to the target environment if specified, otherwise generate for all
        var environments = _targetEnvironment is not null
            ? [_targetEnvironment]
            : allEnvironments;

        foreach (var env in environments)
        {
            _logger.LogInformation("Generating Bicep for Radius environment '{EnvironmentName}'", env.EnvironmentName);

            ValidateResources();

            var builder = new RadiusInfrastructureBuilder(_model, env, _logger);
            builder.ClassifyResources(firstEnvironment);

            // Allow user to customize before generation
            if (_configureCallback is not null)
            {
                var options = new RadiusInfrastructureOptions
                {
                    EnvironmentName = builder.EnvironmentName,
                    ApplicationName = builder.ApplicationName,
                    Namespace = builder.Namespace,
                    PortableResources = builder.PortableResources,
                    ContainerResources = builder.ContainerResources,
                };

                _configureCallback(options);

                // Apply changes back
                builder.EnvironmentName = options.EnvironmentName;
                builder.ApplicationName = options.ApplicationName;
                builder.Namespace = options.Namespace;
            }

            var bicep = builder.Build();

            _logger.LogInformation(
                "Generated Bicep for '{EnvironmentName}': {PortableCount} portable resources, {ContainerCount} containers",
                env.EnvironmentName,
                builder.PortableResources.Count,
                builder.ContainerResources.Count);

            results[env.EnvironmentName] = bicep;
        }

        return results;
    }

    private void ValidateResources()
    {
        foreach (var resource in _model.Resources)
        {
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            var customization = resource.Annotations
                .OfType<Annotations.RadiusResourceCustomizationAnnotation>()
                .FirstOrDefault()?.Customization;

            if (customization is null)
            {
                continue;
            }

            // T034/T040: validate manual provisioning has required fields
            if (customization.Provisioning == RadiusResourceProvisioning.Manual)
            {
                if (string.IsNullOrEmpty(customization.Host))
                {
                    throw new InvalidOperationException(
                        $"Resource '{resource.Name}' is configured for manual provisioning but 'Host' is not set. " +
                        "Provide a host address via PublishAsRadiusResource(config => config.Host = \"...\").");
                }

                if (customization.Port is null)
                {
                    throw new InvalidOperationException(
                        $"Resource '{resource.Name}' is configured for manual provisioning but 'Port' is not set. " +
                        "Provide a port via PublishAsRadiusResource(config => config.Port = 1234).");
                }
            }
        }
    }
}

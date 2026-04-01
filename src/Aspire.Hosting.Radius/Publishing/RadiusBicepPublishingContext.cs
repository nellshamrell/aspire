// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Provisioning;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Orchestrates Radius Bicep generation from a <see cref="DistributedApplicationModel"/>.
/// </summary>
internal sealed class RadiusBicepPublishingContext
{
    private readonly DistributedApplicationModel _model;
    private readonly ILogger _logger;

    public RadiusBicepPublishingContext(DistributedApplicationModel model, ILogger logger)
    {
        _model = model;
        _logger = logger;
    }

    /// <summary>
    /// Generates the Bicep content for the Radius deployment.
    /// </summary>
    /// <returns>The generated Bicep content string.</returns>
    public Task<string> GenerateBicepAsync()
    {
        var builder = new RadiusInfrastructureBuilder(_model, _logger);

        // 1. Build the AST from the app model
        builder.Build();

        // 2. Invoke user's ConfigureRadiusInfrastructure callbacks (if registered)
        InvokeConfigurationCallbacks(builder);

        // 3. Compile AST to Bicep string
        var bicep = builder.Compile();

        return Task.FromResult(bicep);
    }

    /// <summary>
    /// Generates the companion <c>bicepconfig.json</c> content.
    /// </summary>
    public static string GenerateBicepConfig()
    {
        return """
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
    }

    private void InvokeConfigurationCallbacks(RadiusInfrastructureBuilder builder)
    {
        var environments = _model.Resources.OfType<RadiusEnvironmentResource>();

        foreach (var env in environments)
        {
            var callbacks = env.Annotations
                .OfType<RadiusInfrastructureConfigurationAnnotation>();

            foreach (var callback in callbacks)
            {
                var options = new RadiusInfrastructureOptions(builder);

                try
                {
                    callback.Configure(options);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "ConfigureRadiusInfrastructure callback for environment '{EnvironmentName}' failed.",
                        env.Name);
                    throw;
                }
            }
        }
    }
}

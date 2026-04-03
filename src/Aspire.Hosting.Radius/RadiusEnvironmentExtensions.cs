// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // Type is for evaluation purposes only
#pragma warning disable ASPIREPIPELINES001 // Pipeline API is experimental
#pragma warning disable ASPIREPIPELINES004 // IPipelineOutputService is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Radius environment resources to the application model.
/// </summary>
public static class RadiusEnvironmentExtensions
{
    /// <summary>
    /// Adds a Radius compute environment to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Radius environment resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExport("addRadiusEnvironment", Description = "Adds a Radius compute environment")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();

        var resource = new RadiusEnvironmentResource(name);

        var resourceBuilder = builder.AddResource(resource);

        // T039: Register the publish pipeline step
        resourceBuilder.WithAnnotation(new PipelineStepAnnotation((factoryContext) =>
        {
            if (factoryContext.Resource.IsExcludedFromPublish())
            {
                return [];
            }

            var publishStep = new PipelineStep
            {
                Name = $"radius-publish-{resource.Name}",
                Description = $"Generate Radius Bicep template for environment '{resource.EnvironmentName}'",
                Action = async ctx =>
                {
                    var model = ctx.Model;
                    var logger = ctx.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Aspire.Hosting.Radius.Publishing");
                    var outputService = ctx.Services.GetRequiredService<IPipelineOutputService>();

                    // Collect user configure callbacks
                    var configCallbacks = resource.Annotations
                        .OfType<RadiusInfrastructureConfigurationAnnotation>()
                        .Select(a => a.Configure)
                        .ToList();

                    Action<RadiusInfrastructureOptions>? combinedCallback = configCallbacks.Count > 0
                        ? opts => { foreach (var cb in configCallbacks) { cb(opts); } }
                        : null;

                    var context = new RadiusBicepPublishingContext(model, logger, combinedCallback, resource);
                    var bicepOutputs = context.GenerateBicep();

                    var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();

                    foreach (var (envName, bicep) in bicepOutputs)
                    {
                        // T042f: use per-resource directory for multi-env, shared root for single-env
                        string outputDir;
                        if (environments.Count > 1)
                        {
                            outputDir = outputService.GetOutputDirectory(resource);
                        }
                        else
                        {
                            outputDir = outputService.GetOutputDirectory();
                        }

                        Directory.CreateDirectory(outputDir);

                        var bicepPath = Path.Combine(outputDir, "app.bicep");
                        await File.WriteAllTextAsync(bicepPath, bicep, ctx.CancellationToken).ConfigureAwait(false);
                        logger.LogInformation("Wrote Radius Bicep template to '{BicepPath}'", bicepPath);

                        // Generate companion bicepconfig.json for Radius extension
                        var bicepConfigPath = Path.Combine(outputDir, "bicepconfig.json");
                        await File.WriteAllTextAsync(bicepConfigPath, GenerateBicepConfig(), ctx.CancellationToken).ConfigureAwait(false);
                        logger.LogInformation("Wrote bicepconfig.json to '{ConfigPath}'", bicepConfigPath);
                    }
                },
                Tags = [WellKnownPipelineTags.ProvisionInfrastructure],
                RequiredBySteps = [WellKnownPipelineSteps.Publish],
                DependsOnSteps = [WellKnownPipelineSteps.PublishPrereq],
                Resource = resource,
            };

            return [publishStep];
        }));

        // T049: Register the deploy pipeline step
        resourceBuilder.WithAnnotation(new PipelineStepAnnotation((factoryContext) =>
        {
            if (factoryContext.Resource.IsExcludedFromPublish())
            {
                return [];
            }

            return [RadiusDeploymentPipelineStep.Create(resource)];
        }));

        return resourceBuilder;
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the Radius environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="namespace">The Kubernetes namespace to deploy resources into.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExport("withRadiusNamespace", Description = "Sets the Kubernetes namespace for the Radius environment")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithRadiusNamespace(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        string @namespace)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(@namespace);

        builder.Resource.Namespace = @namespace;

        return builder;
    }

    /// <summary>
    /// Enables or disables the Radius dashboard container for this environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="enabled">Whether to enable the dashboard. Default is <c>true</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExport("withDashboard", Description = "Enables or disables the Radius dashboard")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.DashboardEnabled = enabled;

        return builder;
    }

    /// <summary>
    /// Configures the Radius infrastructure generation before Bicep compilation.
    /// Allows users to customize environment, application, and resource constructs.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="configure">An action to configure <see cref="RadiusInfrastructureOptions"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExportIgnore(Reason = "Action<RadiusInfrastructureOptions> callback is not ATS-compatible.")]
    public static IResourceBuilder<RadiusEnvironmentResource> ConfigureRadiusInfrastructure(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        Action<RadiusInfrastructureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.WithAnnotation(new RadiusInfrastructureConfigurationAnnotation(configure));

        return builder;
    }

    /// <summary>
    /// Configures a resource to be published as a Radius resource with custom provisioning options.
    /// </summary>
    /// <typeparam name="T">The type of resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">An action to configure <see cref="RadiusResourceCustomization"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Action<RadiusResourceCustomization> callback is not ATS-compatible.")]
    public static IResourceBuilder<T> PublishAsRadiusResource<T>(
        this IResourceBuilder<T> builder,
        Action<RadiusResourceCustomization> configure)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var customization = new RadiusResourceCustomization();
        configure(customization);

        builder.WithAnnotation(new RadiusResourceCustomizationAnnotation(customization));

        return builder;
    }

    /// <summary>
    /// Generates the bicepconfig.json content that registers the Radius Bicep extension.
    /// </summary>
    private static string GenerateBicepConfig()
    {
        return """
            {
              "experimentalFeaturesEnabled": {
                "extensibility": true
              },
              "extensions": {
                "radius": "br:biceptypes.azurecr.io/radius:latest"
              }
            }
            """;
    }
}

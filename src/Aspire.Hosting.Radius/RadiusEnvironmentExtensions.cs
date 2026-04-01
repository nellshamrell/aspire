// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for configuring Radius environments in Aspire.
/// </summary>
public static class RadiusEnvironmentExtensions
{
    /// <summary>
    /// Registers the Radius eventing subscriber idempotently.
    /// </summary>
    internal static IDistributedApplicationBuilder AddRadiusInfrastructureCore(
        this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();
        return builder;
    }

    /// <summary>
    /// Adds a Radius compute environment to the application model.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the Radius environment resource.</param>
    /// <returns>A resource builder for the Radius environment.</returns>
    [AspireExportIgnore(Reason = "Radius-specific extension — not ATS-compatible.")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.AddRadiusInfrastructureCore();

        var resource = new RadiusEnvironmentResource(name);

        var resourceBuilder = builder.AddResource(resource);

        // Register the Radius Bicep publishing pipeline step
        resource.Annotations.Add(new PipelineStepAnnotation(context =>
        {
            var step = new PipelineStep
            {
                Name = $"publish-radius-{name}",
                Description = $"Generate Radius Bicep template for environment '{name}'",
                Resource = context.Resource,
                Action = async stepContext =>
                {
                    var logger = stepContext.Services.GetRequiredService<ILoggerFactory>()
                        .CreateLogger<RadiusBicepPublishingContext>();

                    var publishingContext = new RadiusBicepPublishingContext(stepContext.Model, logger);
                    var bicep = await publishingContext.GenerateBicepAsync().ConfigureAwait(false);
                    var bicepConfig = RadiusBicepPublishingContext.GenerateBicepConfig();

                    var outputService = stepContext.Services.GetRequiredService<IPipelineOutputService>();
                    var outputDir = stepContext.Model.Resources.OfType<IComputeEnvironmentResource>().Count() > 1
                        ? outputService.GetOutputDirectory(resource)
                        : outputService.GetOutputDirectory();

                    Directory.CreateDirectory(outputDir);

                    var bicepPath = Path.Combine(outputDir, "app.bicep");
                    var configPath = Path.Combine(outputDir, "bicepconfig.json");

                    await File.WriteAllTextAsync(bicepPath, bicep, stepContext.CancellationToken).ConfigureAwait(false);
                    await File.WriteAllTextAsync(configPath, bicepConfig, stepContext.CancellationToken).ConfigureAwait(false);

                    stepContext.Logger.LogInformation("Radius Bicep template written to {BicepPath}", bicepPath);
                    stepContext.Summary.Add("📄 Radius Bicep", bicepPath);
                }
            };

            step.DependsOn(WellKnownPipelineSteps.PublishPrereq);
            step.RequiredBy(WellKnownPipelineSteps.Publish);

            return step;
        }));

        // Register the Radius deployment pipeline step (runs after publish + push)
        var publishStepName = $"publish-radius-{name}";
        resource.Annotations.Add(new PipelineStepAnnotation(context =>
        {
            var deployStep = RadiusDeploymentPipelineStep.Create(name, publishStepName);
            deployStep.Resource = context.Resource;
            return deployStep;
        }));

        return resourceBuilder;
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the Radius environment.
    /// </summary>
    /// <param name="builder">The resource builder for the Radius environment.</param>
    /// <param name="namespace">The Kubernetes namespace.</param>
    /// <returns>The resource builder for chaining.</returns>
    [AspireExportIgnore(Reason = "Radius-specific extension — not ATS-compatible.")]
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
    /// Configures whether the Radius dashboard container is started during <c>aspire run</c>.
    /// </summary>
    /// <param name="builder">The resource builder for the Radius environment.</param>
    /// <param name="enabled">Whether to enable the dashboard.</param>
    /// <returns>The resource builder for chaining.</returns>
    [AspireExportIgnore(Reason = "Radius-specific extension — not ATS-compatible.")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.DashboardEnabled = enabled;
        return builder;
    }

    /// <summary>
    /// Configures a resource to publish as a Radius resource with custom settings.
    /// Attaches a <see cref="RadiusResourceCustomizationAnnotation"/> to the resource.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">An action to configure the Radius resource customization.</param>
    /// <returns>The resource builder for chaining.</returns>
    [AspireExportIgnore(Reason = "Radius-specific extension — not ATS-compatible.")]
    public static IResourceBuilder<T> PublishAsRadiusResource<T>(
        this IResourceBuilder<T> builder,
        Action<RadiusResourceCustomization> configure)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var customization = new RadiusResourceCustomization();
        configure(customization);
        builder.Resource.Annotations.Add(new RadiusResourceCustomizationAnnotation(customization));
        return builder;
    }

    /// <summary>
    /// Configures the Radius infrastructure AST before Bicep compilation.
    /// Follows Aspire's <c>ConfigureInfrastructure</c> pattern for consistency.
    /// </summary>
    /// <param name="builder">The resource builder for the Radius environment.</param>
    /// <param name="configure">An action to customize the infrastructure options before Bicep compilation.</param>
    /// <returns>The resource builder for chaining.</returns>
    [AspireExportIgnore(Reason = "Radius-specific extension — not ATS-compatible.")]
    public static IResourceBuilder<RadiusEnvironmentResource> ConfigureRadiusInfrastructure(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        Action<RadiusInfrastructureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Resource.Annotations.Add(new RadiusInfrastructureConfigurationAnnotation(configure));
        return builder;
    }
}

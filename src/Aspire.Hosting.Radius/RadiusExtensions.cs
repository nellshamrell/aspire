// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using System.Text.RegularExpressions;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Publishing;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding and configuring Radius environment resources.
/// </summary>
public static partial class RadiusExtensions
{
    private const int KubernetesNamespaceMaxLength = 63;

    /// <summary>
    /// Adds a Radius compute environment to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Radius environment resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExportIgnore(Reason = "Radius extension — not part of the core Aspire ATS surface.")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();

        var resource = new RadiusEnvironmentResource(name);

        // Register the publish pipeline step for Bicep generation
        resource.Annotations.Add(new PipelineStepAnnotation(_ =>
        {
            var step = new PipelineStep
            {
                Name = $"publish-radius-{name}",
                Description = $"Publish Radius environment '{name}' as Bicep",
                Action = async stepContext =>
                {
                    var logger = stepContext.Logger;
                    var publishingContext = new RadiusBicepPublishingContext(resource, logger);
                    await publishingContext.ExecuteAsync(stepContext).ConfigureAwait(false);
                }
            };
            step.RequiredBy(WellKnownPipelineSteps.Publish);
            return step;
        }));

        // Register the deploy pipeline step for rad CLI deployment
        resource.Annotations.Add(new PipelineStepAnnotation(_ =>
        {
            var step = new PipelineStep
            {
                Name = $"deploy-radius-{name}",
                Description = $"Deploy Radius environment '{name}' via rad CLI",
                Action = async stepContext =>
                {
                    var deployStep = new RadiusDeploymentPipelineStep(resource, stepContext.Logger);
                    await deployStep.ExecuteAsync(stepContext).ConfigureAwait(false);
                }
            };
            step.DependsOn($"publish-radius-{name}");
            step.RequiredBy(WellKnownPipelineSteps.Deploy);
            step.DependsOn(WellKnownPipelineSteps.DeployPrereq);
            return step;
        }));

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the Radius environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="kubernetesNamespace">A valid RFC 1123 namespace name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the namespace is not a valid RFC 1123 label.</exception>
    [AspireExportIgnore(Reason = "Radius extension — not part of the core Aspire ATS surface.")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithNamespace(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        string kubernetesNamespace)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(kubernetesNamespace);

        if (kubernetesNamespace.Length > KubernetesNamespaceMaxLength || !DnsLabelPattern().IsMatch(kubernetesNamespace))
        {
            throw new ArgumentException(
                $"Kubernetes namespace '{kubernetesNamespace}' is invalid. " +
                "Must match RFC 1123: lowercase alphanumeric characters or hyphens, " +
                $"start and end with an alphanumeric character, and be at most {KubernetesNamespaceMaxLength} characters.",
                nameof(kubernetesNamespace));
        }

        builder.Resource.Namespace = kubernetesNamespace;
        return builder;
    }

    [GeneratedRegex("^[a-z0-9]([-a-z0-9]*[a-z0-9])?$")]
    private static partial Regex DnsLabelPattern();
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable ASPIREPIPELINES004 // IPipelineOutputService is for evaluation purposes only

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment resource that can host application resources.
/// </summary>
/// <remarks>
/// This resource models the Radius publishing environment used by Aspire when generating
/// Bicep templates for application resources that will be deployed to a Radius environment
/// on a Kubernetes cluster.
/// </remarks>
[AspireExport(ExposeProperties = true)]
public sealed class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Gets or sets the Kubernetes namespace where Radius resources will be deployed.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The name of the Radius environment.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
        Annotations.Add(new PipelineStepAnnotation(context =>
        {
            var step = new PipelineStep
            {
                Name = $"publish-{Name}",
                Description = $"Publishes the Radius environment configuration for {Name}.",
                Action = ctx => PublishAsync(ctx)
            };
            step.RequiredBy(WellKnownPipelineSteps.Publish);
            return step;
        }));
    }

    /// <inheritdoc/>
    [Experimental("ASPIRECOMPUTE002", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        var resource = endpointReference.Resource;

        // Use Kubernetes DNS convention for inter-service communication
        return ReferenceExpression.Create($"{resource.Name}.svc.cluster.local");
    }

    private static async Task PublishAsync(PipelineStepContext context)
    {
        var outputService = context.Services.GetRequiredService<IPipelineOutputService>();
        var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger<RadiusEnvironmentResource>();
        var outputDir = outputService.GetOutputDirectory();

        // Collect ConfigureRadiusInfrastructure callbacks from environment resources
        Action<RadiusInfrastructureOptions>? configureCallback = null;
        foreach (var envResource in context.Model.Resources.OfType<RadiusEnvironmentResource>())
        {
            foreach (var annotation in envResource.Annotations.OfType<RadiusInfrastructureConfigurationAnnotation>())
            {
                var existingCallback = configureCallback;
                var newCallback = annotation.Configure;
                configureCallback = existingCallback is null
                    ? newCallback
                    : options => { existingCallback(options); newCallback(options); };
            }
        }

        var publishingContext = new RadiusBicepPublishingContext(context.Model, outputDir, logger);
        await publishingContext.GenerateBicepAsync(configureCallback).ConfigureAwait(false);
    }
}

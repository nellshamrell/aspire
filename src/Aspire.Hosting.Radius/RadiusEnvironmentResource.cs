// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

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

    private static Task PublishAsync(PipelineStepContext context)
    {
        // Publishing implementation will be added in Phase 4 (T039)
        _ = context; // Will be used by the publishing implementation
        return Task.CompletedTask;
    }
}

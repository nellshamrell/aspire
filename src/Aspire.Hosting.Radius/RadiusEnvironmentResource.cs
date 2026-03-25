// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002 // IComputeEnvironmentResource is experimental
#pragma warning disable ASPIREPIPELINES001 // PipelineStepAnnotation is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment in the Aspire app model.
/// </summary>
public class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Gets or sets the Kubernetes namespace for resource deployment.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets whether the Radius dashboard container should be started during <c>aspire run</c>.
    /// </summary>
    public bool DashboardEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the endpoint reference for the Radius dashboard, if one is created.
    /// </summary>
    public EndpointReference? DashboardEndpoint { get; internal set; }

    /// <summary>
    /// Gets or sets the dashboard resource builder, if dashboard is enabled.
    /// </summary>
    internal IResourceBuilder<RadiusDashboardResource>? Dashboard { get; set; }

    /// <summary>
    /// Gets or sets the callback to customize the generated Radius infrastructure AST
    /// before it is compiled to Bicep. Set via <c>ConfigureRadiusInfrastructure()</c>.
    /// </summary>
    internal Action<Azure.Provisioning.Infrastructure>? ConfigureInfrastructureCallback { get; set; }

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
                Description = $"Publishes the Radius Bicep template for environment {Name}.",
                Action = ctx => PublishAsync(ctx)
            };
            step.RequiredBy(WellKnownPipelineSteps.Publish);
            return step;
        }));

        Annotations.Add(new PipelineStepAnnotation(context =>
        {
            var step = new PipelineStep
            {
                Name = $"deploy-{Name}",
                Description = $"Deploys the Radius application for environment {Name} via 'rad deploy'.",
                Action = ctx => RadiusDeploymentPipelineStep.ExecuteAsync(ctx, this),
                Tags = [WellKnownPipelineTags.DeployCompute]
            };
            step.DependsOn($"publish-{Name}");
            step.DependsOn(WellKnownPipelineSteps.Build);
            step.RequiredBy(WellKnownPipelineSteps.Deploy);
            return step;
        }));
    }

    private Task PublishAsync(PipelineStepContext context)
    {
        var outputPath = PublishingContextUtils.GetEnvironmentOutputPath(context, this);

        var publishingContext = new RadiusBicepPublishingContext(
            context.ExecutionContext,
            outputPath,
            context.Logger,
            context.CancellationToken);

        return publishingContext.WriteModelAsync(context.Model, this, ConfigureInfrastructureCallback);
    }

    /// <inheritdoc />
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        // Radius uses Kubernetes DNS for inter-service communication:
        // {resourceName}.{namespace}.svc.cluster.local
        return ReferenceExpression.Create($"{endpointReference.Resource.Name.ToLowerInvariant()}");
    }
}

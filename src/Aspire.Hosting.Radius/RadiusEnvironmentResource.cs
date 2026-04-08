// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius;

/// <summary>
/// Represents a Radius compute environment in the Aspire app model.
/// </summary>
public class RadiusEnvironmentResource : Resource, IComputeEnvironmentResource
{
    /// <summary>
    /// Gets or sets the Kubernetes namespace where Radius resources will be deployed.
    /// Defaults to <c>"default"</c>.
    /// </summary>
    public string Namespace { get; set; } = "default";

    /// <summary>
    /// Gets or sets the callback for user customization of the Radius infrastructure AST.
    /// Set via <see cref="RadiusEnvironmentExtensions.ConfigureRadiusInfrastructure"/>.
    /// </summary>
    internal Action<RadiusInfrastructureOptions>? ConfigureCallback { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentResource"/> class.
    /// </summary>
    /// <param name="name">The name of the Radius environment resource.</param>
    public RadiusEnvironmentResource(string name) : base(name)
    {
        Annotations.Add(new PipelineStepAnnotation((factoryContext) =>
        {
            var model = factoryContext.PipelineContext.Model;
            var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
            var isFirst = environments.Length == 0 || environments[0] == this;

            var publishStep = new PipelineStep
            {
                Name = $"publish-radius-{Name}",
                Description = $"Publishes the Radius environment configuration for {Name}.",
                Action = ctx => PublishAsync(ctx, isFirst)
            };
            publishStep.RequiredBy(WellKnownPipelineSteps.Publish);

            // T057: Deploy step depends on both publish and push to ensure images are available
            var deployStepHelper = new RadiusDeploymentPipelineStep(this);
            var deployStep = deployStepHelper.CreateStep(publishStep.Name);

            return Task.FromResult<IEnumerable<PipelineStep>>([publishStep, deployStep]);
        }));
    }

    /// <inheritdoc />
    [Experimental("ASPIRECOMPUTE002", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    public ReferenceExpression GetHostAddressExpression(EndpointReference endpointReference)
    {
        // Use Kubernetes DNS naming convention for service discovery
        // Format: <service>.<namespace>.svc.cluster.local
        var serviceName = endpointReference.Resource.Name;
        return ReferenceExpression.Create($"{serviceName}.{Namespace}.svc.cluster.local");
    }

    private async Task PublishAsync(PipelineStepContext context, bool isFirstEnvironment)
    {
        var outputPath = PublishingContextUtils.GetEnvironmentOutputPath(context, this);
        Directory.CreateDirectory(outputPath);

        var logger = context.Logger;
        logger.LogInformation("Publishing Radius infrastructure for environment '{EnvironmentName}'.", Name);

        var builder = new RadiusInfrastructureBuilder(
            context.Model,
            this,
            logger,
            ConfigureCallback,
            isFirstEnvironment);

        var bicepContent = builder.Build();

        var bicepPath = Path.Combine(outputPath, "app.bicep");
        await File.WriteAllTextAsync(bicepPath, bicepContent, context.CancellationToken).ConfigureAwait(false);

        var bicepConfigPath = Path.Combine(outputPath, "bicepconfig.json");
        var bicepConfig = """
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
        await File.WriteAllTextAsync(bicepConfigPath, bicepConfig, context.CancellationToken).ConfigureAwait(false);

        logger.LogInformation("Radius infrastructure published to '{OutputPath}'.", outputPath);
    }
}

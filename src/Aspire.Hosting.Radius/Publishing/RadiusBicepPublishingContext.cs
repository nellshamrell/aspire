// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Radius.ResourceMapping;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Orchestrates Bicep generation for a Radius environment: walks the
/// <see cref="DistributedApplicationModel"/>, builds an Azure.Provisioning
/// AST via <see cref="RadiusInfrastructureBuilder"/>, compiles to Bicep via
/// <c>Infrastructure.Build().Compile()</c>, and writes <c>app.bicep</c> +
/// <c>bicepconfig.json</c> to the output directory.
/// </summary>
internal sealed class RadiusBicepPublishingContext
{
    private readonly RadiusEnvironmentResource _environment;

    internal RadiusBicepPublishingContext(RadiusEnvironmentResource environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Creates a <see cref="PipelineStep"/> that generates Radius Bicep during publish.
    /// </summary>
    internal PipelineStep CreatePipelineStep()
    {
        var step = new PipelineStep
        {
            Name = $"publish-radius-{_environment.Name}",
            Description = $"Publish Radius environment '{_environment.Name}' as Bicep",
            Action = ExecuteAsync
        };
        step.RequiredBy(WellKnownPipelineSteps.Publish);
        return step;
    }

    internal async Task ExecuteAsync(PipelineStepContext context)
    {
        var model = context.Model;
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;

        logger.LogInformation(
            "Starting Bicep generation for Radius environment '{EnvironmentName}'",
            _environment.Name);

        var task = await context.ReportingStep.CreateTaskAsync(
            $"Generating Bicep for Radius environment '{_environment.Name}'...",
            cancellationToken).ConfigureAwait(false);

        try
        {
            // Build the infrastructure AST
            var typeMapper = new ResourceTypeMapper(
                context.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ResourceTypeMapper>());
            var builder = new RadiusInfrastructureBuilder(_environment, model, typeMapper, logger);

            var options = await builder.BuildAsync(context.ExecutionContext, cancellationToken).ConfigureAwait(false);

            // Persist the param-identifier -> ParameterResource bindings on the environment so the
            // deploy step (which shares this resource instance) can resolve a value for every
            // valueless Bicep `param` and forward it via `rad deploy --parameters`. An annotation
            // is used because that is how the credential-register step shares state across steps.
            if (options.RecipeParameterBindings.Count > 0)
            {
                var existing = _environment.Annotations.OfType<RadiusDeployParametersAnnotation>().ToList();
                foreach (var stale in existing)
                {
                    _environment.Annotations.Remove(stale);
                }

                _environment.Annotations.Add(new RadiusDeployParametersAnnotation(options.RecipeParameterBindings));
            }

            var resourceCount = options.Environments.Count + options.Applications.Count
                + options.RecipePacks.Count + options.ResourceTypeInstances.Count
                + options.Containers.Count + options.LegacyContainers.Count;

            logger.LogInformation(
                "Built Radius infrastructure AST with {ResourceCount} resources for environment '{EnvironmentName}'",
                resourceCount,
                _environment.Name);

            // Log recipe pack summary
            LogRecipePackSummary(options, logger);

            // Compile Bicep via Azure.Provisioning SDK pipeline
            var bicepContent = BicepPostProcessor.CompileBicep(options, _environment.Name, logger);
            var bicepConfigContent = BicepPostProcessor.RenderBicepConfig();

            // Write output files
            var outputDir = PublishingContextUtils.GetEnvironmentOutputPath(context, _environment);

            Directory.CreateDirectory(outputDir);

            var bicepPath = Path.Combine(outputDir, "app.bicep");
            var configPath = Path.Combine(outputDir, "bicepconfig.json");

            await File.WriteAllTextAsync(bicepPath, bicepContent, cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(configPath, bicepConfigContent, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Bicep generation complete for environment '{EnvironmentName}': {BicepPath}",
                _environment.Name,
                bicepPath);

            await task.CompleteAsync(
                $"Bicep generated: {bicepPath}",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Bicep generation failed for Radius environment '{EnvironmentName}'",
                _environment.Name);

            context.ReportingStep.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Generates Bicep content from a model without running in the pipeline.
    /// Used for testing.
    /// </summary>
    internal string GenerateBicep(DistributedApplicationModel model, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        // Reuse the caller's logger for the mapper so legacy-fallback / unmapped-type
        // diagnostics (e.g., the FakeLogger pattern in ResourceTypeMapperTests) are
        // observable. Allocating a new LoggerFactory here would leak provider state on
        // every call and would also drop any logger the caller supplied.
        var typeMapper = new ResourceTypeMapper(logger);
        var builder = new RadiusInfrastructureBuilder(_environment, model, typeMapper, logger);

        var options = BuildOptionsCore(builder);
        return BicepPostProcessor.CompileBicep(options, _environment.Name, logger);
    }

    /// <summary>
    /// Builds the <see cref="RadiusInfrastructureOptions"/> AST from a model.
    /// Used for testing.
    /// </summary>
    internal RadiusInfrastructureOptions BuildOptions(DistributedApplicationModel model, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        // See GenerateBicep for the rationale on reusing the caller's logger.
        var typeMapper = new ResourceTypeMapper(logger);
        var builder = new RadiusInfrastructureBuilder(_environment, model, typeMapper, logger);

        return BuildOptionsCore(builder);
    }

    /// <summary>
    /// Synchronously drives the async build with a publish-mode execution context. The test
    /// helpers are synchronous for ergonomics; the build only awaits in-memory model resolution,
    /// so blocking here cannot deadlock.
    /// </summary>
    private static RadiusInfrastructureOptions BuildOptionsCore(RadiusInfrastructureBuilder builder)
    {
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);
        return builder.BuildAsync(executionContext, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static void LogRecipePackSummary(RadiusInfrastructureOptions options, ILogger logger)
    {
        foreach (var pack in options.RecipePacks.OfType<RadiusRecipePackConstruct>())
        {
            var recipeCount = pack.Recipes.Count;
            var recipeTypes = string.Join(", ", pack.Recipes.Keys);
            logger.LogInformation(
                "Recipe pack '{PackName}' contains {RecipeCount} recipe(s): {RecipeTypes}",
                pack.BicepIdentifier,
                recipeCount,
                recipeTypes);
        }
    }
}

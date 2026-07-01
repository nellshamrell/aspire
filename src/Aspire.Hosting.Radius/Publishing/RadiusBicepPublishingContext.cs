// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Radius.ResourceGroups;
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

        // Grouped publish path: when any resource is routed to a Radius resource group, per-group
        // artifacts are owned by a single group-keyed emission (research Decision 10). Only the
        // primary environment's step performs it so each groups/<group>/app.bicep is written
        // exactly once regardless of how many environments the model declares; the remaining
        // environment steps become no-ops for the grouped path. The no-group default path below
        // is left byte-for-byte unchanged (SC-004).
        if (RadiusGroupOrchestrator.IsRoutingActive(model))
        {
            if (!IsPrimaryGroupEmitter(model))
            {
                logger.LogDebug(
                    "Skipping per-group Bicep emission for environment '{EnvironmentName}'; another environment owns group emission.",
                    _environment.Name);
                return;
            }

            await ExecuteGroupedAsync(context).ConfigureAwait(false);
            return;
        }

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
    /// Returns <see langword="true"/> when this environment owns the single group-keyed
    /// emission of per-group artifacts — the first <see cref="RadiusEnvironmentResource"/> in
    /// model order. Making one environment the owner keeps each <c>groups/&lt;group&gt;/app.bicep</c>
    /// emitted exactly once even when several environments are declared (research Decision 10).
    /// </summary>
    private bool IsPrimaryGroupEmitter(DistributedApplicationModel model)
    {
        var primary = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        return ReferenceEquals(primary, _environment);
    }

    /// <summary>
    /// Emits one <c>groups/&lt;group&gt;/app.bicep</c> (+ <c>bicepconfig.json</c>) per Radius
    /// resource group that carries routed resources. The model is partitioned by the group-keyed
    /// <see cref="RadiusGroupOrchestrator"/>; each partition is built by a group-scoped
    /// <see cref="RadiusInfrastructureBuilder"/> so an artifact contains only that group's
    /// resources plus the single logical application (FR-007). Files land under the run's root
    /// output directory so groups are not nested under any single environment's folder.
    /// </summary>
    private static async Task ExecuteGroupedAsync(PipelineStepContext context)
    {
        var model = context.Model;
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;

        logger.LogInformation("Starting per-group Bicep generation for Radius resource groups");

        var orchestrator = RadiusGroupOrchestrator.Create(model);

        var rootOutputDir = context.Services
            .GetRequiredService<IPipelineOutputService>()
            .GetOutputDirectory();

        var typeMapper = new ResourceTypeMapper(
            context.Services.GetRequiredService<ILoggerFactory>().CreateLogger<ResourceTypeMapper>());

        foreach (var partition in orchestrator.Partitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Resolve the environment this group's application deploys against and the group-scoped
            // emission context. A group that owns an environment emits its own chain (in-group,
            // bare references); a group that owns no environment deploys against a cross-group
            // environment, emitting only its application chain wired to that environment's full
            // UCP ID (FR-005). A group with neither is skipped.
            var resolved = ResolveGroupBuild(orchestrator, partition);
            if (resolved is not { } build)
            {
                logger.LogDebug(
                    "Radius resource group '{Group}' has no resolvable environment; skipping per-group emission.",
                    partition.Group);
                continue;
            }

            var (environment, groupContext) = build;

            var task = await context.ReportingStep.CreateTaskAsync(
                $"Generating Bicep for Radius resource group '{partition.Group}'...",
                cancellationToken).ConfigureAwait(false);

            try
            {
                var builder = new RadiusInfrastructureBuilder(
                    environment, model, typeMapper, logger, groupContext);

                var options = await builder.BuildAsync(context.ExecutionContext, cancellationToken).ConfigureAwait(false);

                // Persist the param-identifier -> ParameterResource bindings on the group's
                // environment so the (per-group) deploy step can resolve a value for every
                // valueless Bicep `param` and forward it via `rad deploy --parameters`.
                if (options.RecipeParameterBindings.Count > 0)
                {
                    var existing = environment.Annotations.OfType<RadiusDeployParametersAnnotation>().ToList();
                    foreach (var stale in existing)
                    {
                        environment.Annotations.Remove(stale);
                    }

                    environment.Annotations.Add(new RadiusDeployParametersAnnotation(options.RecipeParameterBindings));
                }

                LogRecipePackSummary(options, logger);

                var bicepContent = BicepPostProcessor.CompileBicep(options, environment.Name, logger);
                var bicepConfigContent = BicepPostProcessor.RenderBicepConfig();

                var outputDir = Path.Combine(rootOutputDir, "groups", partition.Group);
                Directory.CreateDirectory(outputDir);

                var bicepPath = Path.Combine(outputDir, "app.bicep");
                var configPath = Path.Combine(outputDir, "bicepconfig.json");

                await File.WriteAllTextAsync(bicepPath, bicepContent, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(configPath, bicepConfigContent, cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "Bicep generation complete for Radius resource group '{Group}': {BicepPath}",
                    partition.Group,
                    bicepPath);

                await task.CompleteAsync(
                    $"Bicep generated: {bicepPath}",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bicep generation failed for Radius resource group '{Group}'",
                    partition.Group);

                context.ReportingStep.Log(LogLevel.Error, ex.Message);
                throw;
            }
        }
    }

    /// <summary>
    /// Resolves, for a group partition, the <see cref="RadiusEnvironmentResource"/> the group's
    /// application deploys against and the group-scoped <see cref="RadiusGroupContext"/> used to
    /// emit it. Returns <see langword="null"/> when the group has no resolvable environment (it is
    /// only named as a cross-group target and carries nothing of its own).
    /// </summary>
    internal static (RadiusEnvironmentResource Environment, RadiusGroupContext Context)? ResolveGroupBuild(
        RadiusGroupOrchestrator orchestrator, RadiusGroupPartition partition)
    {
        var resourceNames = partition.Resources
            .Select(r => r.Name)
            .ToHashSet(StringComparer.Ordinal);

        if (partition.Environments.Count > 0)
        {
            // In-group environment (the deterministic first-declared environment, FR-011); emit
            // the group's own environment/recipe-pack/application chain with bare references.
            var context = new RadiusGroupContext
            {
                Group = partition.Group,
                ResourceNames = resourceNames,
                ReferenceByResourceName = orchestrator.ReferenceByResourceName,
                CrossGroupEnvironmentId = null,
            };
            return (partition.Environments[0], context);
        }

        // Cross-group environment: the group owns no environment; its resources deploy against an
        // environment in another group. Resolve that group's first-declared environment and emit
        // its full UCP ID as properties.environment (FR-005).
        var crossGroupReference = partition.Resources
            .Select(r => orchestrator.ReferenceByResourceName.TryGetValue(r.Name, out var reference) ? reference : null)
            .FirstOrDefault(reference => reference is { IsCrossGroupEnvironment: true });

        if (crossGroupReference is null)
        {
            return null;
        }

        var environmentPartition = orchestrator.Partitions.FirstOrDefault(p =>
            string.Equals(p.Group, crossGroupReference.EnvironmentGroup, StringComparison.Ordinal)
            && p.Environments.Count > 0);

        if (environmentPartition is null)
        {
            return null;
        }

        var crossGroupEnvironment = environmentPartition.Environments[0];
        var crossGroupContext = new RadiusGroupContext
        {
            Group = partition.Group,
            ResourceNames = resourceNames,
            ReferenceByResourceName = orchestrator.ReferenceByResourceName,
            CrossGroupEnvironmentId = crossGroupReference.ToUcpEnvironmentId(crossGroupEnvironment.Name),
        };
        return (crossGroupEnvironment, crossGroupContext);
    }

    /// <summary>
    /// Generates the per-group Bicep content for every group in the model, keyed by group name,
    /// without any file IO. Mirrors <see cref="ExecuteGroupedAsync"/>'s emission for tests that
    /// assert on cross-group reference / environment shapes (FR-004, FR-005).
    /// </summary>
    internal static IReadOnlyDictionary<string, string> GenerateGroupedBicep(
        DistributedApplicationModel model, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        var orchestrator = RadiusGroupOrchestrator.Create(model);
        var typeMapper = new ResourceTypeMapper(logger);
        var executionContext = new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var partition in orchestrator.Partitions)
        {
            var resolved = ResolveGroupBuild(orchestrator, partition);
            if (resolved is not { } build)
            {
                continue;
            }

            var (environment, groupContext) = build;
            var builder = new RadiusInfrastructureBuilder(environment, model, typeMapper, logger, groupContext);
            var options = builder.BuildAsync(executionContext, CancellationToken.None).GetAwaiter().GetResult();
            result[partition.Group] = BicepPostProcessor.CompileBicep(options, environment.Name, logger);
        }

        return result;
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

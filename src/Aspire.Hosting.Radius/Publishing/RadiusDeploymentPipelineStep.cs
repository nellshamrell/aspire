// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

#pragma warning disable ASPIREPIPELINES001
#pragma warning disable ASPIREPIPELINES004

using System.Diagnostics;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Radius.ResourceMapping;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Pipeline step that deploys a Radius application by invoking <c>rad deploy app.bicep</c>.
/// Depends on the publish step only (not <see cref="WellKnownPipelineSteps.Push"/>)
/// to support kind clusters without a container registry.
/// </summary>
internal sealed class RadiusDeploymentPipelineStep
{
    private const string RadInstallUrl = "https://docs.radapp.io/installation/";

    // The single logical Aspire application name, identical across every group's artifact
    // (the builder always names the application 'app'); forwarded as --application (FR-011).
    private const string ApplicationName = "app";

    private readonly RadiusEnvironmentResource _environment;

    internal RadiusDeploymentPipelineStep(RadiusEnvironmentResource environment)
    {
        _environment = environment;
    }

    /// <summary>
    /// Creates a <see cref="PipelineStep"/> that deploys a Radius application via <c>rad deploy</c>.
    /// The step depends on the publish step for this environment (Bicep must be generated first).
    /// </summary>
    internal PipelineStep CreatePipelineStep()
    {
        EnsureGroupedCredentialDependencyConfigurationRegistered();

        var step = new PipelineStep
        {
            Name = $"deploy-radius-{_environment.Name}",
            Description = $"Deploy Radius environment '{_environment.Name}' via rad CLI",
            Action = ExecuteAsync
        };
        step.DependsOn($"publish-radius-{_environment.Name}");
        step.RequiredBy(WellKnownPipelineSteps.Deploy);
        step.DependsOn(WellKnownPipelineSteps.DeployPrereq);
        return step;
    }

    private void EnsureGroupedCredentialDependencyConfigurationRegistered()
    {
        if (_environment.Annotations.OfType<GroupedCredentialDependencyConfigurationAnnotation>().Any())
        {
            return;
        }

        _environment.Annotations.Add(new GroupedCredentialDependencyConfigurationAnnotation());
        _environment.Annotations.Add(new PipelineConfigurationAnnotation(WireGroupedCredentialDependencies));
    }

    private static void WireGroupedCredentialDependencies(PipelineConfigurationContext context)
    {
        var model = context.Model;
        if (!RadiusGroupOrchestrator.IsRoutingActive(model))
        {
            return;
        }

        var primaryEnvironment = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        if (primaryEnvironment is null)
        {
            return;
        }

        var primaryDeployStep = context.Steps.SingleOrDefault(s => s.Name == $"deploy-radius-{primaryEnvironment.Name}");
        if (primaryDeployStep is null)
        {
            return;
        }

        var credentialStepNames = model.Resources
            .OfType<RadiusEnvironmentResource>()
            .Where(static env => env.Annotations.OfType<RadiusCloudProvidersAnnotation>().Any())
            .Select(static env => $"register-radius-credentials-{env.Name}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var credentialStepName in context.Steps.Select(static s => s.Name).Where(credentialStepNames.Contains))
        {
            // In grouped mode the primary emitter owns every group's deploy, so it must wait
            // for every environment's Radius installation-global credential registration.
            if (!primaryDeployStep.DependsOnSteps.Contains(credentialStepName))
            {
                primaryDeployStep.DependsOn(credentialStepName);
            }
        }
    }

    internal async Task ExecuteAsync(PipelineStepContext context)
    {
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;

        // Detect rad CLI availability
        var radAvailable = await DetectRadCliAsync(cancellationToken).ConfigureAwait(false);
        if (!radAvailable)
        {
            logger.LogError("The 'rad' CLI was not found on PATH. Install it from {InstallUrl}", RadInstallUrl);
            throw new InvalidOperationException(
                $"The 'rad' CLI was not found. Please install it from {RadInstallUrl} and ensure it is available on your PATH.");
        }

        logger.LogInformation("rad CLI detected on PATH for environment '{EnvironmentName}'", _environment.Name);

        // Multi-group deploy: when any resource is routed to a Radius resource group, the
        // primary environment owns a single group-ordered deploy (one `rad group create` +
        // one `rad deploy` per group). Non-primary environments defer so each group deploys
        // exactly once regardless of environment count (FR-012). The default no-group path
        // below stays byte-for-byte identical (FR-016, SC-004).
        var model = context.Model;
        if (RadiusGroupOrchestrator.IsRoutingActive(model))
        {
            if (!IsPrimaryGroupEmitter(model))
            {
                logger.LogDebug(
                    "Radius environment '{EnvironmentName}' defers multi-group deploy to the primary environment.",
                    _environment.Name);
                return;
            }

            await ExecuteGroupedAsync(context).ConfigureAwait(false);
            return;
        }

        // Resolve the output directory where Bicep was generated
        var outputDir = PublishingContextUtils.GetEnvironmentOutputPath(context, _environment);
        var bicepPath = Path.Combine(outputDir, "app.bicep");

        if (!File.Exists(bicepPath))
        {
            logger.LogError("Bicep file not found at {BicepPath}. Ensure the publish step completed successfully.", bicepPath);
            throw new InvalidOperationException(
                $"Bicep file not found at '{bicepPath}'. Ensure the publish step completed successfully before deploying.");
        }

        logger.LogInformation("Starting rad deploy with Bicep file '{BicepPath}'", bicepPath);

        // Resolve a value for every Bicep `param` that was bound to an Aspire ParameterResource
        // during publish. The generated Bicep declares these as valueless params, so `rad deploy`
        // must receive each one via `--parameters name=value`. Secret-bound values are collected
        // so they can be scrubbed from the logged command and rad stdout/stderr.
        var (parameterArgs, secretValues) = await ResolveDeployParametersAsync(cancellationToken).ConfigureAwait(false);

        var deployTask = await context.ReportingStep.CreateTaskAsync(
            $"Deploying Radius environment '{_environment.Name}' via rad deploy...",
            cancellationToken).ConfigureAwait(false);

        try
        {
            var stderrBuilder = new System.Text.StringBuilder();

            using var process = new Process();
            // Use ArgumentList rather than Arguments so the bicep path doesn't need shell-style
            // quoting and is forwarded verbatim — paths containing spaces or special characters
            // (e.g. `C:\Program Files\…`) survive intact on every platform without double-escaping.
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "rad",
                WorkingDirectory = outputDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("deploy");
            process.StartInfo.ArgumentList.Add(bicepPath);
            foreach (var arg in parameterArgs)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            // Mirror the credential step: log the full command but with any secret parameter
            // values replaced by '***' so they never reach the console or log sinks.
            var loggedArgs = RadCredentialRegisterStep.RedactSecretValues(
                string.Join(' ', process.StartInfo.ArgumentList), secretValues);
            logger.LogInformation("Running: rad {Args}", loggedArgs);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    logger.LogInformation("rad (stdout): {Output}", redacted);
                    context.ReportingStep.Log(LogLevel.Information, redacted);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    stderrBuilder.AppendLine(redacted);
                    logger.LogWarning("rad (stderr): {Error}", redacted);
                    context.ReportingStep.Log(LogLevel.Warning, redacted);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    logger.LogWarning("Cancellation requested — terminating rad deploy process for environment '{EnvironmentName}'", _environment.Name);
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // Race: the process exited between HasExited check and Kill. Nothing to do.
                    }
                }

                throw;
            }

            var exitCode = process.ExitCode;

            logger.LogInformation("rad deploy exited with code {ExitCode} for environment '{EnvironmentName}'", exitCode, _environment.Name);

            if (exitCode != 0)
            {
                var stderrText = stderrBuilder.ToString().Trim();
                var errorMessage = string.IsNullOrEmpty(stderrText)
                    ? $"rad deploy failed with exit code {exitCode}"
                    : $"rad deploy failed with exit code {exitCode}: {stderrText}";

                logger.LogError("rad deploy failed for environment '{EnvironmentName}': {ErrorMessage}", _environment.Name, errorMessage);
                context.ReportingStep.Log(LogLevel.Error, errorMessage);

                throw new InvalidOperationException(errorMessage);
            }

            await deployTask.CompleteAsync(
                $"Radius deployment complete for '{_environment.Name}'",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error during rad deploy for environment '{EnvironmentName}'", _environment.Name);
            context.ReportingStep.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Resolves deploy-time values for every Bicep parameter that was bound to an Aspire
    /// <see cref="ParameterResource"/> during publish (recorded on the environment via
    /// <see cref="RadiusDeployParametersAnnotation"/>). Returns the <c>--parameters name=value</c>
    /// argument tokens plus the set of secret values to scrub from logs.
    /// </summary>
    private async Task<(IReadOnlyList<string> Args, IReadOnlyList<string> SecretValues)> ResolveDeployParametersAsync(
        CancellationToken cancellationToken)
    {
        var annotation = _environment.Annotations.OfType<RadiusDeployParametersAnnotation>().LastOrDefault();
        return await BuildParameterArgsAsync(annotation?.Parameters, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the deterministic <c>--parameters name=value</c> tokens for a set of Bicep-param →
    /// <see cref="ParameterResource"/> bindings and collects the secret values to scrub from logs.
    /// Shared by the single-environment and per-group deploy paths (FR-013, SC-008).
    /// </summary>
    private static async Task<(IReadOnlyList<string> Args, IReadOnlyList<string> SecretValues)> BuildParameterArgsAsync(
        IReadOnlyDictionary<string, ParameterResource>? bindings,
        CancellationToken cancellationToken)
    {
        if (bindings is null || bindings.Count == 0)
        {
            return (Array.Empty<string>(), Array.Empty<string>());
        }

        var args = new List<string>();
        var secretValues = new List<string>();

        // Order by identifier for deterministic command construction (stable logs and tests).
        foreach (var (identifier, parameter) in bindings.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            var value = await parameter.GetValueAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;

            // `rad deploy` accepts repeated `--parameters name=value` flags. The name/value pair is
            // a single token so paths/values with spaces survive via ArgumentList verbatim.
            args.Add("--parameters");
            args.Add($"{identifier}={value}");

            if (parameter.Secret && !string.IsNullOrWhiteSpace(value))
            {
                secretValues.Add(value);
            }
        }

        return (args, secretValues);
    }

    /// <summary>
    /// Returns <see langword="true"/> when this environment owns the single group-ordered deploy —
    /// the first <see cref="RadiusEnvironmentResource"/> in model order. Mirrors the publish step's
    /// primary-emitter rule so each group is deployed exactly once even with several environments.
    /// </summary>
    private bool IsPrimaryGroupEmitter(DistributedApplicationModel model)
    {
        var primary = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        return ReferenceEquals(primary, _environment);
    }

    /// <summary>
    /// A single group's planned <c>rad</c> invocations: an idempotent <c>rad group create</c>
    /// followed by exactly one <c>rad deploy</c> (FR-010, FR-012), plus the secret parameter values
    /// to redact from logs (FR-013, SC-008).
    /// </summary>
    internal sealed record RadGroupDeployCommand(
        string Group,
        IReadOnlyList<string> GroupCreateArguments,
        IReadOnlyList<string> DeployArguments,
        IReadOnlyList<string> SecretValues);

    /// <summary>
    /// Plans the per-group <c>rad</c> invocations for a multi-group deploy: one entry per group in
    /// topological deploy order (dependencies first, SC-002). Each entry issues an idempotent
    /// <c>rad group create</c> then a single <c>rad deploy groups/&lt;group&gt;/app.bicep</c> with
    /// explicit <c>--group</c>, the inert first-declared (or cross-group UCP) <c>--environment</c>,
    /// the shared <c>--application</c>, an optional <c>--workspace</c>, and this group's resolved
    /// <c>--parameters</c> (FR-009 – FR-013). Pure planning: no processes are launched.
    /// </summary>
    internal static async Task<IReadOnlyList<RadGroupDeployCommand>> PlanGroupDeployAsync(
        DistributedApplicationModel model,
        string rootOutputDir,
        string? workspace,
        DistributedApplicationExecutionContext executionContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var orchestrator = RadiusGroupOrchestrator.Create(model);
        var typeMapper = new ResourceTypeMapper(logger);
        var partitionByGroup = orchestrator.Partitions.ToDictionary(static p => p.Group, StringComparer.Ordinal);

        var commands = new List<RadGroupDeployCommand>();

        foreach (var group in orchestrator.DeployOrder)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!partitionByGroup.TryGetValue(group, out var partition))
            {
                // Group is only named as a cross-group target and carries nothing of its own.
                continue;
            }

            var resolved = RadiusBicepPublishingContext.ResolveGroupBuild(orchestrator, partition);
            if (resolved is not { } build)
            {
                continue;
            }

            var (environment, groupContext) = build;

            // The inert --environment default: the group's first-declared environment name when it
            // owns one, or the cross-group environment's full UCP ID otherwise (FR-011, FR-005).
            var environmentArg = groupContext.CrossGroupEnvironmentId ?? environment.Name;

            // Resolve this group's recipe-parameter bindings from its own bicep so every valueless
            // Bicep param is forwarded via --parameters, with secret values collected for redaction.
            var builder = new RadiusInfrastructureBuilder(environment, model, typeMapper, logger, groupContext);
            var options = await builder.BuildAsync(executionContext, cancellationToken).ConfigureAwait(false);
            var (parameterArgs, secretValues) = await BuildParameterArgsAsync(
                options.RecipeParameterBindings, cancellationToken).ConfigureAwait(false);

            var bicepPath = Path.Combine(rootOutputDir, "groups", group, "app.bicep");

            var groupCreateArgs = new List<string> { "group", "create", group };
            if (!string.IsNullOrEmpty(workspace))
            {
                groupCreateArgs.Add("-w");
                groupCreateArgs.Add(workspace);
            }

            var deployArgs = new List<string>
            {
                "deploy",
                bicepPath,
                "--group", group,
                "--environment", environmentArg,
                "--application", ApplicationName,
            };
            if (!string.IsNullOrEmpty(workspace))
            {
                deployArgs.Add("--workspace");
                deployArgs.Add(workspace);
            }
            deployArgs.AddRange(parameterArgs);

            commands.Add(new RadGroupDeployCommand(group, groupCreateArgs, deployArgs, secretValues));
        }

        return commands;
    }

    /// <summary>
    /// Deploys every Radius resource group in topological order via one idempotent
    /// <c>rad group create</c> and one <c>rad deploy</c> per group. Fails on the first group
    /// failure with no custom cross-group rollback; a re-run is idempotent (FR-014).
    /// </summary>
    private static async Task ExecuteGroupedAsync(PipelineStepContext context)
    {
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;
        var model = context.Model;

        var rootOutputDir = context.Services
            .GetRequiredService<IPipelineOutputService>()
            .GetOutputDirectory();

        // FR-017: --workspace is passed only when a non-default workspace is configured. No
        // workspace-configuration surface exists yet, so the ambient workspace is used.
        string? workspace = null;

        var commands = await PlanGroupDeployAsync(
            model, rootOutputDir, workspace, context.ExecutionContext, logger, cancellationToken).ConfigureAwait(false);

        if (commands.Count == 0)
        {
            logger.LogWarning("No Radius resource groups resolved for deployment; nothing to deploy.");
            return;
        }

        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Deploying Radius resource group '{Group}'", command.Group);

            // Idempotent group create — a pre-existing group is treated as success (FR-010).
            await RunRadProcessAsync(
                rootOutputDir,
                command.GroupCreateArguments,
                Array.Empty<string>(),
                $"Ensuring Radius resource group '{command.Group}' exists...",
                $"Radius resource group '{command.Group}' ready",
                treatAlreadyExistsAsSuccess: true,
                context).ConfigureAwait(false);

            // Exactly one rad deploy per group (FR-012).
            await RunRadProcessAsync(
                rootOutputDir,
                command.DeployArguments,
                command.SecretValues,
                $"Deploying Radius resource group '{command.Group}' via rad deploy...",
                $"Radius deployment complete for group '{command.Group}'",
                treatAlreadyExistsAsSuccess: false,
                context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a single <c>rad</c> invocation, streaming redacted stdout/stderr to the logger and the
    /// reporting step. Throws <see cref="InvalidOperationException"/> on a non-zero exit unless
    /// <paramref name="treatAlreadyExistsAsSuccess"/> is set and the failure is an already-exists
    /// group create (FR-010, FR-014).
    /// </summary>
    private static async Task RunRadProcessAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        IReadOnlyList<string> secretValues,
        string taskDescription,
        string completionDescription,
        bool treatAlreadyExistsAsSuccess,
        PipelineStepContext context)
    {
        var cancellationToken = context.CancellationToken;
        var logger = context.Logger;

        var task = await context.ReportingStep.CreateTaskAsync(taskDescription, cancellationToken).ConfigureAwait(false);

        try
        {
            var stderrBuilder = new System.Text.StringBuilder();

            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "rad",
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in arguments)
            {
                process.StartInfo.ArgumentList.Add(arg);
            }

            var loggedArgs = RadCredentialRegisterStep.RedactSecretValues(
                string.Join(' ', process.StartInfo.ArgumentList), secretValues);
            logger.LogInformation("Running: rad {Args}", loggedArgs);

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    logger.LogInformation("rad (stdout): {Output}", redacted);
                    context.ReportingStep.Log(LogLevel.Information, redacted);
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    var redacted = RadCredentialRegisterStep.RedactSecretValues(e.Data, secretValues);
                    stderrBuilder.AppendLine(redacted);
                    logger.LogWarning("rad (stderr): {Error}", redacted);
                    context.ReportingStep.Log(LogLevel.Warning, redacted);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException)
                    {
                        // Race: the process exited between HasExited check and Kill. Nothing to do.
                    }
                }

                throw;
            }

            var exitCode = process.ExitCode;
            if (exitCode != 0)
            {
                var stderrText = stderrBuilder.ToString().Trim();

                // Idempotent group create: an already-existing group is a success, not a failure.
                if (treatAlreadyExistsAsSuccess &&
                    stderrText.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Radius resource group already exists — treating as success.");
                    await task.CompleteAsync(completionDescription, cancellationToken: cancellationToken).ConfigureAwait(false);
                    return;
                }

                var errorMessage = string.IsNullOrEmpty(stderrText)
                    ? $"rad {arguments[0]} failed with exit code {exitCode}"
                    : $"rad {arguments[0]} failed with exit code {exitCode}: {stderrText}";

                logger.LogError("{ErrorMessage}", errorMessage);
                context.ReportingStep.Log(LogLevel.Error, errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            await task.CompleteAsync(completionDescription, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not OperationCanceledException)
        {
            logger.LogError(ex, "Unexpected error during rad invocation");
            context.ReportingStep.Log(LogLevel.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Detects whether the <c>rad</c> CLI is available on the system PATH.
    /// </summary>
    internal static async Task<bool> DetectRadCliAsync(CancellationToken cancellationToken = default)
    {
        // Honour a pre-cancelled token even when `rad` is missing from PATH. Without this,
        // Process.Start() would throw Win32Exception first and the catch-all below would
        // convert that into `return false`, swallowing the cancellation request.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = "rad",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            process.StartInfo.ArgumentList.Add("version");

            process.Start();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (OperationCanceledException)
        {
            // Honour cancellation — never swallow this; the caller (and CTRL-C) must observe it.
            throw;
        }
        catch
        {
            // rad is not found on PATH, or some other launch failure (e.g. EACCES).
            // Treat as "not available" so the deploy step surfaces a clear install message instead
            // of a stack trace pointing at Process.Start.
            return false;
        }
    }

    private sealed class GroupedCredentialDependencyConfigurationAnnotation : IResourceAnnotation;
}

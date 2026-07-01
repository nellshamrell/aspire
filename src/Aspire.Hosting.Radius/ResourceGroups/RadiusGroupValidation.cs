// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;

namespace Aspire.Hosting.Radius.ResourceGroups;

/// <summary>
/// Config-time (pre-publish/deploy) validation of Radius resource-group routing.
/// Runs as a fail-fast gate that is <c>RequiredBy</c> both publish and deploy, so
/// orphan/ambiguous/unresolvable/cycle failures surface before any Bicep is emitted
/// or <c>rad</c> is contacted (FR-003, FR-006). Empty/whitespace group names are
/// rejected earlier, at the <c>WithRadiusResourceGroup</c> call site.
/// </summary>
internal static class RadiusGroupValidation
{
    /// <summary>Pipeline-step entry point. No-ops in Run mode.</summary>
    internal static Task ValidateAsync(PipelineStepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.ExecutionContext.IsRunMode)
        {
            return Task.CompletedTask;
        }

        Validate(context.Model);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates group routing over the whole application model. No-op when no resource
    /// is routed to a group (the feature is inactive and the default path is unchanged).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A resource is orphaned (<c>ASPIRERADIUS031</c>) or ambiguously assigned
    /// (<c>ASPIRERADIUS032</c>), a cross-group environment target is unresolvable
    /// (<c>ASPIRERADIUS034</c>), or the group dependency graph contains a cycle
    /// (<c>ASPIRERADIUS035</c>).
    /// </exception>
    internal static void Validate(DistributedApplicationModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (!RadiusGroupOrchestrator.IsRoutingActive(model))
        {
            return;
        }

        // Resolving the orchestrator performs all whole-model graph validation and throws
        // ASPIRERADIUS031/032/034/035 on invalid routing (research Decision 10).
        _ = RadiusGroupOrchestrator.Create(model);
    }
}

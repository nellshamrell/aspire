// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Test utilities for Radius integration tests.
/// </summary>
internal static class RadiusTestHelper
{
    /// <summary>
    /// Builds the distributed application and returns the model.
    /// </summary>
    public static async Task<DistributedApplicationModel> BuildAndGetModelAsync(
        IDistributedApplicationBuilder builder)
    {
        using var app = builder.Build();

        await ExecuteBeforeStartHooksAsync(app, default);

        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    /// <summary>
    /// Finds the <see cref="RadiusEnvironmentResource"/> in the model or throws if not found.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        var environment = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();

        if (environment is null)
        {
            throw new InvalidOperationException(
                "No RadiusEnvironmentResource found in the application model. " +
                "Ensure AddRadiusEnvironment() was called.");
        }

        return environment;
    }

    /// <summary>
    /// Gets all <see cref="DeploymentTargetAnnotation"/> instances on the given resource.
    /// </summary>
    public static IReadOnlyList<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>().ToList();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}

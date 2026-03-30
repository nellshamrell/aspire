#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Test utilities for building and inspecting Aspire app models with Radius environments.
/// </summary>
public static class RadiusTestHelper
{
    /// <summary>
    /// Builds the distributed application and returns the model.
    /// </summary>
    public static DistributedApplicationModel BuildAndGetModel(IDistributedApplicationTestingBuilder builder)
    {
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    /// <summary>
    /// Gets the first <see cref="RadiusEnvironmentResource"/> from the model, or throws.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        return model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault()
            ?? throw new InvalidOperationException("No RadiusEnvironmentResource found in the model.");
    }

    /// <summary>
    /// Gets all <see cref="DeploymentTargetAnnotation"/> instances on a resource.
    /// </summary>
    public static IReadOnlyList<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>().ToList();
    }
}

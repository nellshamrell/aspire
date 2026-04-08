// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Shared test utilities for Radius integration tests.
/// </summary>
internal static class RadiusTestHelper
{
    /// <summary>
    /// Builds a distributed application model from the given builder and returns the model.
    /// </summary>
    public static DistributedApplicationModel GetModel(IDistributedApplicationTestingBuilder builder)
    {
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    /// <summary>
    /// Returns the first <see cref="RadiusEnvironmentResource"/> from the model, or throws.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        return model.Resources.OfType<RadiusEnvironmentResource>().First();
    }

    /// <summary>
    /// Returns all <see cref="DeploymentTargetAnnotation"/> instances on the given resource.
    /// </summary>
    public static IEnumerable<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>();
    }
}

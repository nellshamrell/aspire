// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Helper utilities for Radius integration tests.
/// </summary>
internal static class RadiusTestHelper
{
    /// <summary>
    /// Builds an app host and returns its <see cref="DistributedApplicationModel"/>.
    /// </summary>
    public static DistributedApplicationModel BuildAndGetModel(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = DistributedApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    /// <summary>
    /// Gets the first <see cref="RadiusEnvironmentResource"/> from the model.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        return model.Resources.OfType<RadiusEnvironmentResource>().First();
    }

    /// <summary>
    /// Gets all <see cref="DeploymentTargetAnnotation"/> instances on a resource.
    /// </summary>
    public static IEnumerable<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Test utilities for building app models and inspecting Radius resources.
/// </summary>
public static class RadiusTestHelper
{
    /// <summary>
    /// Builds a <see cref="DistributedApplicationModel"/> from a configured builder.
    /// </summary>
    public static (DistributedApplication App, DistributedApplicationModel Model) BuildAndGetModel(
        Func<IDistributedApplicationBuilder, IDistributedApplicationBuilder> configure)
    {
        var builder = DistributedApplication.CreateBuilder();
        configure(builder);
        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        return (app, model);
    }

    /// <summary>
    /// Gets the first <see cref="RadiusEnvironmentResource"/> from the model, or throws.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        return model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault()
            ?? throw new InvalidOperationException("No RadiusEnvironmentResource found in app model.");
    }

    /// <summary>
    /// Gets all <see cref="DeploymentTargetAnnotation"/> instances on a resource.
    /// </summary>
    public static IEnumerable<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>();
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Eventing;
using Aspire.Hosting.Lifecycle;
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
    /// Triggers the RadiusInfrastructure subscriber's BeforeStartEvent handler in isolation,
    /// without firing other Aspire built-in handlers that require DCP/dashboard paths.
    /// </summary>
    public static async Task PublishBeforeStartEventAsync(DistributedApplication app)
    {
        var appModel = app.Services.GetRequiredService<DistributedApplicationModel>();
        var execContext = app.Services.GetRequiredService<DistributedApplicationExecutionContext>();

        // Create an isolated eventing instance so built-in Aspire handlers
        // (e.g., InitializeDcpAnnotations) are not triggered.
        var isolatedEventing = new DistributedApplicationEventing();

        // Only activate Radius-related subscribers
        var subscribers = app.Services.GetServices<IDistributedApplicationEventingSubscriber>();
        foreach (var subscriber in subscribers.Where(s => s is RadiusInfrastructure))
        {
            await subscriber.SubscribeAsync(isolatedEventing, execContext, CancellationToken.None);
        }

        await isolatedEventing.PublishAsync(new BeforeStartEvent(app.Services, appModel), CancellationToken.None);
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

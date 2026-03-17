// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002 // IComputeEnvironmentResource is experimental

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting;

/// <summary>
/// Provides extension methods for adding Radius environment resources to the application model.
/// </summary>
public static class RadiusEnvironmentExtensions
{
    /// <summary>
    /// Adds a Radius compute environment to the application model.
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    /// <param name="name">The name of the Radius environment resource.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExportIgnore(Reason = "Radius extension is not yet ATS-compatible")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();

        var resource = new RadiusEnvironmentResource(name)
        {
            Dashboard = CreateRadiusDashboard(builder, $"{name}-dashboard")
        };

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Creates a new Radius Dashboard resource builder with the specified name.
    /// </summary>
    internal static IResourceBuilder<RadiusDashboardResource> CreateRadiusDashboard(
        IDistributedApplicationBuilder builder,
        string name)
    {
        var resource = new RadiusDashboardResource(name);

        return builder.CreateResourceBuilder(resource)
                      .WithImage(RadiusDashboardResource.DefaultImage, RadiusDashboardResource.DefaultTag)
                      .WithHttpEndpoint(targetPort: RadiusDashboardResource.DefaultPort, name: "http")
                      .WithEndpoint("http", e => e.IsExternal = true);
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the Radius environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="namespace">A valid Kubernetes namespace name.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExportIgnore(Reason = "Radius extension is not yet ATS-compatible")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithRadiusNamespace(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        string @namespace)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(@namespace);

        builder.Resource.Namespace = @namespace;

        return builder;
    }

    /// <summary>
    /// Enables or disables the Radius dashboard visualization during <c>aspire run</c>.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="enabled">Whether the dashboard should be started. Defaults to <c>true</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExportIgnore(Reason = "Radius extension is not yet ATS-compatible")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.DashboardEnabled = enabled;

        return builder;
    }

    /// <summary>
    /// Applies Radius-specific publishing customizations to an individual resource.
    /// </summary>
    /// <typeparam name="T">The type of resource to customize.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">A delegate to configure the <see cref="RadiusResourceCustomization"/>.</param>
    /// <returns>The resource builder for method chaining.</returns>
    [AspireExportIgnore(Reason = "Radius extension is not yet ATS-compatible")]
    public static IResourceBuilder<T> PublishAsRadiusResource<T>(
        this IResourceBuilder<T> builder,
        Action<RadiusResourceCustomization> configure)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var customization = new RadiusResourceCustomization();
        configure(customization);

        builder.Resource.Annotations.Add(new RadiusResourceCustomizationAnnotation(customization));

        return builder;
    }
}

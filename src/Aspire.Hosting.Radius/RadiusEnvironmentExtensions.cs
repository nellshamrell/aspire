// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREATS001 // Type is for evaluation purposes only

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
    [AspireExport("addRadiusEnvironment", Description = "Adds a Radius compute environment")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();

        var resource = new RadiusEnvironmentResource(name);

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the Kubernetes namespace for the Radius environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="namespace">The Kubernetes namespace to deploy resources into.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExport("withRadiusNamespace", Description = "Sets the Kubernetes namespace for the Radius environment")]
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
    /// Enables or disables the Radius dashboard container for this environment.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="enabled">Whether to enable the dashboard. Default is <c>true</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/>.</returns>
    [AspireExport("withDashboard", Description = "Enables or disables the Radius dashboard")]
    public static IResourceBuilder<RadiusEnvironmentResource> WithDashboard(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Resource.DashboardEnabled = enabled;

        return builder;
    }

    /// <summary>
    /// Configures a resource to be published as a Radius resource with custom provisioning options.
    /// </summary>
    /// <typeparam name="T">The type of resource.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">An action to configure <see cref="RadiusResourceCustomization"/>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{T}"/>.</returns>
    [AspireExportIgnore(Reason = "Action<RadiusResourceCustomization> callback is not ATS-compatible.")]
    public static IResourceBuilder<T> PublishAsRadiusResource<T>(
        this IResourceBuilder<T> builder,
        Action<RadiusResourceCustomization> configure)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var customization = new RadiusResourceCustomization();
        configure(customization);

        builder.WithAnnotation(new RadiusResourceCustomizationAnnotation(customization));

        return builder;
    }
}

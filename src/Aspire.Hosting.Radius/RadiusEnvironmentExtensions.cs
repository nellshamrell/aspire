// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Provisioning;

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
    /// <param name="name">The name of the Radius environment resource. Defaults to <c>"radius"</c>.</param>
    /// <returns>A reference to the <see cref="IResourceBuilder{RadiusEnvironmentResource}"/> for further configuration.</returns>
    [AspireExport(Description = "Adds a Radius compute environment")]
    public static IResourceBuilder<RadiusEnvironmentResource> AddRadiusEnvironment(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name = "radius")
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        // Register the event subscriber (idempotent via TryAddEnumerable)
        builder.Services.TryAddEventingSubscriber<RadiusInfrastructure>();

        var resource = new RadiusEnvironmentResource(name);

        if (builder.ExecutionContext.IsRunMode)
        {
            // In run mode, don't add to top-level resources (visualization-only)
            return builder.CreateResourceBuilder(resource);
        }

        return builder.AddResource(resource);
    }

    /// <summary>
    /// Sets the Kubernetes namespace where Radius resources will be deployed.
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="namespace">A valid Kubernetes namespace name (RFC 1123).</param>
    /// <returns>The same builder for method chaining.</returns>
    [AspireExport(Description = "Sets the Kubernetes namespace for the Radius environment")]
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
    /// Applies Radius-specific publishing customizations to a resource.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="configure">A delegate to configure <see cref="RadiusResourceCustomization"/>.</param>
    /// <returns>The same builder for method chaining.</returns>
    [AspireExportIgnore(Reason = "Action<RadiusResourceCustomization> callbacks are not ATS-compatible.")]
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

    /// <summary>
    /// Configures the Radius infrastructure AST before Bicep generation.
    /// The callback receives <see cref="RadiusInfrastructureOptions"/> with mutable access to
    /// all constructs (environments, applications, portable resources, containers).
    /// </summary>
    /// <param name="builder">The Radius environment resource builder.</param>
    /// <param name="configure">A delegate to configure <see cref="RadiusInfrastructureOptions"/>.</param>
    /// <returns>The same builder for method chaining.</returns>
    [AspireExportIgnore(Reason = "Action<RadiusInfrastructureOptions> callbacks are not ATS-compatible.")]
    public static IResourceBuilder<RadiusEnvironmentResource> ConfigureRadiusInfrastructure(
        this IResourceBuilder<RadiusEnvironmentResource> builder,
        Action<RadiusInfrastructureOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Resource.ConfigureCallback = configure;
        return builder;
    }
}

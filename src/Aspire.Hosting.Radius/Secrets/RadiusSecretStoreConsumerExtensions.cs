// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Secret-store types are experimental; the private helpers below consume them.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Secrets;

namespace Aspire.Hosting;

/// <summary>
/// Fluent entry points for consuming a declared Radius secret store from its documented Radius
/// consumers (environment <c>recipeConfig</c> authentication / <c>envSecrets</c>, and gateway
/// TLS). Each reference is emitted by the store's fully-qualified UCP secret-store ID
/// (<c>/planes/radius/local/resourceGroups/&lt;group&gt;/providers/Applications.Core/secretStores/&lt;name&gt;</c>).
/// All surface is experimental and gated by <c>ASPIRERADIUS006</c>.
/// </summary>
public static class RadiusSecretStoreConsumerExtensions
{
    /// <summary>Uses a <c>basicAuthentication</c> store as private Bicep-registry auth in <c>recipeConfig</c>.</summary>
    [Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithBicepRegistryAuthentication(
        this IResourceBuilder<RadiusEnvironmentResource> radius,
        string registryHost,
        IResourceBuilder<RadiusSecretStoreResource> store)
        => AddConsumer(radius, RadiusSecretStoreConsumerKind.BicepRegistryAuth, registryHost, store, key: null);

    /// <summary>Uses a <c>basicAuthentication</c> store as a Terraform Git PAT in <c>recipeConfig</c>.</summary>
    [Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithTerraformGitAuthentication(
        this IResourceBuilder<RadiusEnvironmentResource> radius,
        string gitHost,
        IResourceBuilder<RadiusSecretStoreResource> store)
        => AddConsumer(radius, RadiusSecretStoreConsumerKind.TerraformGitPat, gitHost, store, key: null);

    /// <summary>Uses a store as a Terraform provider <c>secrets</c> reference in <c>recipeConfig</c>.</summary>
    [Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithTerraformProviderSecret(
        this IResourceBuilder<RadiusEnvironmentResource> radius,
        string provider,
        IResourceBuilder<RadiusSecretStoreResource> store)
        => AddConsumer(radius, RadiusSecretStoreConsumerKind.TerraformProviderSecret, provider, store, key: null);

    /// <summary>Adds a <c>recipeConfig.envSecrets['&lt;VAR&gt;'] = { source: &lt;storeId&gt;, key: &lt;k&gt; }</c> entry.</summary>
    [Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExportIgnore]
    public static IResourceBuilder<RadiusEnvironmentResource> WithRecipeEnvironmentSecret(
        this IResourceBuilder<RadiusEnvironmentResource> radius,
        string variableName,
        IResourceBuilder<RadiusSecretStoreResource> store,
        string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return AddConsumer(radius, RadiusSecretStoreConsumerKind.EnvSecret, variableName, store, key);
    }

    /// <summary>
    /// Uses a <c>certificate</c> store as gateway TLS (<c>tls.certificateFrom</c>) where gateways
    /// are modeled. Gateways are not modeled by the integration today, so this records the
    /// intended reference and exposes the store's referenceable UCP ID.
    /// </summary>
    [Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
    [AspireExportIgnore]
    public static IResourceBuilder<T> WithTlsCertificate<T>(
        this IResourceBuilder<T> gateway,
        IResourceBuilder<RadiusSecretStoreResource> store)
        where T : IResource
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(store);

        // Record the intended TLS wiring on the store's owning environment when one is known.
        if (store.Resource.OwningEnvironment is { } environment)
        {
            RadiusSecretStoresAnnotation.GetOrAdd(environment).Consumers.Add(
                new RadiusSecretStoreConsumer(
                    RadiusSecretStoreConsumerKind.GatewayTls, store.Resource, gateway.Resource.Name, Key: null));
        }

        return gateway;
    }

    private static IResourceBuilder<RadiusEnvironmentResource> AddConsumer(
        IResourceBuilder<RadiusEnvironmentResource> radius,
        RadiusSecretStoreConsumerKind kind,
        string selector,
        IResourceBuilder<RadiusSecretStoreResource> store,
        string? key)
    {
        ArgumentNullException.ThrowIfNull(radius);
        ArgumentException.ThrowIfNullOrWhiteSpace(selector);
        ArgumentNullException.ThrowIfNull(store);

        RadiusSecretStoresAnnotation.GetOrAdd(radius.Resource).Consumers.Add(
            new RadiusSecretStoreConsumer(kind, store.Resource, selector, key));

        return radius;
    }
}

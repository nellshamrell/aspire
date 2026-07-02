// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Secret-store types are experimental; consumed internally by the integration.

using System.Diagnostics.CodeAnalysis;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Radius.Secrets;

namespace Aspire.Hosting.Radius;

/// <summary>
/// The scope a Radius secret store is emitted with. A <c>secretStores</c> resource
/// carries exactly one of <c>properties.application</c> / <c>properties.environment</c>;
/// the scope is implied by the declaring API form.
/// </summary>
internal enum RadiusSecretStoreScope
{
    /// <summary>Environment-scoped (<c>properties.environment</c>) — declared via <c>radius.WithSecretStore(...)</c>.</summary>
    Environment,

    /// <summary>Application-scoped (<c>properties.application</c>) — declared via <c>builder.AddRadiusSecretStore(...)</c>.</summary>
    Application,
}

/// <summary>
/// Represents a Radius <c>Applications.Core/secretStores@2023-10-01-preview</c> resource
/// in the Aspire app model. Referenceable by consumers (recipe-config auth, gateway TLS)
/// via its fully-qualified UCP secret-store ID. Declared via
/// <c>builder.AddRadiusSecretStore(...)</c> (application-scoped) or
/// <c>radius.WithSecretStore(...)</c> (environment-scoped).
/// </summary>
[Experimental("ASPIRERADIUS006", UrlFormat = "https://aka.ms/aspire/diagnostics/{0}")]
public sealed class RadiusSecretStoreResource : Resource
{
    internal RadiusSecretStoreResource(string name, RadiusSecretStoreType type, RadiusSecretStoreScope scope)
        : base(name)
    {
        Type = type;
        Scope = scope;
    }

    /// <summary>The Radius secret-store type (drives required-key validation and encoding defaults).</summary>
    public RadiusSecretStoreType Type { get; }

    /// <summary>Whether the store is emitted application- or environment-scoped (implied by the declaring API form).</summary>
    internal RadiusSecretStoreScope Scope { get; }

    /// <summary>The single declared population mode (inline / existing-secret / sealed-secret).</summary>
    internal RadiusSecretStorePopulation Population { get; } = new();

    /// <summary>
    /// The bounded time to wait for a sealed/referenced <c>Secret</c> to materialize
    /// in-cluster before <c>rad deploy</c>. Default 120s; overridable via
    /// <c>WithMaterializationTimeout</c>. Used only by the sealed-secrets deploy path.
    /// </summary>
    public TimeSpan MaterializationTimeout { get; internal set; } = TimeSpan.FromSeconds(120);

    /// <summary>
    /// The owning Radius environment, used to default a bare existing-secret reference's
    /// namespace to the environment's <see cref="RadiusEnvironmentResource.Namespace"/>.
    /// Set by <c>WithSecretStore</c>; may be resolved from the model for
    /// <c>AddRadiusSecretStore</c>.
    /// </summary>
    internal RadiusEnvironmentResource? OwningEnvironment { get; set; }

    /// <summary>
    /// Returns the fully-qualified UCP secret-store ID for this store in
    /// <paramref name="group"/>, e.g.
    /// <c>/planes/radius/local/resourceGroups/&lt;group&gt;/providers/Applications.Core/secretStores/&lt;name&gt;</c>.
    /// </summary>
    internal string ToUcpSecretStoreId(string group) =>
        RadiusResourceGroupReference.ToUcpResourceId(group, "Applications.Core/secretStores", Name);
}

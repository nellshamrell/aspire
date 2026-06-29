// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius;

/// <summary>
/// A reference to a scope field of a cloud provider configured on a Radius environment
/// (via <c>WithAzureProvider</c> / <c>WithAwsProvider</c>). Assign one as a recipe
/// parameter value to reuse provider configuration without re-declaring it, e.g.
/// <c>p["region"] = RadiusProviderReference.AwsRegion</c>.
/// </summary>
/// <remarks>
/// References are resolved at publish time against the cloud provider configured on the
/// same environment; they do not require capturing the provider-builder instance passed
/// to the <c>WithAzureProvider</c>/<c>WithAwsProvider</c> callback. Referencing a provider
/// that is not configured on the environment fails at publish time with a message naming
/// the missing provider.
/// </remarks>
public sealed class RadiusProviderReference
{
    private RadiusProviderReference(RadiusCloud cloud, RadiusProviderScopeField field)
    {
        Cloud = cloud;
        Field = field;
    }

    /// <summary>The cloud whose provider configuration this reference resolves against.</summary>
    internal RadiusCloud Cloud { get; }

    /// <summary>The specific provider scope field this reference resolves to.</summary>
    internal RadiusProviderScopeField Field { get; }

    /// <summary>The AWS region of the environment's configured AWS provider.</summary>
    public static RadiusProviderReference AwsRegion { get; } =
        new(RadiusCloud.Aws, RadiusProviderScopeField.Region);

    /// <summary>The 12-digit account ID of the environment's configured AWS provider.</summary>
    public static RadiusProviderReference AwsAccountId { get; } =
        new(RadiusCloud.Aws, RadiusProviderScopeField.AccountId);

    /// <summary>The subscription ID of the environment's configured Azure provider.</summary>
    public static RadiusProviderReference AzureSubscriptionId { get; } =
        new(RadiusCloud.Azure, RadiusProviderScopeField.SubscriptionId);

    /// <summary>The resource group of the environment's configured Azure provider.</summary>
    public static RadiusProviderReference AzureResourceGroup { get; } =
        new(RadiusCloud.Azure, RadiusProviderScopeField.ResourceGroup);
}

/// <summary>
/// Identifies which cloud-provider scope field a <see cref="RadiusProviderReference"/>
/// resolves to at publish time.
/// </summary>
internal enum RadiusProviderScopeField
{
    /// <summary>AWS region.</summary>
    Region,

    /// <summary>AWS account ID.</summary>
    AccountId,

    /// <summary>Azure subscription ID.</summary>
    SubscriptionId,

    /// <summary>Azure resource group.</summary>
    ResourceGroup,
}

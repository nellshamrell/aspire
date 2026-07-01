// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-008, US4 — feature 003/004/005 configuration composes per group: each group's provider
/// scope, namespace, and recipe parameters land in that group's <c>app.bicep</c> only, never
/// bleeding into a sibling group's artifact.
/// </summary>
public class PerGroupProviderConfigBicepTests
{
    private const string WebSub = "11111111-1111-1111-1111-111111111111";
    private const string WebRg = "rg-web";
    private const string AzureTenant = "22222222-2222-2222-2222-222222222222";
    private const string AzureClient = "33333333-3333-3333-3333-333333333333";
    private const string DataAccount = "123456789012";
    private const string DataRegion = "us-west-2";

    [Fact]
    public void PerGroupProviderNamespaceAndRecipeParameters_LandInCorrectGroupOnly()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // web group: Azure provider, web-ns namespace, a web-scoped recipe parameter.
        builder.AddRadiusEnvironment("env-web")
            .WithRadiusResourceGroup("web")
            .WithNamespace("web-ns")
            .WithAzureProvider(WebSub, WebRg, azure => azure.WithWorkloadIdentity(AzureTenant, AzureClient))
            .WithRecipeParameters(p => p["tier"] = "web-tier");
        builder.AddRedis("webcache").WithRadiusResourceGroup("web");
        builder.AddContainer("webapi", "img", "latest").WithRadiusResourceGroup("web");

        // data group: AWS provider, data-ns namespace, a data-scoped recipe parameter.
        builder.AddRadiusEnvironment("env-data")
            .WithRadiusResourceGroup("data")
            .WithNamespace("data-ns")
            .WithAwsProvider(DataAccount, DataRegion, aws => aws.WithIrsa("arn:aws:iam::123456789012:role/radius"))
            .WithRecipeParameters(p => p["tier"] = "data-tier");
        builder.AddRedis("datacache").WithRadiusResourceGroup("data");
        builder.AddContainer("dataapi", "img", "latest").WithRadiusResourceGroup("data");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var bicepByGroup = RadiusBicepPublishingContext.GenerateGroupedBicep(model);
        var web = bicepByGroup["web"];
        var data = bicepByGroup["data"];

        // web group carries only its own namespace, Azure provider, and recipe parameter.
        Assert.Contains("namespace: 'web-ns'", web);
        Assert.Contains($"subscriptionId: '{WebSub}'", web);
        Assert.Contains($"resourceGroupName: '{WebRg}'", web);
        Assert.Contains("tier: 'web-tier'", web);
        Assert.DoesNotContain("namespace: 'data-ns'", web);
        Assert.DoesNotContain($"accountId: '{DataAccount}'", web);
        Assert.DoesNotContain("tier: 'data-tier'", web);

        // data group carries only its own namespace, AWS provider, and recipe parameter.
        Assert.Contains("namespace: 'data-ns'", data);
        Assert.Contains($"accountId: '{DataAccount}'", data);
        Assert.Contains($"region: '{DataRegion}'", data);
        Assert.Contains("tier: 'data-tier'", data);
        Assert.DoesNotContain("namespace: 'web-ns'", data);
        Assert.DoesNotContain($"subscriptionId: '{WebSub}'", data);
        Assert.DoesNotContain("tier: 'web-tier'", data);
    }
}

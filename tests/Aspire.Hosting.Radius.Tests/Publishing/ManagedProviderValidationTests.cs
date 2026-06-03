// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// The provider-configured invariant (<c>ASPIRERADIUS020</c>) for cloud-managed resources is
/// validated at publish time, so it is independent of the order in which providers and managed
/// selections are configured on the environment.
/// </summary>
public class ManagedProviderValidationTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string Account = "123456789012";
    private const string Region = "us-west-2";
    private const string Arn = "arn:aws:iam::123456789012:role/radius-irsa";
    private const string AzureRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";

    private static string GenerateBicep(DistributedApplication app)
    {
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        return new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);
    }

    [Fact]
    public void Publish_ManagedResource_NoMatchingProvider_Throws_ASPIRERADIUS020()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv"); // no provider configured
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe });

        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => GenerateBicep(app));
        Assert.Contains("ASPIRERADIUS020", ex.Message);
        Assert.Contains("cache", ex.Message);
    }

    [Fact]
    public void Publish_AzureSelection_AwsProviderOnly_Throws_ASPIRERADIUS020()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAwsProvider(Account, Region, aws => aws.WithIrsa(Arn));
        var cache = builder.AddRedis("cache");
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe });

        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();

        var ex = Assert.Throws<InvalidOperationException>(() => GenerateBicep(app));
        Assert.Contains("ASPIRERADIUS020", ex.Message);
    }

    [Fact]
    public void Publish_ManagedResourceMarkedBeforeProviderConfigured_Succeeds()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv");
        var cache = builder.AddRedis("cache");

        // Mark the resource cloud-managed BEFORE configuring the provider. Because the provider
        // check runs at publish time, this ordering is valid and must publish successfully.
        env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe });
        env.WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var bicep = GenerateBicep(app);

        Assert.Contains(AzureRecipe, bicep);
        Assert.Contains($"/subscriptions/{Sub}/resourceGroups/{Rg}", bicep);
    }
}

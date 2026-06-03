// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;

namespace Aspire.Hosting.Radius.Tests.Managed;

public class ManagedValidationTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string Account = "123456789012";
    private const string Region = "us-west-2";
    private const string Arn = "arn:aws:iam::123456789012:role/radius-irsa";
    private const string AzureRecipe = "br:reg.azurecr.io/recipes/azure-rediscache:latest";
    private const string AwsRecipe = "br:public.ecr.aws/recipes/aws-elasticache:latest";

    private static IResourceBuilder<RadiusEnvironmentResource> AzureEnv(IDistributedApplicationBuilder builder)
        => builder.AddRadiusEnvironment("radius")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

    [Fact]
    public void Managed_ForCloudWithoutProvider_Throws_ASPIRERADIUS020()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius"); // no provider configured
        var cache = builder.AddRedis("cache");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS020", ex.Message);
        Assert.Contains("cache", ex.Message);
    }

    [Fact]
    public void Managed_AzureSelection_AwsProviderOnly_Throws_ASPIRERADIUS020()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius")
            .WithAwsProvider(Account, Region, aws => aws.WithIrsa(Arn));
        var cache = builder.AddRedis("cache");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Contains("ASPIRERADIUS020", ex.Message);
    }

    [Fact]
    public void Managed_OnContainerCompute_Throws_ASPIRERADIUS022()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var api = builder.AddContainer("api", "myapp/api", "latest");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(api, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS022", ex.Message);
        Assert.Contains("api", ex.Message);
    }

    [Fact]
    public void Managed_CloudConflictsWithRecipe_Throws_ASPIRERADIUS021()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        // Cloud is Azure but the recipe location clearly targets AWS.
        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AwsRecipe }));

        Assert.Contains("ASPIRERADIUS021", ex.Message);
    }

    [Fact]
    public void Managed_AwsSelection_AwsProviderConfigured_Succeeds()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius")
            .WithAwsProvider(Account, Region, aws => aws.WithIrsa(Arn));
        var storage = builder.AddRedis("storage");

        var ex = Record.Exception(() =>
            env.WithManagedResource(storage, RadiusCloud.Aws, new RadiusRecipe { RecipeLocation = AwsRecipe }));

        Assert.Null(ex);
    }

    [Fact]
    public void Managed_CloudAgnosticRecipe_DoesNotTrigger_ASPIRERADIUS021()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        // Location names neither cloud → no declared cloud → no conflict.
        var ex = Record.Exception(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = "br:reg.example.io/recipes/rediscache:latest" }));

        Assert.Null(ex);
    }

    [Fact]
    public void Managed_RecipeWithoutLocation_Throws_ASPIRERADIUS023()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe()));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS023", ex.Message);
        Assert.Contains("cache", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Managed_RecipeWithEmptyOrWhitespaceLocation_Throws_ASPIRERADIUS023(string location)
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = location }));

        Assert.Contains("ASPIRERADIUS023", ex.Message);
    }

    [Fact]
    public void Managed_OnChildResource_Throws_ASPIRERADIUS024()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var db = builder.AddSqlServer("sql").AddDatabase("db");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(db, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS024", ex.Message);
        Assert.Contains("sql", ex.Message);
    }

    [Fact]
    public void Managed_OnUnsupportedResource_Throws_ASPIRERADIUS025()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var parameter = builder.AddParameter("param", "value");

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(parameter, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS025", ex.Message);
        Assert.Contains("param", ex.Message);
    }

    [Fact]
    public void Managed_OnResourceWithNonComputeTypeOverride_DoesNotThrow_ASPIRERADIUS025()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);

        // A resource with no built-in backing mapping, mapped via a custom TypeOverride to a
        // non-compute UDT. Publish resolves the override before the mapper, so configuration-time
        // validation must honor it too rather than rejecting with ASPIRERADIUS025.
        var custom = builder.AddParameter("param", "value")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("MyOrg.Custom/myCache", "2025-01-01"));

        var ex = Record.Exception(() =>
            env.WithManagedResource(custom, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Null(ex);
    }
}

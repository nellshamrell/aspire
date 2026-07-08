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

    private static IResourceBuilder<RadiusEnvironmentResource> AzureEnv(IDistributedApplicationBuilder builder)
        => builder.AddRadiusEnvironment("radius")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));

    [Fact]
    public void Managed_BeforeProviderConfigured_DoesNotThrow_AtConfigTime()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius"); // no provider configured yet
        var cache = builder.AddRedis("cache");

        // Provider-configured (ASPIRERADIUS020) is a cross-resource invariant validated at
        // publish time, so marking a resource cloud-managed before (or without) configuring
        // the provider must not throw when WithManagedResource is called.
        var ex = Record.Exception(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Null(ex);
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
    public void Managed_OnResourceWithComputeTypeOverride_Throws_ASPIRERADIUS022()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);

        // A backing resource retargeted to the compute Containers type via TypeOverride
        // resolves to compute at publish, so it must be rejected as compute here rather than
        // slipping past both ASPIRERADIUS022 and ASPIRERADIUS025.
        var cache = builder.AddRedis("cache")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("Radius.Compute/containers", "2025-08-01-preview"));

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS022", ex.Message);
        Assert.Contains("cache", ex.Message);
    }

    [Fact]
    public void Managed_AwsRecipeInAzureRegistry_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius")
            .WithAwsProvider(Account, Region, aws => aws.WithIrsa(Arn));
        var cache = builder.AddRedis("cache");

        // An AWS recipe published to an Azure Container Registry has a host containing
        // "azure" (azurecr.io). The cloud is taken from the explicit RadiusCloud argument,
        // never inferred from the location string, so this valid config must not throw.
        var ex = Record.Exception(() =>
            env.WithManagedResource(cache, RadiusCloud.Aws, new RadiusRecipe { RecipeLocation = "br:reg.azurecr.io/recipes/aws-elasticache:latest" }));

        Assert.Null(ex);
    }

    [Fact]
    public void Managed_AzureRecipeInAwsRegistry_DoesNotThrow()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);
        var cache = builder.AddRedis("cache");

        // The inverse: an Azure recipe hosted on a public ECR (host contains "aws").
        var ex = Record.Exception(() =>
            env.WithManagedResource(cache, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = "br:public.ecr.aws/recipes/azure-rediscache:latest" }));

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

    [Fact]
    public void Managed_OnContainerWithNonComputeTypeOverride_DoesNotThrow_ASPIRERADIUS022()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);

        // A plain container retargeted to a non-compute UDT via TypeOverride resolves to a Radius
        // backing resource at publish (ResolveResourceType honors the override; ClassifyResources
        // routes it to the radius types), so config-time validation must accept it rather than
        // falsely rejecting it as compute — ValidateNotCompute runs before ValidateSupportedBackingResource.
        var api = builder.AddContainer("api", "myapp/api", "latest")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("MyOrg.Custom/myCache", "2025-01-01"));

        var ex = Record.Exception(() =>
            env.WithManagedResource(api, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Null(ex);
    }

    [Fact]
    public void Managed_OnProjectWithNonComputeTypeOverride_Throws_ASPIRERADIUS022()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = AzureEnv(builder);

        // Projects are always compute at publish — ClassifyResources classifies a ProjectResource
        // as compute regardless of any override — so a non-compute TypeOverride must NOT let a
        // project be marked cloud-managed. The non-compute-override bypass is scoped to non-projects.
        var project = builder.AddProject<TestProjectMetadata>("webapp")
            .PublishAsRadiusResource(r => r.TypeOverride = new RadiusResourceTypeReference("MyOrg.Custom/myCache", "2025-01-01"));

        var ex = Assert.Throws<ArgumentException>(() =>
            env.WithManagedResource(project, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureRecipe }));

        Assert.Equal("resource", ex.ParamName);
        Assert.Contains("ASPIRERADIUS022", ex.Message);
        Assert.Contains("webapp", ex.Message);
    }

    private sealed class TestProjectMetadata : IProjectMetadata
    {
        public string ProjectPath => "testproject";
        public LaunchSettings LaunchSettings { get; } = new();
    }
}

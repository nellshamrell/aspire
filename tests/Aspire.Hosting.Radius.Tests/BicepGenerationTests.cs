// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Radius.Tests.TestHosts;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests;

public class BicepGenerationTests
{
    // ---- T042: Extension directive ----

    [Fact]
    public void Generated_bicep_starts_with_extension_radius()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.StartsWith("extension radius", bicep);
    }

    // ---- T043: Environment resource ----

    [Fact]
    public void Generated_bicep_contains_environment_resource()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("resource radiusenv 'Applications.Core/environments@", bicep);
        Assert.Contains("name: 'radius-env'", bicep);
        Assert.Contains("kind: 'kubernetes'", bicep);
        Assert.Contains("namespace: 'default'", bicep);
    }

    // ---- T044: Application resource ----

    [Fact]
    public void Generated_bicep_contains_application_resource()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("resource radiusenv_app 'Applications.Core/applications@", bicep);
        Assert.Contains("name: 'radius-app'", bicep);
        Assert.Contains("environment: radiusenv.id", bicep);
    }

    // ---- T045: Portable resources ----

    [Fact]
    public void Generated_bicep_contains_redis_portable_resource()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("resource cache 'Applications.Datastores/redisCaches@", bicep);
        Assert.Contains("name: 'cache'", bicep);
        Assert.Contains("application: radiusenv_app.id", bicep);
    }

    [Fact]
    public void Generated_bicep_contains_postgres_portable_resource()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("resource db 'Applications.Datastores/postgresDatabases@", bicep);
        Assert.Contains("name: 'db'", bicep);
    }

    // ---- T046: Container resource ----

    [Fact]
    public void Generated_bicep_contains_container_resource()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("resource api 'Applications.Core/containers@", bicep);
        Assert.Contains("name: 'api'", bicep);
        Assert.Contains("image: 'myapp/api:latest'", bicep);
    }

    // ---- T047: Container connections ----

    [Fact]
    public void Generated_bicep_contains_container_connections()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("connections: {", bicep);
        Assert.Contains("source: cache.id", bicep);
    }

    // ---- T048: Recipe config in environment ----

    [Fact]
    public void Generated_bicep_contains_recipe_config()
    {
        var bicep = GenerateBicepFromMixedResources();

        Assert.Contains("recipes: {", bicep);
        Assert.Contains("'Applications.Datastores/redisCaches'", bicep);
    }

    // ---- T049: Custom recipe ----

    [Fact]
    public void Custom_recipe_emits_recipe_block_in_portable_resource()
    {
        var bicep = GenerateBicepFromCustomRecipe();

        Assert.Contains("recipe: {", bicep);
        Assert.Contains("name: 'azure-redis'", bicep);
    }

    [Fact]
    public void Custom_recipe_registers_in_environment_config()
    {
        var bicep = GenerateBicepFromCustomRecipe();

        Assert.Contains("azure-redis: {", bicep);
        Assert.Contains("templatePath: 'ghcr.io/myorg/recipes/azure-redis:1.0'", bicep);
    }

    // ---- T050: Manual provisioning ----

    [Fact]
    public void Manual_provisioning_emits_manual_fields()
    {
        var bicep = GenerateBicepFromManualProvisioning();

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("host: 'redis.example.com'", bicep);
        Assert.Contains("port: 6379", bicep);
    }

    [Fact]
    public void Manual_provisioning_does_not_emit_recipe_block()
    {
        var bicep = GenerateBicepFromManualProvisioning();

        Assert.DoesNotContain("recipe: {", bicep);
    }

    // ---- T051: Recipe with parameters ----

    [Fact]
    public void Recipe_with_parameters_emits_parameters_block()
    {
        var bicep = GenerateBicepFromRecipeWithParameters();

        Assert.Contains("recipe: {", bicep);
        Assert.Contains("parameters: {", bicep);
        Assert.Contains("sku: 'Premium'", bicep);
        Assert.Contains("capacity: 2", bicep);
        Assert.Contains("enableNonSslPort: false", bicep);
    }

    // ---- T052: Recipe + manual mutual exclusivity ----

    [Fact]
    public void Recipe_and_manual_provisioning_throws()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");

        builder.AddRedis("cache")
            .PublishAsRadiusResource(options =>
            {
                options.Recipe = new RadiusRecipe
                {
                    Name = "custom",
                    TemplatePath = "ghcr.io/custom:1.0"
                };
                options.Provisioning = RadiusResourceProvisioning.Manual;
            });

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model,
            env,
            NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(infraBuilder.Build);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    // ---- T035: ConfigureRadiusInfrastructure callback ----

    [Fact]
    public void ConfigureRadiusInfrastructure_callback_is_invoked()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var callbackInvoked = false;
        builder.AddRadiusEnvironment("radius")
            .ConfigureRadiusInfrastructure(options =>
            {
                callbackInvoked = true;
                Assert.NotEmpty(options.Environments);
                Assert.NotEmpty(options.Applications);
            });

        builder.AddRedis("cache");

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model,
            env,
            NullLogger.Instance,
            env.ConfigureCallback);

        infraBuilder.Build();

        Assert.True(callbackInvoked);
    }

    // ---- T044: Custom namespace ----

    [Fact]
    public void Custom_namespace_appears_in_bicep()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("my-namespace");

        builder.AddRedis("cache");

        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model,
            env,
            NullLogger.Instance);

        var bicep = infraBuilder.Build();

        Assert.Contains("namespace: 'my-namespace'", bicep);
    }

    // ---- SerializeBicepValue ----

    [Theory]
    [InlineData("hello", "'hello'")]
    [InlineData(42, "42")]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void SerializeBicepValue_formats_correctly(object input, string expected)
    {
        var result = RadiusInfrastructureBuilder.SerializeBicepValue(input);
        Assert.Equal(expected, result);
    }

    // ---- Helpers ----

    private static string GenerateBicepFromMixedResources()
    {
        var builder = MixedResourcesAppHost.CreateBuilder();
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model, env, NullLogger.Instance);

        return infraBuilder.Build();
    }

    private static string GenerateBicepFromCustomRecipe()
    {
        var builder = MixedResourcesAppHost.CreateWithCustomRecipe();
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model, env, NullLogger.Instance);

        return infraBuilder.Build();
    }

    private static string GenerateBicepFromManualProvisioning()
    {
        var builder = MixedResourcesAppHost.CreateWithManualProvisioning();
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model, env, NullLogger.Instance);

        return infraBuilder.Build();
    }

    private static string GenerateBicepFromRecipeWithParameters()
    {
        var builder = MixedResourcesAppHost.CreateWithRecipeParameters();
        var model = RadiusTestHelper.GetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(
            model, env, NullLogger.Instance);

        return infraBuilder.Build();
    }
}

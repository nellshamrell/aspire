#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentExtensionsTests
{
    [Fact]
    public void AddRadiusEnvironment_AddsResourceToModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.Equal("radius", env.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_UsesDefaultName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment();

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.Equal("radius", env.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_UsesCustomName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("my-radius");

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.Equal("my-radius", env.Name);
    }

    [Fact]
    public void WithRadiusNamespace_SetsNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("staging");

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.Equal("staging", env.Namespace);
    }

    [Fact]
    public void WithDashboard_EnablesDashboard()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(true);

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.True(env.DashboardEnabled);
    }

    [Fact]
    public void WithDashboard_DisablesDashboard()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithDashboard(false);

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.False(env.DashboardEnabled);
    }

    [Fact]
    public void FluentChaining_WorksCorrectly()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("production")
            .WithDashboard(false);

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var env = RadiusTestHelper.GetRadiusEnvironment(model);

        Assert.Equal("production", env.Namespace);
        Assert.False(env.DashboardEnabled);
    }

    [Fact]
    public void PublishAsRadiusResource_AttachesAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("api", "myimage", "latest")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Manual;
                config.Host = "localhost";
                config.Port = 5432;
            });

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var api = model.Resources.First(r => r.Name == "api");
        var annotation = api.Annotations.OfType<RadiusResourceCustomizationAnnotation>().FirstOrDefault();

        Assert.NotNull(annotation);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
        Assert.Equal("localhost", annotation.Customization.Host);
        Assert.Equal(5432, annotation.Customization.Port);
    }

    [Fact]
    public void PublishAsRadiusResource_WithRecipe_AttachesAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("api", "myimage", "latest")
            .PublishAsRadiusResource(config =>
            {
                config.Recipe = new RadiusRecipe
                {
                    Name = "custom-recipe",
                    TemplatePath = "ghcr.io/my-org/recipes/custom:latest"
                };
            });

        var model = RadiusTestHelper.BuildAndGetModel(builder);
        var api = model.Resources.First(r => r.Name == "api");
        var annotation = api.Annotations.OfType<RadiusResourceCustomizationAnnotation>().FirstOrDefault();

        Assert.NotNull(annotation);
        Assert.NotNull(annotation.Customization.Recipe);
        Assert.Equal("custom-recipe", annotation.Customization.Recipe.Name);
    }

    [Fact]
    public async Task ConfigureRadiusInfrastructure_CanMutateGeneratedConstructs()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius")
            .ConfigureRadiusInfrastructure(options =>
            {
                var environment = Assert.Single(options.Environments);
                environment.ComputeNamespace = "custom-namespace";
            });

        var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
        var publishingContext = new RadiusBicepPublishingContext(
            model,
            loggerFactory.CreateLogger<RadiusBicepPublishingContext>());

        var bicep = await publishingContext.GenerateBicepAsync();

        Assert.Contains("namespace: 'custom-namespace'", bicep);
    }

    [System.Runtime.CompilerServices.UnsafeAccessor(
        System.Runtime.CompilerServices.UnsafeAccessorKind.Method,
        Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(
        DistributedApplication app,
        CancellationToken cancellationToken);
}

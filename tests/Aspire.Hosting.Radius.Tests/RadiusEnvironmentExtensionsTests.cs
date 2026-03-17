// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentExtensionsTests
{
    [Fact]
    public void AddRadiusEnvironment_CreatesResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment();

        Assert.NotNull(radiusBuilder);
        Assert.Equal("radius", radiusBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_WithCustomName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment("staging");

        Assert.Equal("staging", radiusBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_HasDefaultProperties()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment();

        Assert.Equal("default", radiusBuilder.Resource.Namespace);
        Assert.True(radiusBuilder.Resource.DashboardEnabled);
    }

    [Fact]
    public void AddRadiusEnvironment_RegistersResourceInModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment("test-env");

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var env = model.Resources.OfType<RadiusEnvironmentResource>().SingleOrDefault();
        Assert.NotNull(env);
        Assert.Equal("test-env", env.Name);
    }

    [Fact]
    public void WithDashboard_EnablesDashboard()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment()
            .WithDashboard(true);

        Assert.True(radiusBuilder.Resource.DashboardEnabled);
    }

    [Fact]
    public void WithDashboard_DisablesDashboard()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment()
            .WithDashboard(false);

        Assert.False(radiusBuilder.Resource.DashboardEnabled);
    }

    [Fact]
    public void WithRadiusNamespace_SetsNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment()
            .WithRadiusNamespace("custom-ns");

        Assert.Equal("custom-ns", radiusBuilder.Resource.Namespace);
    }

    [Fact]
    public void MethodChaining_WorksCorrectly()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        var radiusBuilder = builder.AddRadiusEnvironment("production")
            .WithRadiusNamespace("prod-ns")
            .WithDashboard(false);

        Assert.Equal("production", radiusBuilder.Resource.Name);
        Assert.Equal("prod-ns", radiusBuilder.Resource.Namespace);
        Assert.False(radiusBuilder.Resource.DashboardEnabled);
    }

    [Fact]
    public void PublishAsRadiusResource_AttachesAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        builder.AddRadiusEnvironment();

        var container = builder.AddContainer("webapi", "myimage:latest")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Recipe = "my-recipe";
                cfg.Provisioning = Radius.Models.RadiusResourceProvisioning.Manual;
                cfg.Host = "db.example.com";
                cfg.Port = 5432;
            });

        var annotation = container.Resource.Annotations
            .OfType<Radius.Annotations.RadiusResourceCustomizationAnnotation>()
            .SingleOrDefault();

        Assert.NotNull(annotation);
        Assert.Equal("my-recipe", annotation.Customization.Recipe);
        Assert.Equal(Radius.Models.RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
        Assert.Equal("db.example.com", annotation.Customization.Host);
        Assert.Equal(5432, annotation.Customization.Port);
    }

    [Fact]
    public void AddRadiusEnvironment_ThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddRadiusEnvironment());
    }

    [Fact]
    public void AddRadiusEnvironment_ThrowsOnEmptyName()
    {
        using var builder = TestDistributedApplicationBuilder.Create();

        Assert.Throws<ArgumentException>(() => builder.AddRadiusEnvironment(""));
    }
}

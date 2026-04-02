// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentExtensionsTests
{
    [Fact]
    public void AddRadiusEnvironment_AddsResourceToModel()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.Equal("radius", resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_DefaultName_IsRadius()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.Equal("radius", resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_CustomName()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("myenv");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.Equal("myenv", resource.Name);
        Assert.Equal("myenv", resource.EnvironmentName);
    }

    [Fact]
    public void WithRadiusNamespace_SetsNamespace()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithRadiusNamespace("staging");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.Equal("staging", resource.Namespace);
    }

    [Fact]
    public void WithDashboard_EnablesDashboard()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(true);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.True(resource.DashboardEnabled);
    }

    [Fact]
    public void WithDashboard_DisablesDashboard()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard(false);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.False(resource.DashboardEnabled);
    }

    [Fact]
    public void WithDashboard_DefaultParameter_EnablesDashboard()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius")
               .WithDashboard();

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var resource = Assert.Single(model.Resources.OfType<RadiusEnvironmentResource>());
        Assert.True(resource.DashboardEnabled);
    }

    [Fact]
    public void PublishAsRadiusResource_AttachesAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();

        builder.AddContainer("myapp", "myimage:latest")
               .PublishAsRadiusResource(config =>
               {
                   config.Provisioning = RadiusResourceProvisioning.Manual;
                   config.Host = "db.example.com";
                   config.Port = 5432;
               });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var container = model.Resources.Single(r => r.Name == "myapp");
        var annotation = Assert.Single(container.Annotations.OfType<RadiusResourceCustomizationAnnotation>());
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
        Assert.Equal("db.example.com", annotation.Customization.Host);
        Assert.Equal(5432, annotation.Customization.Port);
    }

    [Fact]
    public void MethodsCanBeChained()
    {
        var builder = DistributedApplication.CreateBuilder();

        var result = builder.AddRadiusEnvironment("radius")
                            .WithRadiusNamespace("production")
                            .WithDashboard(false);

        Assert.NotNull(result);
        Assert.Equal("production", result.Resource.Namespace);
        Assert.False(result.Resource.DashboardEnabled);
    }
}

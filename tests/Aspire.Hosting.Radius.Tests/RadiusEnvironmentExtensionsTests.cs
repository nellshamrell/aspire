// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentExtensionsTests
{
    [Fact]
    public void AddRadiusEnvironment_CreatesResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment("radius");

        Assert.NotNull(resourceBuilder);
        Assert.Equal("radius", resourceBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_DefaultNameIsRadius()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment();

        Assert.Equal("radius", resourceBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_CustomName()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment("staging");

        Assert.Equal("staging", resourceBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_InPublishMode_AddsToModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.Contains(model.Resources, r => r is RadiusEnvironmentResource && r.Name == "radius");
    }

    [Fact]
    public void AddRadiusEnvironment_InRunMode_DoesNotAddToModel()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddRadiusEnvironment("radius");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.DoesNotContain(model.Resources, r => r is RadiusEnvironmentResource);
    }

    [Fact]
    public void AddRadiusEnvironment_ThrowsOnNullBuilder()
    {
        IDistributedApplicationBuilder builder = null!;

        Assert.Throws<ArgumentNullException>(() => builder.AddRadiusEnvironment("radius"));
    }

    [Fact]
    public void AddRadiusEnvironment_ThrowsOnEmptyName()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        Assert.Throws<ArgumentException>(() => builder.AddRadiusEnvironment(""));
    }

    [Fact]
    public void WithRadiusNamespace_SetsNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment("radius")
            .WithRadiusNamespace("production");

        Assert.Equal("production", resourceBuilder.Resource.Namespace);
    }

    [Fact]
    public void WithRadiusNamespace_ThrowsOnEmptyNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment("radius");

        Assert.Throws<ArgumentException>(() => resourceBuilder.WithRadiusNamespace(""));
    }

    [Fact]
    public void WithRadiusNamespace_DefaultNamespaceIsDefault()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var resourceBuilder = builder.AddRadiusEnvironment("radius");

        Assert.Equal("default", resourceBuilder.Resource.Namespace);
    }

    [Fact]
    public void PublishAsRadiusResource_AttachesAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        var container = builder.AddContainer("mycontainer", "nginx")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
            });

        var annotation = container.Resource.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .FirstOrDefault();

        Assert.NotNull(annotation);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
    }

    [Fact]
    public void PublishAsRadiusResource_WithRecipe_SetsRecipeOnAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");

        var container = builder.AddContainer("mycontainer", "nginx")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Recipe = new RadiusRecipe
                {
                    Name = "custom-recipe",
                    TemplatePath = "ghcr.io/myorg/recipes/nginx:latest",
                };
            });

        var annotation = container.Resource.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .Single();

        Assert.NotNull(annotation.Customization.Recipe);
        Assert.Equal("custom-recipe", annotation.Customization.Recipe.Name);
    }

    [Fact]
    public void PublishAsRadiusResource_ThrowsOnNullConfigure()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var container = builder.AddContainer("mycontainer", "nginx");

        Assert.Throws<ArgumentNullException>(() => container.PublishAsRadiusResource(null!));
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentResourceTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var resource = new RadiusEnvironmentResource("my-env");

        Assert.Equal("my-env", resource.Name);
    }

    [Fact]
    public void Namespace_DefaultsToDefault()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.Equal("default", resource.Namespace);
    }

    [Fact]
    public void Namespace_CanBeSet()
    {
        var resource = new RadiusEnvironmentResource("radius")
        {
            Namespace = "production"
        };

        Assert.Equal("production", resource.Namespace);
    }

    [Fact]
    public void ImplementsIComputeEnvironmentResource()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.IsAssignableFrom<ApplicationModel.IComputeEnvironmentResource>(resource);
    }

    [Fact]
    public void AddRadiusEnvironment_DefaultName()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var envBuilder = builder.AddRadiusEnvironment();

        Assert.Equal("radius", envBuilder.Resource.Name);
    }

    [Fact]
    public void AddRadiusEnvironment_CustomName()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var envBuilder = builder.AddRadiusEnvironment("staging");

        Assert.Equal("staging", envBuilder.Resource.Name);
    }

    [Fact]
    public void WithRadiusNamespace_SetsNamespace()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var envBuilder = builder.AddRadiusEnvironment()
            .WithRadiusNamespace("custom-ns");

        Assert.Equal("custom-ns", envBuilder.Resource.Namespace);
    }

    [Fact]
    public void AddRadiusEnvironment_MultipleCalls_Idempotent()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("env1");
        builder.AddRadiusEnvironment("env2");

        // Both should be registered without error
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<ApplicationModel.DistributedApplicationModel>();
        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();

        Assert.Equal(2, environments.Length);
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
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        Assert.Throws<ArgumentException>(() => builder.AddRadiusEnvironment(""));
    }
}

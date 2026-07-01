// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;

namespace Aspire.Hosting.Radius.Tests.ResourceGroups;

public class WithRadiusResourceGroupTests
{
    [Fact]
    public void SingleArg_SetsGroup_NoEnvironmentGroup_ReturnsSameBuilder()
    {
        var builder = DistributedApplication.CreateBuilder();
        var cache = builder.AddContainer("cache", "redis", "latest");

        var returned = cache.WithRadiusResourceGroup("shared-data");

        Assert.Same(cache, returned);
        var annotation = cache.Resource.Annotations.OfType<RadiusResourceGroupAnnotation>().Single();
        Assert.Equal("shared-data", annotation.Group);
        Assert.Null(annotation.EnvironmentGroup);
        Assert.Equal("shared-data", annotation.EffectiveEnvironmentGroup);
    }

    [Fact]
    public void TwoArg_SetsEnvironmentGroupOverride()
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("orders", "orders-img", "latest");

        service.WithRadiusResourceGroup("orders", "platform");

        var annotation = service.Resource.Annotations.OfType<RadiusResourceGroupAnnotation>().Single();
        Assert.Equal("orders", annotation.Group);
        Assert.Equal("platform", annotation.EnvironmentGroup);
        Assert.Equal("platform", annotation.EffectiveEnvironmentGroup);
    }

    [Fact]
    public void RepeatedCalls_LastWins_SingleAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        service.WithRadiusResourceGroup("a");
        service.WithRadiusResourceGroup("b", "env-b");

        var annotation = service.Resource.Annotations.OfType<RadiusResourceGroupAnnotation>().Single();
        Assert.Equal("b", annotation.Group);
        Assert.Equal("env-b", annotation.EnvironmentGroup);
    }

    [Fact]
    public void SingleArg_RoutesEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius");

        env.WithRadiusResourceGroup("platform");

        var annotation = env.Resource.Annotations.OfType<RadiusResourceGroupAnnotation>().Single();
        Assert.Equal("platform", annotation.Group);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceGroup_ThrowsArgumentException_ASPIRERADIUS033(string group)
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        var ex = Assert.Throws<ArgumentException>(() => service.WithRadiusResourceGroup(group));
        Assert.Contains("ASPIRERADIUS033", ex.Message);
    }

    [Fact]
    public void TwoArg_OnEnvironmentBuilder_ThrowsArgumentException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius");

        // An environment does not deploy against another environment (contracts §1.1).
        Assert.Throws<ArgumentException>(() => env.WithRadiusResourceGroup("platform", "data"));
    }

    [Fact]
    public void NullBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => RadiusResourceGroupExtensions.WithRadiusResourceGroup<ContainerResource>(null!, "g"));
    }
}

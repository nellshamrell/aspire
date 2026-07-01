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

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../escape")]
    [InlineData("foo..bar")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData(".hidden")]
    [InlineData("trailingdot.")]
    [InlineData("has\tcontrol")]
    [InlineData("a:b")]
    [InlineData("a<b")]
    [InlineData("a|b")]
    [InlineData("a*b")]
    [InlineData("CON")]
    [InlineData("nul")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    [InlineData("CON.bicep")]
    public void UnsafeGroupName_ThrowsArgumentException_ASPIRERADIUS033(string group)
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        var ex = Assert.Throws<ArgumentException>(() => service.WithRadiusResourceGroup(group));
        Assert.Contains("ASPIRERADIUS033", ex.Message);
    }

    [Fact]
    public void GroupNameExceedingMaxLength_ThrowsArgumentException_ASPIRERADIUS033()
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        var ex = Assert.Throws<ArgumentException>(() => service.WithRadiusResourceGroup(new string('a', 91)));
        Assert.Contains("ASPIRERADIUS033", ex.Message);
    }

    [Fact]
    public void UnsafeEnvironmentGroupName_ThrowsArgumentException_ASPIRERADIUS033()
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        var ex = Assert.Throws<ArgumentException>(() => service.WithRadiusResourceGroup("orders", "../platform"));
        Assert.Contains("ASPIRERADIUS033", ex.Message);
    }

    [Theory]
    [InlineData("platform")]
    [InlineData("shared-data")]
    [InlineData("orders_v2")]
    [InlineData("Group.Name")]
    public void SafeGroupName_DoesNotThrow(string group)
    {
        var builder = DistributedApplication.CreateBuilder();
        var service = builder.AddContainer("svc", "img", "latest");

        var returned = service.WithRadiusResourceGroup(group);

        Assert.Same(service, returned);
    }

    [Fact]
    public void TwoArg_OnEnvironmentBuilder_ThrowsInvalidOperationException()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("radius");

        // An environment does not deploy against another environment (contracts §1.1). The invalid
        // input is the receiver type, not the environmentGroup argument, so this is an
        // InvalidOperationException rather than an ArgumentException.
        Assert.Throws<InvalidOperationException>(() => env.WithRadiusResourceGroup("platform", "data"));
    }

    [Fact]
    public void NullBuilder_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => RadiusResourceGroupExtensions.WithRadiusResourceGroup<ContainerResource>(null!, "g"));
    }
}

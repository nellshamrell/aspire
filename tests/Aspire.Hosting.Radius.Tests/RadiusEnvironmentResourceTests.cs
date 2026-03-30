#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentResourceTests
{
    [Fact]
    public void Constructor_SetsName()
    {
        var resource = new RadiusEnvironmentResource("my-radius");
        Assert.Equal("my-radius", resource.Name);
    }

    [Fact]
    public void Namespace_DefaultsToDefault()
    {
        var resource = new RadiusEnvironmentResource("radius");
        Assert.Equal("default", resource.Namespace);
    }

    [Fact]
    public void DashboardEnabled_DefaultsToTrue()
    {
        var resource = new RadiusEnvironmentResource("radius");
        Assert.True(resource.DashboardEnabled);
    }

    [Fact]
    public void DashboardEndpoint_DefaultsToNull()
    {
        var resource = new RadiusEnvironmentResource("radius");
        Assert.Null(resource.DashboardEndpoint);
    }

    [Fact]
    public void Namespace_CanBeSet()
    {
        var resource = new RadiusEnvironmentResource("radius")
        {
            Namespace = "staging"
        };
        Assert.Equal("staging", resource.Namespace);
    }

    [Fact]
    public void DashboardEnabled_CanBeDisabled()
    {
        var resource = new RadiusEnvironmentResource("radius")
        {
            DashboardEnabled = false
        };
        Assert.False(resource.DashboardEnabled);
    }

    [Fact]
    public void ImplementsIComputeEnvironmentResource()
    {
        var resource = new RadiusEnvironmentResource("radius");
        Assert.IsAssignableFrom<IComputeEnvironmentResource>(resource);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentResourceTests
{
    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        var resource = new RadiusEnvironmentResource("my-radius");

        Assert.Equal("my-radius", resource.Name);
    }

    [Fact]
    public void DefaultProperties_AreCorrect()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.Equal("default", resource.Namespace);
        Assert.True(resource.DashboardEnabled);
        Assert.Null(resource.DashboardEndpoint);
    }

    [Fact]
    public void Namespace_CanBeSet()
    {
        var resource = new RadiusEnvironmentResource("radius")
        {
            Namespace = "custom-ns"
        };

        Assert.Equal("custom-ns", resource.Namespace);
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
    public void GetHostAddressExpression_ReturnsLowercaseResourceName()
    {
        var resource = new RadiusEnvironmentResource("radius");

        // Create a container resource with an endpoint to test GetHostAddressExpression
        var container = new ApplicationModel.ContainerResource("MyService");
        container.Annotations.Add(new ApplicationModel.EndpointAnnotation(System.Net.Sockets.ProtocolType.Tcp, name: "http", port: 80));
        var endpoint = new ApplicationModel.EndpointReference(container, "http");

        var expression = resource.GetHostAddressExpression(endpoint);

        Assert.NotNull(expression);
        Assert.Equal("myservice", expression.Format);
    }
}

#pragma warning disable ASPIRECOMPUTE002 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests;

public class RadiusEnvironmentResourceTests
{
    [Fact]
    public void Constructor_SetsNameCorrectly()
    {
        var resource = new RadiusEnvironmentResource("test-env");

        Assert.Equal("test-env", resource.Name);
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
            Namespace = "custom-ns"
        };

        Assert.Equal("custom-ns", resource.Namespace);
    }

    [Fact]
    public void ImplementsIComputeEnvironmentResource()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.IsAssignableFrom<IComputeEnvironmentResource>(resource);
    }

    [Fact]
    public void ImplementsIResource()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.IsAssignableFrom<IResource>(resource);
    }

    [Fact]
    public void GetHostAddressExpression_ReturnsKubernetesDns()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var env = builder.AddRadiusEnvironment("radius");
        var container = builder.AddContainer("myservice", "nginx")
            .WithHttpEndpoint(targetPort: 80, name: "http");

        var expression = env.Resource.GetHostAddressExpression(container.GetEndpoint("http"));

        Assert.NotNull(expression);
        Assert.Equal("myservice.svc.cluster.local", expression.Format);
    }

    [Fact]
    public void Constructor_AddsPipelineStepAnnotation()
    {
        var resource = new RadiusEnvironmentResource("radius");

        Assert.Contains(resource.Annotations, a => a.GetType().Name == "PipelineStepAnnotation");
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class MultiEnvironmentTests
{
    [Fact]
    public async Task MultipleRadiusEnvironments_CanCoexist()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env1 = builder.AddRadiusEnvironment("env1").WithNamespace("ns1");
        var env2 = builder.AddRadiusEnvironment("env2").WithNamespace("ns2");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
        Assert.Equal(2, environments.Length);
        Assert.Contains(environments, e => e.Namespace == "ns1");
        Assert.Contains(environments, e => e.Namespace == "ns2");
    }

    [Fact]
    public async Task UntargetedResources_UseFirstEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env1 = builder.AddRadiusEnvironment("env1").WithNamespace("ns1");
        var env2 = builder.AddRadiusEnvironment("env2").WithNamespace("ns2");
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");

        // Untargeted resources should get exactly one annotation targeting the first environment
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.Single(annotations);
        Assert.Equal(env1.Resource, annotations[0].ComputeEnvironment);
    }

    [Fact]
    public async Task DifferentNamespaces_DontCauseNamingCollisions()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("env1").WithNamespace("ns1");
        builder.AddRadiusEnvironment("env2").WithNamespace("ns2");
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify env1 and env2 have different namespaces and unique names
        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
        Assert.Equal("ns1", environments[0].Namespace);
        Assert.Equal("ns2", environments[1].Namespace);

        // Verify resource names are unique — no naming collisions across environments
        var allResourceNames = model.Resources.Select(r => r.Name).ToArray();
        Assert.Equal(allResourceNames.Length, allResourceNames.Distinct().Count());

        // Verify the api container only has one deployment target annotation (assigned to first env)
        var apiResource = model.Resources.First(r => r.Name == "api");
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.Single(annotations);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);
}

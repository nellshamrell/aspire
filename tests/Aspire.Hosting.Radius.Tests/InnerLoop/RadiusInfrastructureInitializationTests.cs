// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class RadiusInfrastructureInitializationTests
{
    [Fact]
    public async Task BeforeStartEvent_AttachesDeploymentTargetAnnotation_ToContainerResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        var container = builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");
        var annotations = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();

        Assert.Single(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotations[0].ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_AttachesAnnotation_ToAllComputeResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddContainer("frontend", "myapp/frontend:latest");
        builder.AddContainer("backend", "myapp/backend:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        foreach (var resource in model.GetComputeResources())
        {
            var annotations = resource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
            Assert.NotEmpty(annotations);
        }
    }

    [Fact]
    public async Task BeforeStartEvent_DoesNotAnnotate_NonComputeResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddContainer("api", "myapp/api:latest");
        builder.AddParameter("my-param");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var paramResource = model.Resources.First(r => r.Name == "my-param");

        // Parameters are not compute resources, so should not have a deployment target annotation
        var annotations = paramResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();
        Assert.Empty(annotations);
    }

    [Fact]
    public async Task BeforeStartEvent_AnnotationPointsToCorrectEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api:latest");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var apiResource = model.Resources.First(r => r.Name == "api");
        var annotation = apiResource.Annotations.OfType<DeploymentTargetAnnotation>().First();

        Assert.Same(env.Resource, annotation.ComputeEnvironment);
    }

    [Fact]
    public async Task BeforeStartEvent_AttachesAnnotation_ToProjectResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment();
        builder.AddProject<TestProjectMetadata>("webapp");

        using var app = builder.Build();
        await ExecuteBeforeStartHooksAsync(app, default);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var projectResource = model.Resources.First(r => r.Name == "webapp");
        var annotations = projectResource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();

        Assert.Single(annotations);
        Assert.IsType<RadiusEnvironmentResource>(annotations[0].ComputeEnvironment);
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "ExecuteBeforeStartHooksAsync")]
    private static extern Task ExecuteBeforeStartHooksAsync(DistributedApplication app, CancellationToken cancellationToken);

    private sealed class TestProjectMetadata : IProjectMetadata
    {
        public string ProjectPath => "testproject";
        public LaunchSettings LaunchSettings { get; } = new();
    }
}

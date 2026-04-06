// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class GracefulDegradationTests
{
    [Fact]
    public async Task RunMode_CompletesWithoutErrors_NoKubernetes()
    {
        // In run mode, RadiusInfrastructure should not attach annotations
        // and should not require Kubernetes or rad CLI
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        // Should not throw — graceful in run mode
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // In run mode, the Radius environment is NOT added to the model
        // (CreateResourceBuilder is used instead of AddResource)
        var radiusEnvs = model.Resources.OfType<RadiusEnvironmentResource>().ToList();
        Assert.Empty(radiusEnvs);
    }

    [Fact]
    public async Task RunMode_NoAnnotationsAttached()
    {
        // In run mode, BeforeStartEvent handler returns early
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var webfrontend = model.Resources.FirstOrDefault(r => r.Name == "webfrontend");
        if (webfrontend is not null)
        {
            // No deployment target annotations should exist in run mode
            Assert.Null(webfrontend.GetDeploymentTargetAnnotation());
        }
    }

    [Fact]
    public async Task PublishMode_AnnotationsAttached_WithoutKubernetes()
    {
        // In publish mode, annotations are attached even without a running Kubernetes cluster.
        // This is visualization-only mode — no actual provisioning occurs at this stage.
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var webfrontend = model.Resources.First(r => r.Name == "webfrontend");
        var annotation = webfrontend.GetDeploymentTargetAnnotation();

        // Annotations should be attached even without Kubernetes/Radius availability
        Assert.NotNull(annotation);
        Assert.IsType<RadiusEnvironmentResource>(annotation.ComputeEnvironment);
    }

    [Fact]
    public async Task PublishMode_WithoutRadEnv_NoAnnotations()
    {
        // When no Radius environment is registered and no PublishAsRadiusResource
        // is used, the infrastructure should be a no-op
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // Register the infrastructure via AddRadiusInfrastructureCore but NOT an environment
        // (This simulates the case where AddRadiusEnvironment was called in run mode but
        // the environment wasn't added to the model)
        builder.AddRadiusInfrastructureCore();
        builder.AddContainer("webfrontend", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var webfrontend = model.Resources.First(r => r.Name == "webfrontend");
        Assert.Null(webfrontend.GetDeploymentTargetAnnotation());
    }

    [Fact]
    public void RadiusEnvironment_CanBeCreated_WithoutExternalDependencies()
    {
        // Creating a Radius environment should never require external
        // services (Kubernetes, rad CLI, network access)
        var resource = new RadiusEnvironmentResource("test");

        Assert.Equal("test", resource.Name);
        Assert.Equal("default", resource.Namespace);
    }
}

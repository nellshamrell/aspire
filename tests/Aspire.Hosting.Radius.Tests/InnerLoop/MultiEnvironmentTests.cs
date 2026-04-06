// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.InnerLoop;

public class MultiEnvironmentTests
{
    [Fact]
    public async Task MultipleRadiusEnvironments_CanCoexist()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToList();
        Assert.Equal(2, environments.Count);
        Assert.Contains(environments, e => e.Name == "radius-dev" && e.Namespace == "dev");
        Assert.Contains(environments, e => e.Name == "radius-staging" && e.Namespace == "staging");
    }

    [Fact]
    public async Task Resources_TargetSpecificEnvironment_ViaWithComputeEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var devEnv = builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        var stagingEnv = builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        builder.AddContainer("dev-api", "nginx")
            .WithComputeEnvironment(devEnv);
        builder.AddContainer("staging-api", "nginx")
            .WithComputeEnvironment(stagingEnv);

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var devApi = model.Resources.First(r => r.Name == "dev-api");
        var stagingApi = model.Resources.First(r => r.Name == "staging-api");

        // dev-api should target radius-dev only
        var devAnnotation = devApi.GetDeploymentTargetAnnotation(devEnv.Resource);
        Assert.NotNull(devAnnotation);
        Assert.Same(devEnv.Resource, devAnnotation.ComputeEnvironment);

        // staging-api should target radius-staging only
        var stagingAnnotation = stagingApi.GetDeploymentTargetAnnotation(stagingEnv.Resource);
        Assert.NotNull(stagingAnnotation);
        Assert.Same(stagingEnv.Resource, stagingAnnotation.ComputeEnvironment);

        // Cross-environment targeting should NOT exist
        Assert.Null(devApi.GetDeploymentTargetAnnotation(stagingEnv.Resource));
        Assert.Null(stagingApi.GetDeploymentTargetAnnotation(devEnv.Resource));
    }

    [Fact]
    public async Task Resources_WithNoExplicitTarget_GetAnnotationsFromAllEnvironments()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        builder.AddContainer("shared-api", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var sharedApi = model.Resources.First(r => r.Name == "shared-api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(sharedApi);

        // Untargeted resources receive annotations from ALL environments
        Assert.Equal(2, annotations.Count);
    }

    [Fact]
    public async Task DifferentNamespaces_DontCauseCollisions()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius-dev")
            .WithRadiusNamespace("dev");
        builder.AddRadiusEnvironment("radius-staging")
            .WithRadiusNamespace("staging");

        builder.AddContainer("api", "nginx");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);

        var api = model.Resources.First(r => r.Name == "api");
        var annotations = RadiusTestHelper.GetDeploymentTargetAnnotations(api);

        // Each annotation should point to a different compute environment
        var environments = annotations.Select(a => a.ComputeEnvironment).OfType<RadiusEnvironmentResource>().ToList();
        Assert.Equal(2, environments.Count);
        Assert.NotEqual(environments[0].Namespace, environments[1].Namespace);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepToRadDeployCompatibilityTests
{
    [Fact]
    public async Task GeneratedBicep_HasExtensionRadiusDirective()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.StartsWith("extension radius", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_HasEnvironmentBeforeApplication()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        var envIndex = bicep.IndexOf("Applications.Core/environments", StringComparison.Ordinal);
        var appIndex = bicep.IndexOf("Applications.Core/applications", StringComparison.Ordinal);

        Assert.True(envIndex >= 0, "Environment resource not found in Bicep");
        Assert.True(appIndex >= 0, "Application resource not found in Bicep");
        Assert.True(envIndex < appIndex, "Environment must appear before application in Bicep");
    }

    [Fact]
    public async Task GeneratedBicep_ContainsValidResourceReferences()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains(".id", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_UsesCorrectApiVersions()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("2023-10-01-preview", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_HasComputeKindKubernetes()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("kubernetes", bicep);
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory.Create(b => { }).CreateLogger("RadiusTests");
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-005, SC-005, US3 — a group whose application deploys against an environment owned by
/// another group emits <c>properties.environment</c> as that environment's fully-qualified UCP
/// ID (and does not re-declare the environment), while an in-group group keeps the bare
/// <c>.id</c> reference.
/// </summary>
public class CrossGroupEnvironmentBicepTests
{
    [Fact]
    public void CrossGroupEnvironment_EmitsFullUcpId_AsPropertiesEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // platform owns the environment (and a resource so the environment is materialized).
        builder.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
        builder.AddRedis("platformcache").WithRadiusResourceGroup("platform");

        // orders owns no environment; its api deploys against platform's environment (cross-group).
        builder.AddContainer("api", "myapp/api", "latest")
            .WithRadiusResourceGroup("orders", environmentGroup: "platform");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var bicepByGroup = RadiusBicepPublishingContext.GenerateGroupedBicep(model);
        var ordersBicep = bicepByGroup["orders"];

        // The application deploys against platform's environment via its full UCP ID (FR-005).
        Assert.Contains(
            "environment: '/planes/radius/local/resourceGroups/platform/providers/Applications.Core/environments/env-platform'",
            ordersBicep);

        // The cross-group group must NOT re-declare the environment; it lives in platform's bicep.
        Assert.DoesNotContain("Radius.Core/environments", ordersBicep);
    }

    [Fact]
    public void CrossGroupEnvironment_WithLegacyResource_EmitsFullUcpId_WithoutLocalLegacyEnvironment()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        // platform owns the environment (and a resource so the environment is materialized).
        builder.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
        builder.AddRedis("platformcache").WithRadiusResourceGroup("platform");

        // orders owns no environment; its legacy Redis resource deploys against platform's environment.
        builder.AddRedis("orderscache").WithRadiusResourceGroup("orders", environmentGroup: "platform");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var ordersBicep = RadiusBicepPublishingContext.GenerateGroupedBicep(model)["orders"];
        var expectedEnvironment = "environment: '/planes/radius/local/resourceGroups/platform/providers/Applications.Core/environments/env-platform'";

        Assert.Contains("Applications.Core/applications@", ordersBicep);
        var legacyApplicationIndex = ordersBicep.IndexOf("resource app 'Applications.Core/applications@", StringComparison.Ordinal);
        Assert.True(legacyApplicationIndex >= 0, $"Expected a legacy application. Bicep:{Environment.NewLine}{ordersBicep}");
        var nextResourceIndex = ordersBicep.IndexOf("\nresource ", legacyApplicationIndex + 1, StringComparison.Ordinal);
        var legacyApplicationEnvironmentIndex = ordersBicep.IndexOf(expectedEnvironment, legacyApplicationIndex, StringComparison.Ordinal);
        Assert.True(
            legacyApplicationEnvironmentIndex > legacyApplicationIndex &&
            legacyApplicationEnvironmentIndex < nextResourceIndex,
            $"Expected the legacy application to reference the cross-group environment. Bicep:{Environment.NewLine}{ordersBicep}");
        Assert.DoesNotContain("Applications.Core/environments@", ordersBicep);
    }

    [Fact]
    public void InGroupEnvironment_KeepsBareReference()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
        builder.AddRedis("platformcache").WithRadiusResourceGroup("platform");

        builder.AddContainer("api", "myapp/api", "latest")
            .WithRadiusResourceGroup("orders", environmentGroup: "platform");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var platformBicep = RadiusBicepPublishingContext.GenerateGroupedBicep(model)["platform"];

        // platform owns its environment, so its application references it by the bare .id.
        Assert.Contains("environment: env_platform.id", platformBicep);

        // It must NOT be rewritten as a fully-qualified UCP literal for the owning group.
        Assert.DoesNotContain(
            "/planes/radius/local/resourceGroups/platform/providers/Applications.Core/environments",
            platformBicep);
    }
}

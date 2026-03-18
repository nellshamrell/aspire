// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ManualProvisioningValidationTests
{
    [Fact]
    public async Task ManualProvisioning_WithHostAndPort_GeneratesBicepWithManualConfig()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
                cfg.Host = "postgres.example.com";
                cfg.Port = 5432;
            });

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("host: 'postgres.example.com'", bicep);
        Assert.Contains("port: 5432", bicep);
    }

    [Fact]
    public async Task ManualProvisioning_WithoutHost_ThrowsValidationError()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
                // Host intentionally missing
                cfg.Port = 5432;
            });

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() => context.GenerateBicep(model, environment));
        Assert.Contains("Host", ex.Message);
        Assert.Contains("Manual", ex.Message);
    }

    [Fact]
    public async Task ManualProvisioning_WithoutPort_ThrowsValidationError()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
                cfg.Host = "postgres.example.com";
                // Port intentionally missing
            });

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var ex = Assert.Throws<InvalidOperationException>(() => context.GenerateBicep(model, environment));
        Assert.Contains("Port", ex.Message);
    }

    [Fact]
    public async Task PostgresEndToEnd_ManualProvisioningWorks()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        var postgres = builder.AddPostgres("postgres")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
                cfg.Host = "db.internal.example.com";
                cfg.Port = 5432;
            });
        var db = postgres.AddDatabase("appdb");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Should contain the postgres resource with manual provisioning
        Assert.Contains("postgres", bicep);
        Assert.Contains("resourceProvisioning: 'manual'", bicep);
    }
}

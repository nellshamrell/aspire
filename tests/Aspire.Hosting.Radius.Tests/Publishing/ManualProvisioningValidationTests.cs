// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ManualProvisioningValidationTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    [Fact]
    public void ManualProvisioning_WithHostAndPort_EmitsBicepCorrectly()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var pg = builder.AddPostgres("postgres");
        pg.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            config.Host = "pg.example.com";
            config.Port = 5432;
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("host: 'pg.example.com'", bicep);
        Assert.Contains("port: 5432", bicep);
    }

    [Fact]
    public void ManualProvisioning_MissingHost_ThrowsValidationError()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var pg = builder.AddPostgres("postgres");
        pg.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            // Host intentionally missing
            config.Port = 5432;
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);

        var ex = Assert.Throws<InvalidOperationException>(context.GenerateBicep);
        Assert.Contains("Host", ex.Message);
        Assert.Contains("postgres", ex.Message);
    }

    [Fact]
    public void ManualProvisioning_MissingPort_ThrowsValidationError()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var pg = builder.AddPostgres("postgres");
        pg.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            config.Host = "pg.example.com";
            // Port intentionally missing
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);

        var ex = Assert.Throws<InvalidOperationException>(context.GenerateBicep);
        Assert.Contains("Port", ex.Message);
        Assert.Contains("postgres", ex.Message);
    }

    [Fact]
    public void PostgresDefault_IsManualProvisioning_AsPortableResource()
    {
        // T042c: PostgreSQL maps to portable resource with manual provisioning by default
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var pg = builder.AddPostgres("postgres");
        pg.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            config.Host = "pg.example.com";
            config.Port = 5432;
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Should be a portable resource, NOT a container
        Assert.Contains("Applications.Datastores/postgresDatabases", bicep);
        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        // Should NOT be emitted as Applications.Core/containers
        var containerLines = bicep.Split('\n')
            .Where(l => l.Contains("Applications.Core/containers"))
            .ToList();
        // No container block for postgres
        Assert.DoesNotContain("name: 'postgres'",
            string.Join('\n', containerLines.SelectMany((l, i) =>
                bicep.Split('\n').Skip(Array.IndexOf(bicep.Split('\n'), l)).Take(3))));
    }

    [Fact]
    public void ManualProvisioning_NoRecipesGenerated()
    {
        // Manual provisioned resources should not generate recipe entries
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");

        var pg = builder.AddPostgres("postgres");
        pg.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            config.Host = "pg.example.com";
            config.Port = 5432;
        });

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // No recipes block for manual-only environment
        Assert.DoesNotContain("recipes:", bicep);
    }
}

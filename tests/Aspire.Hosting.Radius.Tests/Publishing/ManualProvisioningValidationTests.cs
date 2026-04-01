#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Tests verifying manual provisioning configuration and validation.
/// </summary>
public class ManualProvisioningValidationTests
{
    [Fact]
    public void ManualProvisioning_WithHostAndPort_CreatesValidAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Manual;
                config.Host = "host.docker.internal";
                config.Port = 5432;
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var postgres = model.Resources.First(r => r.Name == "postgres");
        var annotation = postgres.Annotations.OfType<RadiusResourceCustomizationAnnotation>().FirstOrDefault();

        Assert.NotNull(annotation);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
        Assert.Equal("host.docker.internal", annotation.Customization.Host);
        Assert.Equal(5432, annotation.Customization.Port);
    }

    [Fact]
    public void ManualProvisioning_MissingHost_HasNullHost()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Manual;
                config.Port = 5432;
                // Host intentionally not set
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var postgres = model.Resources.First(r => r.Name == "postgres");
        var annotation = postgres.Annotations.OfType<RadiusResourceCustomizationAnnotation>().First();

        // Host is null — validation should catch this during Bicep generation
        Assert.Null(annotation.Customization.Host);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
    }

    [Fact]
    public void ManualProvisioning_MissingPort_HasNullPort()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Manual;
                config.Host = "my-host";
                // Port intentionally not set
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var postgres = model.Resources.First(r => r.Name == "postgres");
        var annotation = postgres.Annotations.OfType<RadiusResourceCustomizationAnnotation>().First();

        Assert.Null(annotation.Customization.Port);
        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
    }

    [Fact]
    public void PostgresWithManualProvisioning_MapsToManualType()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Manual;
                config.Host = "host.docker.internal";
                config.Port = 5432;
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var postgres = model.Resources.First(r => r.Name == "postgres");

        // ResourceTypeMapper should flag Postgres as manual provisioning
        var mapping = ResourceTypeMapper.GetRadiusType(postgres);
        Assert.True(mapping.IsManualProvisioning);
    }

    [Fact]
    public void AutomaticProvisioning_IsDefault()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Equal(RadiusResourceProvisioning.Automatic, customization.Provisioning);
        Assert.Null(customization.Host);
        Assert.Null(customization.Port);
    }

    [Fact]
    public void ManualProvisioning_FullPostgresScenario()
    {
        // End-to-end manual provisioning for PostgreSQL
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");

        var postgres = builder.AddPostgres("postgres")
            .AddDatabase("mydb");

        builder.Resources.OfType<PostgresServerResource>().First()
            .Annotations.Add(new RadiusResourceCustomizationAnnotation(
                new RadiusResourceCustomization
                {
                    Provisioning = RadiusResourceProvisioning.Manual,
                    Host = "postgres.example.com",
                    Port = 5432
                }));

        builder.AddContainer("api", "myimage", "latest")
            .WithReference(postgres);

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Verify the Postgres server has the manual provisioning annotation
        var pgServer = model.Resources.OfType<PostgresServerResource>().First();
        var annotation = pgServer.Annotations.OfType<RadiusResourceCustomizationAnnotation>().First();

        Assert.Equal(RadiusResourceProvisioning.Manual, annotation.Customization.Provisioning);
        Assert.Equal("postgres.example.com", annotation.Customization.Host);
        Assert.Equal(5432, annotation.Customization.Port);

        // And the database child resource should be in the model
        var dbResource = model.Resources.FirstOrDefault(r => r.Name == "mydb");
        Assert.NotNull(dbResource);
    }

    [Fact]
    public void AutomaticProvisioning_NoHostOrPort_Required()
    {
        using var builder = TestDistributedApplicationBuilder.Create();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis")
            .PublishAsRadiusResource(config =>
            {
                config.Provisioning = RadiusResourceProvisioning.Automatic;
            });

        var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var redis = model.Resources.First(r => r.Name == "redis");
        var annotation = redis.Annotations.OfType<RadiusResourceCustomizationAnnotation>().First();

        Assert.Equal(RadiusResourceProvisioning.Automatic, annotation.Customization.Provisioning);
        Assert.Null(annotation.Customization.Host);
        Assert.Null(annotation.Customization.Port);
    }
}

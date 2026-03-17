// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests;

/// <summary>
/// Test utilities for Radius hosting integration tests.
/// </summary>
internal static class RadiusTestHelper
{
    /// <summary>
    /// Creates a simple test app host with a Radius environment and a single container.
    /// </summary>
    public static IDistributedApplicationTestingBuilder CreateSimpleRadiusAppHost(params string[] args)
    {
        var builder = TestDistributedApplicationBuilder.Create(args);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        return builder;
    }

    /// <summary>
    /// Creates a test app host with multiple resource types for comprehensive testing.
    /// </summary>
    public static IDistributedApplicationTestingBuilder CreateMultiResourceAppHost(params string[] args)
    {
        var builder = TestDistributedApplicationBuilder.Create(args);

        builder.AddRadiusEnvironment("radius");

        // Container resources
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithHttpEndpoint(port: 8080, name: "http");
        builder.AddContainer("worker", "mcr.microsoft.com/dotnet/runtime:8.0");

        // Redis
        builder.AddRedis("cache");

        // SQL Server + database
        builder.AddSqlServer("sqlserver")
            .AddDatabase("appdb");

        // RabbitMQ
        builder.AddRabbitMQ("messaging");

        // MongoDB
        builder.AddMongoDB("mongo")
            .AddDatabase("nosqldb");

        // PostgreSQL (manual provisioning)
        builder.AddPostgres("postgres")
            .AddDatabase("pgdb");

        return builder;
    }

    /// <summary>
    /// Builds the app and returns the <see cref="DistributedApplicationModel"/>.
    /// </summary>
    public static DistributedApplicationModel BuildAndGetModel(IDistributedApplicationTestingBuilder builder)
    {
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    /// <summary>
    /// Gets the <see cref="RadiusEnvironmentResource"/> from the model, or throws if not found.
    /// </summary>
    public static RadiusEnvironmentResource GetRadiusEnvironment(DistributedApplicationModel model)
    {
        var env = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        if (env is null)
        {
            throw new InvalidOperationException("No RadiusEnvironmentResource found in the model.");
        }
        return env;
    }

    /// <summary>
    /// Gets all <see cref="DeploymentTargetAnnotation"/> instances from a resource.
    /// </summary>
    public static IEnumerable<DeploymentTargetAnnotation> GetDeploymentTargetAnnotations(IResource resource)
    {
        return resource.Annotations.OfType<DeploymentTargetAnnotation>();
    }
}

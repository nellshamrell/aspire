// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// App host containing multiple resource types covering all mapped types from ResourceTypeMapper.
/// Includes: Redis, SQL Server + database, RabbitMQ, MongoDB, PostgreSQL (manual provisioning),
/// two containers, and one project.
/// </summary>
/// <remarks>
/// Dapr resources are excluded because the Dapr hosting package does not exist in this codebase.
/// </remarks>
internal static class MultiResourceAppHost
{
    public static IDistributedApplicationBuilder CreateBuilder(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish)
    {
        var builder = TestDistributedApplicationBuilder.Create(operation);

        builder.AddRadiusEnvironment("radius");

        // Redis
        builder.AddRedis("cache");

        // SQL Server + database
        var sqlServer = builder.AddSqlServer("sqlserver");
        sqlServer.AddDatabase("sqldb");

        // RabbitMQ
        builder.AddRabbitMQ("messaging");

        // MongoDB
        var mongo = builder.AddMongoDB("mongo");
        mongo.AddDatabase("mongodb");

        // PostgreSQL with manual provisioning
        builder.AddPostgres("postgres")
            .PublishAsRadiusResource(cfg =>
            {
                cfg.Provisioning = RadiusResourceProvisioning.Manual;
                cfg.Host = "postgres.example.com";
                cfg.Port = 5432;
            });

        // Two container resources
        builder.AddContainer("webfrontend", "nginx");
        builder.AddContainer("worker", "myregistry/worker:latest");

        // Project resource (using inline IProjectMetadata stub)
        builder.AddProject<TestProject>("api");

        return builder;
    }

    private sealed class TestProject : IProjectMetadata
    {
        public string ProjectPath => "TestProject";

        public LaunchSettings? LaunchSettings { get; set; }
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// App host with multiple resource types covering all mapped types from ResourceTypeMapper.
/// Contains: Redis, SQL Server + database, RabbitMQ, MongoDB, PostgreSQL (manual provisioning),
/// two container instances, and one project-like container.
/// Note: Dapr resources are excluded — not present in this Aspire fork.
/// </summary>
public static class MultiResourceAppHost
{
    public static IDistributedApplicationBuilder Configure(IDistributedApplicationBuilder? builder = null)
    {
        builder ??= DistributedApplication.CreateBuilder();

        var env = builder.AddRadiusEnvironment("radius");

        // Portable resources
        var redis = builder.AddRedis("redis");
        var sqlserver = builder.AddSqlServer("sqlserver");
        var sqldb = sqlserver.AddDatabase("appdb");
        var rabbitmq = builder.AddRabbitMQ("rabbitmq");
        var mongodb = builder.AddMongoDB("mongodb");
        var mongoDb = mongodb.AddDatabase("catalogdb");

        // PostgreSQL with manual provisioning
        var postgres = builder.AddPostgres("postgres");
        var postgresDb = postgres.AddDatabase("inventorydb");
        postgres.PublishAsRadiusResource(config =>
        {
            config.Provisioning = RadiusResourceProvisioning.Manual;
            config.Host = "pg.example.com";
            config.Port = 5432;
        });

        // Container workloads
        builder.AddContainer("api", "myregistry.azurecr.io/api:latest")
            .WithReference(redis)
            .WithReference(sqldb)
            .WithReference(rabbitmq);

        builder.AddContainer("worker", "myregistry.azurecr.io/worker:latest")
            .WithReference(mongoDb)
            .WithReference(postgresDb);

        return builder;
    }
}

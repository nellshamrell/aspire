#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// App host with multiple resource types covering all mapped types from ResourceTypeMapper.
/// Includes: Redis, SQL Server + database, RabbitMQ, MongoDB, PostgreSQL (manual provisioning),
/// two container instances, and one project-style container.
/// Note: Dapr resources are excluded because no Aspire.Hosting.Dapr package exists in this repo.
/// </summary>
public static class MultiResourceAppHost
{
    public static IDistributedApplicationTestingBuilder Create(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Run)
    {
        var builder = TestDistributedApplicationBuilder.Create(operation);

        _ = builder.AddRadiusEnvironment("radius");

        // Portable resources
        var redis = builder.AddRedis("redis");
        var sqlserver = builder.AddSqlServer("sqlserver").AddDatabase("appdb");
        var rabbitmq = builder.AddRabbitMQ("rabbitmq");
        var mongo = builder.AddMongoDB("mongo").AddDatabase("mongodb");
        _ = builder.AddPostgres("postgres")
            .AddDatabase("pgdb");

        // Mark PostgreSQL for manual provisioning (no native Radius type)
        builder.Resources.OfType<PostgresServerResource>().First()
            .Annotations.Add(new RadiusResourceCustomizationAnnotation(
                new RadiusResourceCustomization
                {
                    Provisioning = RadiusResourceProvisioning.Manual,
                    Host = "host.docker.internal",
                    Port = 5432
                }));

        // Container workloads
        _ = builder.AddContainer("api", "myregistry.azurecr.io/api", "latest")
            .WithReference(redis)
            .WithReference(sqlserver);

        _ = builder.AddContainer("worker", "myregistry.azurecr.io/worker", "latest")
            .WithReference(rabbitmq)
            .WithReference(mongo);

        return builder;
    }
}

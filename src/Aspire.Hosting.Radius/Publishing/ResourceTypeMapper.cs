// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Maps Aspire resource types to Radius portable resource types and default recipe template paths.
/// </summary>
internal static class ResourceTypeMapper
{
    internal record ResourceMapping(string RadiusType, string ApiVersion, string DefaultTemplatePath);

    private static readonly Dictionary<string, ResourceMapping> s_typeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RedisResource"] = new("Applications.Datastores/redisCaches", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest"),
        ["SqlServerServerResource"] = new("Applications.Datastores/sqlDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest"),
        ["PostgresServerResource"] = new("Applications.Datastores/postgresDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/postgresdatabases:latest"),
        ["MongoDBServerResource"] = new("Applications.Datastores/mongoDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest"),
        ["RabbitMQServerResource"] = new("Applications.Messaging/rabbitMQQueues", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest"),
        ["DaprStateStoreResource"] = new("Applications.Dapr/stateStores", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/statestores:latest"),
        ["DaprPubSubResource"] = new("Applications.Dapr/pubSubBrokers", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/pubsubbrokers:latest"),
    };

    // Fallback for unmapped types
    private static readonly ResourceMapping s_containerFallback = new("Applications.Core/containers", "2023-10-01-preview", "");

    /// <summary>
    /// Gets the Radius type mapping for the given Aspire resource.
    /// Returns the container fallback for unmapped types.
    /// </summary>
    public static ResourceMapping GetMapping(IResource resource)
    {
        var typeName = resource.GetType().Name;

        if (s_typeMappings.TryGetValue(typeName, out var mapping))
        {
            return mapping;
        }

        // Check base types (e.g., PostgresDatabaseResource → check parent)
        var type = resource.GetType();
        while (type.BaseType is not null)
        {
            type = type.BaseType;
            if (s_typeMappings.TryGetValue(type.Name, out mapping))
            {
                return mapping;
            }
        }

        return s_containerFallback;
    }

    /// <summary>
    /// Returns true if the resource maps to a Radius portable resource type (not a container).
    /// </summary>
    public static bool IsPortableResource(IResource resource)
    {
        var mapping = GetMapping(resource);
        return mapping.RadiusType != s_containerFallback.RadiusType;
    }

    /// <summary>
    /// Returns true if the resource is a compute resource (container or project).
    /// </summary>
    public static bool IsComputeResource(IResource resource)
    {
        return resource.IsContainer() || resource is ProjectResource;
    }
}

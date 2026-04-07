// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Maps Aspire resource types to Radius portable resource types.
/// </summary>
internal static class ResourceTypeMapper
{
    // Use type names instead of direct type references to avoid taking dependencies
    // on all hosting packages (Redis, SQL, MongoDB, etc.)
    private static readonly Dictionary<string, ResourceMapping> s_mappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Datastores
        ["RedisResource"] = new("Applications.Datastores/redisCaches", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest"),
        ["SqlServerServerResource"] = new("Applications.Datastores/sqlDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest"),
        ["SqlServerDatabaseResource"] = new("Applications.Datastores/sqlDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest"),
        ["MongoDBServerResource"] = new("Applications.Datastores/mongoDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest"),
        ["MongoDBDatabaseResource"] = new("Applications.Datastores/mongoDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest"),
        ["PostgresServerResource"] = new("Applications.Datastores/postgresDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/postgresdatabases:latest"),
        ["PostgresDatabaseResource"] = new("Applications.Datastores/postgresDatabases", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/postgresdatabases:latest"),

        // Messaging
        ["RabbitMQServerResource"] = new("Applications.Messaging/rabbitMQQueues", "2023-10-01-preview", "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest"),
    };

    /// <summary>
    /// Gets the Radius resource mapping for a given Aspire resource.
    /// </summary>
    /// <param name="resource">The Aspire resource to map.</param>
    /// <returns>The corresponding <see cref="ResourceMapping"/>.</returns>
    public static ResourceMapping GetRadiusType(IResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var typeName = resource.GetType().Name;

        // Check for exact type name match
        if (s_mappings.TryGetValue(typeName, out var mapping))
        {
            return mapping;
        }

        // Walk the type hierarchy for base type matches
        var currentType = resource.GetType().BaseType;
        while (currentType is not null)
        {
            if (s_mappings.TryGetValue(currentType.Name, out var baseMapping))
            {
                return baseMapping;
            }
            currentType = currentType.BaseType;
        }

        // Containers and projects are workloads, not portable resources
        if (resource is ContainerResource or ProjectResource)
        {
            return new ResourceMapping("Applications.Core/containers", "2023-10-01-preview", DefaultRecipe: null);
        }

        // Fallback: treat unknown resource types as containers with a warning
        return new ResourceMapping("Applications.Core/containers", "2023-10-01-preview", DefaultRecipe: null);
    }

    /// <summary>
    /// Determines whether the resource maps to a Radius portable resource type (vs. a workload container).
    /// </summary>
    public static bool IsPortableResource(IResource resource)
    {
        var mapping = GetRadiusType(resource);
        return mapping.Type != "Applications.Core/containers";
    }
}

/// <summary>
/// Represents the mapping from an Aspire resource type to a Radius resource type.
/// </summary>
/// <param name="Type">The Radius resource type string (e.g., <c>Applications.Datastores/redisCaches</c>).</param>
/// <param name="ApiVersion">The Radius API version for the resource type.</param>
/// <param name="DefaultRecipe">The default recipe template path, or <c>null</c> for workload types.</param>
internal readonly record struct ResourceMapping(string Type, string ApiVersion, string? DefaultRecipe);

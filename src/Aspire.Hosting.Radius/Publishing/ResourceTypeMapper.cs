// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Maps Aspire resource types to Radius portable resource types and default recipe templates.
/// </summary>
internal static class ResourceTypeMapper
{
    /// <summary>
    /// The Radius API version used for all resource types.
    /// </summary>
    internal const string RadiusApiVersion = "2023-10-01-preview";

    /// <summary>
    /// Mapping from Aspire resource type names to Radius resource mappings.
    /// Uses type names to avoid hard references to hosting packages (Redis, SQL, etc.).
    /// </summary>
    private static readonly Dictionary<string, ResourceMapping> s_mappingsByTypeName = new(StringComparer.Ordinal)
    {
        ["RedisResource"] = new("Applications.Datastores/redisCaches", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest"),
        ["SqlServerServerResource"] = new("Applications.Datastores/sqlDatabases", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest"),
        ["SqlServerDatabaseResource"] = new("Applications.Datastores/sqlDatabases", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest"),
        ["MongoDBServerResource"] = new("Applications.Datastores/mongoDatabases", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest"),
        ["MongoDBDatabaseResource"] = new("Applications.Datastores/mongoDatabases", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest"),
        ["RabbitMQServerResource"] = new("Applications.Messaging/rabbitMQQueues", RadiusApiVersion, "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest"),
        ["PostgresServerResource"] = new("Applications.Datastores/postgresDatabases", RadiusApiVersion, null, Models.RadiusResourceProvisioning.Manual),
        ["PostgresDatabaseResource"] = new("Applications.Datastores/postgresDatabases", RadiusApiVersion, null, Models.RadiusResourceProvisioning.Manual),
    };

    /// <summary>
    /// Container type mapping for container and project resources.
    /// </summary>
    internal static readonly ResourceMapping s_containerMapping = new("Applications.Core/containers", RadiusApiVersion, defaultRecipe: null);

    /// <summary>
    /// Gets the Radius resource mapping for the given Aspire resource.
    /// Returns a container mapping for unmapped types and logs a warning.
    /// </summary>
    public static ResourceMapping GetRadiusMapping(IResource resource, ILogger? logger = null)
    {
        // Try exact type name match first — this catches portable resources
        // (Redis, SqlServer, etc.) that extend ContainerResource but should
        // be mapped to their specific Radius portable resource types.
        var typeName = resource.GetType().Name;
        if (s_mappingsByTypeName.TryGetValue(typeName, out var mapping))
        {
            return mapping;
        }

        // Walk base types for subclass support
        var baseType = resource.GetType().BaseType;
        while (baseType is not null)
        {
            if (s_mappingsByTypeName.TryGetValue(baseType.Name, out var baseMapping))
            {
                return baseMapping;
            }
            baseType = baseType.BaseType;
        }

        // Container and project resources are workload containers
        if (resource is ContainerResource || resource is ProjectResource)
        {
            return s_containerMapping;
        }

        // Fallback: treat as container with warning
        logger?.LogWarning("Unmapped resource type '{ResourceType}' for resource '{ResourceName}'. Falling back to Applications.Core/containers.", resource.GetType().FullName, resource.Name);
        return s_containerMapping;
    }

    /// <summary>
    /// Returns true if the mapping represents a portable resource (not a container workload).
    /// </summary>
    public static bool IsPortableResource(ResourceMapping mapping)
    {
        return mapping.Type != "Applications.Core/containers";
    }

    /// <summary>
    /// The container type mapping. Exposed for test access.
    /// </summary>
    internal static ResourceMapping ContainerMapping => s_containerMapping;
}

/// <summary>
/// Describes the Radius type mapping for an Aspire resource.
/// </summary>
public readonly record struct ResourceMapping(
    string Type,
    string ApiVersion,
    string? DefaultRecipe,
    Models.RadiusResourceProvisioning DefaultProvisioning = Models.RadiusResourceProvisioning.Automatic)
{
    /// <summary>
    /// Initializes a new <see cref="ResourceMapping"/> with automatic provisioning.
    /// </summary>
    public ResourceMapping(string type, string apiVersion, string? defaultRecipe)
        : this(type, apiVersion, defaultRecipe, Models.RadiusResourceProvisioning.Automatic)
    {
    }
}

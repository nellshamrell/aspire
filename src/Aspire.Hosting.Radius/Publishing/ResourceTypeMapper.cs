// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Maps Aspire resource types to Radius portable resource types.
/// Uses type-name-based matching to avoid compile-time dependencies on every hosting package.
/// </summary>
internal static class ResourceTypeMapper
{
    /// <summary>
    /// The Radius portable resource type used for PostgreSQL databases.
    /// </summary>
    public const string PostgresPortableResourceType = "Applications.Datastores/postgresDatabases";

    /// <summary>
    /// The Radius API version used for all resource types.
    /// </summary>
    public const string RadiusApiVersion = "2023-10-01";

    // Type name → Radius mapping (avoids compile-time dependency on each hosting package)
    private static readonly Dictionary<string, ResourceMapping> s_mappingsByName = new(StringComparer.Ordinal)
    {
        ["RedisResource"] = new ResourceMapping
        {
            Type = "Applications.Datastores/redisCaches",
            ApiVersion = RadiusApiVersion,
            DefaultRecipe = "default"
        },
        ["SqlServerServerResource"] = new ResourceMapping
        {
            Type = "Applications.Datastores/sqlDatabases",
            ApiVersion = RadiusApiVersion,
            DefaultRecipe = "default"
        },
        ["MongoDBServerResource"] = new ResourceMapping
        {
            Type = "Applications.Datastores/mongoDatabases",
            ApiVersion = RadiusApiVersion,
            DefaultRecipe = "default"
        },
        ["RabbitMQServerResource"] = new ResourceMapping
        {
            Type = "Applications.Messaging/rabbitMQQueues",
            ApiVersion = RadiusApiVersion,
            DefaultRecipe = "default"
        },
    };

    // Type names that require manual provisioning (no native Radius portable type)
    private static readonly HashSet<string> s_manualProvisioningTypeNames = new(StringComparer.Ordinal)
    {
        "PostgresServerResource"
    };

    /// <summary>
    /// Gets the Radius resource mapping for the given Aspire resource.
    /// </summary>
    /// <param name="resource">The Aspire resource.</param>
    /// <param name="logger">Optional logger for warnings about unmapped types.</param>
    /// <returns>The resource mapping.</returns>
    public static ResourceMapping GetRadiusType(IResource resource, ILogger? logger = null)
    {
        var resourceType = resource.GetType();
        var typeName = resourceType.Name;

        // Check for manual provisioning types (by name or base type name)
        if (IsManualProvisioningType(resourceType))
        {
            return new ResourceMapping
            {
                Type = PostgresPortableResourceType,
                ApiVersion = RadiusApiVersion,
                IsManualProvisioning = true
            };
        }

        // Check exact type name match
        if (s_mappingsByName.TryGetValue(typeName, out var mapping))
        {
            return mapping;
        }

        // Check base type hierarchy names
        var baseType = resourceType.BaseType;
        while (baseType is not null)
        {
            if (s_mappingsByName.TryGetValue(baseType.Name, out var baseMapping))
            {
                return baseMapping;
            }
            baseType = baseType.BaseType;
        }

        // ContainerResource and ProjectResource → Applications.Core/containers
        if (resource is ContainerResource)
        {
            return new ResourceMapping
            {
                Type = "Applications.Core/containers",
                ApiVersion = RadiusApiVersion
            };
        }

        if (resource is ProjectResource)
        {
            return new ResourceMapping
            {
                Type = "Applications.Core/containers",
                ApiVersion = RadiusApiVersion
            };
        }

        // Fallback: unmapped type → Applications.Core/containers with warning
        logger?.LogWarning(
            "Resource type '{ResourceType}' has no native Radius mapping. Falling back to Applications.Core/containers.",
            typeName);

        return new ResourceMapping
        {
            Type = "Applications.Core/containers",
            ApiVersion = RadiusApiVersion,
            IsFallback = true
        };
    }

    private static bool IsManualProvisioningType(Type resourceType)
    {
        var type = resourceType;
        while (type is not null)
        {
            if (s_manualProvisioningTypeNames.Contains(type.Name))
            {
                return true;
            }
            type = type.BaseType;
        }
        return false;
    }
}

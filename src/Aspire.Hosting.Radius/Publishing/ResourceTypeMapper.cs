// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Maps Aspire resource types to Radius portable resource types and metadata.
/// </summary>
internal static class ResourceTypeMapper
{
    /// <summary>
    /// The default Radius API version for portable resources.
    /// </summary>
    internal const string DefaultApiVersion = "2023-10-01-preview";

    // Map by type name to avoid taking dependencies on individual resource packages
    private static readonly Dictionary<string, ResourceMapping> s_mappingsByTypeName = new(StringComparer.Ordinal)
    {
        // Datastores
        ["RedisResource"] = new ResourceMapping(
            "Applications.Datastores/redisCaches",
            DefaultApiVersion,
            "default"),

        ["SqlServerServerResource"] = new ResourceMapping(
            "Applications.Datastores/sqlDatabases",
            DefaultApiVersion,
            "default"),

        ["SqlServerDatabaseResource"] = new ResourceMapping(
            "Applications.Datastores/sqlDatabases",
            DefaultApiVersion,
            "default"),

        ["MongoDBServerResource"] = new ResourceMapping(
            "Applications.Datastores/mongoDatabases",
            DefaultApiVersion,
            "default"),

        ["MongoDBDatabaseResource"] = new ResourceMapping(
            "Applications.Datastores/mongoDatabases",
            DefaultApiVersion,
            "default"),

        // Messaging
        ["RabbitMQServerResource"] = new ResourceMapping(
            "Applications.Messaging/rabbitMQQueues",
            DefaultApiVersion,
            "default"),

        // PostgreSQL → Manual provisioning (no native Radius type)
        ["PostgresServerResource"] = new ResourceMapping(
            "Applications.Datastores/sqlDatabases",
            DefaultApiVersion,
            "default"),

        ["PostgresDatabaseResource"] = new ResourceMapping(
            "Applications.Datastores/sqlDatabases",
            DefaultApiVersion,
            "default"),

        // Dapr
        ["DaprStateStoreResource"] = new ResourceMapping(
            "Applications.Dapr/stateStores",
            DefaultApiVersion,
            "default"),

        ["DaprPubSubResource"] = new ResourceMapping(
            "Applications.Dapr/pubSubBrokers",
            DefaultApiVersion,
            "default"),
    };

    private static readonly ResourceMapping s_containerMapping = new(
        "Applications.Core/containers",
        DefaultApiVersion,
        "default");

    /// <summary>
    /// Gets the Radius resource mapping for the specified Aspire resource type.
    /// </summary>
    /// <param name="resource">The Aspire resource to map.</param>
    /// <param name="logger">Optional logger for warning about unmapped types.</param>
    /// <returns>The resource mapping for the Aspire resource type.</returns>
    public static ResourceMapping GetRadiusType(IResource resource, ILogger? logger = null)
    {
        // Check the type hierarchy by name
        var type = resource.GetType();
        while (type is not null)
        {
            if (s_mappingsByTypeName.TryGetValue(type.Name, out var mapping))
            {
                return mapping;
            }
            type = type.BaseType;
        }

        // ContainerResource and ProjectResource → workload containers
        if (resource is ContainerResource || resource is ProjectResource)
        {
            return s_containerMapping;
        }

        // Fallback to generic container with warning
        logger?.LogWarning(
            "{ResourceType} is not mapped to a Radius portable resource type; using generic container fallback.",
            resource.GetType().Name);

        return s_containerMapping;
    }

    /// <summary>
    /// Determines whether the given resource maps to a workload container type
    /// (as opposed to a portable resource type like Redis, SQL, etc.).
    /// </summary>
    public static bool IsWorkloadResource(IResource resource)
    {
        // Check name-based mapping first — resources like RedisResource inherit
        // from ContainerResource but should be treated as portable resources.
        if (HasPortableResourceMapping(resource))
        {
            return false;
        }

        // Everything else (ContainerResource, ProjectResource, unmapped types) is a workload
        return true;
    }

    /// <summary>
    /// Returns true if the resource has an explicit portable resource mapping
    /// in the type name table (not the generic container fallback).
    /// </summary>
    private static bool HasPortableResourceMapping(IResource resource)
    {
        var type = resource.GetType();
        while (type is not null)
        {
            if (s_mappingsByTypeName.TryGetValue(type.Name, out _))
            {
                return true;
            }
            type = type.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Determines whether the given resource maps to a portable resource type
    /// (datastores, messaging, Dapr, etc.).
    /// </summary>
    public static bool IsPortableResource(IResource resource)
    {
        return !IsWorkloadResource(resource);
    }
}

/// <summary>
/// Represents the mapping from an Aspire resource type to a Radius resource type.
/// </summary>
/// <param name="Type">The Radius portable resource type string.</param>
/// <param name="ApiVersion">The Radius API version for this resource type.</param>
/// <param name="DefaultRecipe">The default Radius recipe name.</param>
internal readonly record struct ResourceMapping(
    string Type,
    string ApiVersion,
    string DefaultRecipe);

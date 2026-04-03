// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Preview;

/// <summary>
/// Walks the Aspire distributed application model and generates preview JSON files
/// that the Radius dashboard can consume to show a projected topology graph.
/// </summary>
internal sealed class PreviewGraphGenerator(ILogger logger)
{
    /// <summary>
    /// Generates preview data files (status.json, applications.json, graph.json)
    /// from the distributed application model.
    /// </summary>
    /// <param name="model">The Aspire distributed application model.</param>
    /// <param name="radiusEnv">The Radius environment resource providing namespace and app name.</param>
    /// <param name="outputDirectory">The directory to write preview JSON files to.</param>
    public async Task GenerateAsync(
        DistributedApplicationModel model,
        RadiusEnvironmentResource radiusEnv,
        string outputDirectory)
    {
        var appName = radiusEnv.EnvironmentName;
        var ns = radiusEnv.Namespace;

        logger.LogInformation("Starting preview data generation for application '{AppName}' in namespace '{Namespace}'", appName, ns);

        // Filter resources: only those with DeploymentTargetAnnotation targeting this Radius environment,
        // excluding RadiusEnvironmentResource and RadiusDashboardResource
        var targetedResources = new List<(IResource Resource, ResourceMapping Mapping)>();

        foreach (var resource in model.Resources)
        {
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            // Skip child resources (e.g., SqlServerDatabaseResource from AddDatabase()) —
            // they are represented by their parent in the preview graph.
            if (resource is IResourceWithParent)
            {
                continue;
            }

            var hasTargetAnnotation = resource.Annotations
                .OfType<DeploymentTargetAnnotation>()
                .Any(a => a.DeploymentTarget == radiusEnv);

            if (!hasTargetAnnotation)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusMapping(resource, logger);
            targetedResources.Add((resource, mapping));
        }

        logger.LogInformation("Found {ResourceCount} resources targeted at Radius environment", targetedResources.Count);

        // Build lookup maps for connection resolution
        var portableResources = new Dictionary<string, (IResource Resource, ResourceMapping Mapping)>(StringComparer.OrdinalIgnoreCase);
        var allResources = new Dictionary<string, (IResource Resource, ResourceMapping Mapping)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (resource, mapping) in targetedResources)
        {
            allResources[resource.Name] = (resource, mapping);
            if (ResourceTypeMapper.IsPortableResource(mapping))
            {
                portableResources[resource.Name] = (resource, mapping);
            }
        }

        // Build preview resources with connections
        var previewResources = new List<PreviewResource>();
        // Track inbound connections to add to portable resources later
        var inboundConnections = new Dictionary<string, List<PreviewConnection>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (resource, mapping) in targetedResources)
        {
            var previewResource = new PreviewResource
            {
                Id = BuildSyntheticId(ns, mapping.Type, resource.Name),
                Name = resource.Name,
                Type = mapping.Type,
            };

            // Discover outbound connections (workload → portable)
            foreach (var annotation in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
            {
                if (annotation.Type != "Reference")
                {
                    continue;
                }

                var referencedResource = annotation.Resource;
                var refName = referencedResource.Name;

                // Try to find the referenced resource in portable resources
                if (!portableResources.TryGetValue(refName, out var targetInfo))
                {
                    // Resolve child resources to their parent via IResourceWithParent
                    if (referencedResource is IResourceWithParent childRef)
                    {
                        var parentName = childRef.Parent.Name;
                        if (portableResources.TryGetValue(parentName, out targetInfo))
                        {
                            refName = parentName;
                        }
                        else
                        {
                            continue; // Parent not in portable resources either
                        }
                    }
                    else
                    {
                        continue; // Not a portable resource reference
                    }
                }

                var targetMapping = targetInfo.Mapping;
                var targetResource = targetInfo.Resource;

                // Add outbound connection on the workload
                previewResource.Connections.Add(new PreviewConnection
                {
                    Id = BuildSyntheticId(ns, targetMapping.Type, targetResource.Name),
                    Name = targetResource.Name,
                    Type = targetMapping.Type,
                    Direction = "Outbound",
                });

                // Track inbound connection for the portable resource
                if (!inboundConnections.TryGetValue(targetResource.Name, out var inboundList))
                {
                    inboundList = [];
                    inboundConnections[targetResource.Name] = inboundList;
                }

                inboundList.Add(new PreviewConnection
                {
                    Id = BuildSyntheticId(ns, mapping.Type, resource.Name),
                    Name = resource.Name,
                    Type = mapping.Type,
                    Direction = "Inbound",
                });
            }

            previewResources.Add(previewResource);
        }

        // Add tracked inbound connections to portable resources
        foreach (var previewResource in previewResources)
        {
            if (inboundConnections.TryGetValue(previewResource.Name, out var inbound))
            {
                previewResource.Connections.AddRange(inbound);
            }
        }

        var connectionCount = previewResources.Sum(r => r.Connections.Count(c => c.Direction == "Outbound"));
        logger.LogInformation("Built {ConnectionCount} connections across {ResourceCount} resources", connectionCount, previewResources.Count);

        // Write output files
        Directory.CreateDirectory(outputDirectory);

        var status = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = appName,
            Namespace = ns,
            ResourceCount = previewResources.Count,
        };

        var applications = new PreviewApplicationList
        {
            Value =
            [
                new PreviewApplication
                {
                    Id = BuildSyntheticId(ns, "Applications.Core/applications", appName),
                    Name = appName,
                }
            ]
        };

        var graph = new PreviewGraphResponse
        {
            Resources = previewResources,
        };

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };

        var statusPath = Path.Combine(outputDirectory, "status.json");
        var applicationsPath = Path.Combine(outputDirectory, "applications.json");
        var graphPath = Path.Combine(outputDirectory, "graph.json");

        await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(status, jsonOptions)).ConfigureAwait(false);
        await File.WriteAllTextAsync(applicationsPath, JsonSerializer.Serialize(applications, jsonOptions)).ConfigureAwait(false);
        await File.WriteAllTextAsync(graphPath, JsonSerializer.Serialize(graph, jsonOptions)).ConfigureAwait(false);

        logger.LogInformation("Preview data written to '{OutputDirectory}'", outputDirectory);
    }

    private static string BuildSyntheticId(string ns, string radiusType, string name)
    {
        return $"/planes/radius/local/resourceGroups/{ns}/providers/{radiusType}/{name}";
    }
}

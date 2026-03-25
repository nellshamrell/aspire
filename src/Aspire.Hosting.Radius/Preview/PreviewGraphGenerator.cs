// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Preview;

/// <summary>
/// Walks the <see cref="DistributedApplicationModel"/> and generates preview JSON files
/// (status.json, applications.json, graph.json) that the Radius dashboard can serve
/// to show a projected topology before deployment.
/// </summary>
internal sealed class PreviewGraphGenerator(ILogger logger)
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Generates preview JSON files from the application model and writes them to the specified output directory.
    /// </summary>
    internal async Task GenerateAsync(
        DistributedApplicationModel model,
        RadiusEnvironmentResource environment,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting preview graph generation for environment '{EnvironmentName}'", environment.Name);

        var ns = environment.Namespace;

        // Classify resources — same pattern as RadiusBicepPublishingContext
        var portableResources = new List<IResource>();
        var workloadResources = new List<IResource>();

        foreach (var resource in model.Resources)
        {
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            var deploymentAnnotation = resource.GetDeploymentTargetAnnotation(environment);
            if (deploymentAnnotation is null)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusType(resource, logger);

            if (ResourceTypeMapper.IsPortableResource(resource))
            {
                portableResources.Add(resource);
            }
            else
            {
                workloadResources.Add(resource);
            }
        }

        var allResources = workloadResources.Concat(portableResources).ToList();

        logger.LogInformation("Found {ResourceCount} resources targeted at Radius ({Workloads} workloads, {Portables} portables)",
            allResources.Count, workloadResources.Count, portableResources.Count);

        // Build preview resources with connections
        var previewResources = new List<PreviewResource>();
        var resourceMap = new Dictionary<string, PreviewResource>();

        foreach (var resource in allResources)
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource, logger);
            var id = BuildSyntheticId(ns, mapping.Type, resource.Name);

            var previewResource = new PreviewResource
            {
                Id = id,
                Name = resource.Name,
                Type = mapping.Type,
            };

            previewResources.Add(previewResource);
            resourceMap[resource.Name] = previewResource;
        }

        // Build bidirectional connections
        foreach (var resource in workloadResources)
        {
            if (!resource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var refs))
            {
                continue;
            }

            foreach (var refAnnotation in refs)
            {
                var referencedResource = refAnnotation.Resource;
                if (!resourceMap.TryGetValue(referencedResource.Name, out var targetPreview))
                {
                    continue;
                }

                if (!resourceMap.TryGetValue(resource.Name, out var sourcePreview))
                {
                    continue;
                }

                // Workload → Portable: Outbound on workload, Inbound on portable
                sourcePreview.Connections.Add(new PreviewConnection
                {
                    Id = targetPreview.Id,
                    Name = targetPreview.Name,
                    Type = targetPreview.Type,
                    Direction = "Outbound",
                });

                targetPreview.Connections.Add(new PreviewConnection
                {
                    Id = sourcePreview.Id,
                    Name = sourcePreview.Name,
                    Type = sourcePreview.Type,
                    Direction = "Inbound",
                });
            }
        }

        // Derive application name
        var appName = $"{environment.Name}-app";
        var appId = BuildSyntheticId(ns, "Applications.Core/applications", appName);

        // Write status.json
        var status = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = appName,
            Namespace = ns,
            ResourceCount = previewResources.Count,
        };

        // Write applications.json
        var applications = new PreviewApplicationList
        {
            Value =
            [
                new PreviewApplication
                {
                    Id = appId,
                    Name = appName,
                }
            ],
        };

        // Write graph.json
        var graph = new PreviewGraphResponse
        {
            Resources = previewResources,
        };

        Directory.CreateDirectory(outputPath);

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "status.json"),
            JsonSerializer.Serialize(status, s_jsonOptions),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "applications.json"),
            JsonSerializer.Serialize(applications, s_jsonOptions),
            cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "graph.json"),
            JsonSerializer.Serialize(graph, s_jsonOptions),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Preview graph written to '{OutputPath}' ({ResourceCount} resources, {ConnectionCount} connections)",
            outputPath, previewResources.Count, previewResources.Sum(r => r.Connections.Count));
    }

    private static string BuildSyntheticId(string ns, string radiusType, string name)
    {
        return $"/planes/radius/local/resourceGroups/{ns}/providers/{radiusType}/{name}";
    }
}

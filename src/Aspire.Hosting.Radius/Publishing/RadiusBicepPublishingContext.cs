// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Context for generating Bicep templates from the distributed application model
/// for Radius deployment.
/// </summary>
internal sealed class RadiusBicepPublishingContext(
    DistributedApplicationExecutionContext executionContext,
    string outputPath,
    ILogger logger,
    CancellationToken cancellationToken = default)
{
    internal string OutputPath { get; } = outputPath;

    /// <summary>
    /// Writes the Bicep template for the given model and Radius environment to the output directory.
    /// </summary>
    internal async Task WriteModelAsync(DistributedApplicationModel model, RadiusEnvironmentResource environment)
    {
        if (!executionContext.IsPublishMode)
        {
            return;
        }

        logger.LogInformation("Starting Radius Bicep generation for environment '{EnvironmentName}'", environment.Name);

        ArgumentNullException.ThrowIfNull(model);

        if (model.Resources.Count == 0)
        {
            logger.LogWarning("No resources found in the application model. Skipping Bicep generation.");
            return;
        }

        var bicepContent = GenerateBicep(model, environment);

        Directory.CreateDirectory(OutputPath);
        var bicepFilePath = Path.Combine(OutputPath, "app.bicep");
        await File.WriteAllTextAsync(bicepFilePath, bicepContent, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Radius Bicep template written to '{BicepFilePath}'", bicepFilePath);
    }

    /// <summary>
    /// Generates the Bicep template content from the application model.
    /// </summary>
    internal string GenerateBicep(DistributedApplicationModel model, RadiusEnvironmentResource environment)
    {
        var builder = new BicepTemplateBuilder();

        // Classify resources
        var portableResources = new List<IResource>();
        var workloadResources = new List<IResource>();

        foreach (var resource in model.Resources)
        {
            // Skip the Radius environment itself and dashboard resources
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            // Only include resources that target this Radius environment
            var deploymentAnnotation = resource.GetDeploymentTargetAnnotation(environment);
            if (deploymentAnnotation is null)
            {
                continue;
            }

            if (ResourceTypeMapper.IsPortableResource(resource))
            {
                portableResources.Add(resource);
            }
            else
            {
                workloadResources.Add(resource);
            }
        }

        // 1. Add environment resource with compute target and recipe registrations
        var recipes = BuildRecipeRegistrations(portableResources);
        builder.AddEnvironmentResource(environment.Name, environment.Namespace, recipes.Count > 0 ? recipes : null);

        // 2. Add application resource
        var appName = GetApplicationName(model);
        builder.AddApplicationResource(appName, environment.Name);

        // 3. Add portable resources (datastores, messaging, etc.)
        foreach (var resource in portableResources)
        {
            AddPortableResourceToBicep(builder, resource, appName, environment.Name);
        }

        // 4. Add workload containers
        foreach (var resource in workloadResources)
        {
            AddWorkloadResourceToBicep(builder, resource, appName, portableResources);
        }

        return builder.Render();
    }

    private void AddPortableResourceToBicep(
        BicepTemplateBuilder builder,
        IResource resource,
        string applicationName,
        string environmentName)
    {
        var customization = GetCustomization(resource);
        var mapping = ResourceTypeMapper.GetRadiusType(resource, logger);

        if (customization?.Provisioning == RadiusResourceProvisioning.Manual)
        {
            // Manual provisioning validation
            if (string.IsNullOrEmpty(customization.Host) || customization.Port is null or 0)
            {
                throw new InvalidOperationException(
                    $"Resource '{resource.Name}' is configured for manual provisioning but 'Host' and/or 'Port' " +
                    $"are not set. Set Host and Port via PublishAsRadiusResource() when using RadiusResourceProvisioning.Manual.");
            }

            builder.AddManuallyProvisionedResource(
                mapping.Type,
                resource.Name,
                applicationName,
                environmentName,
                customization.Host,
                customization.Port.Value);
        }
        else
        {
            Dictionary<string, string>? properties = null;

            // Apply custom recipe if specified
            if (!string.IsNullOrEmpty(customization?.Recipe))
            {
                properties = new Dictionary<string, string>
                {
                    ["recipe"] = $"{{ name: '{customization.Recipe}' }}"
                };
            }

            builder.AddPortableResource(
                mapping.Type,
                resource.Name,
                applicationName,
                environmentName,
                properties);
        }
    }

    private static void AddWorkloadResourceToBicep(
        BicepTemplateBuilder builder,
        IResource resource,
        string applicationName,
        List<IResource> portableResources)
    {
        var image = GetContainerImage(resource);
        var connections = BuildConnections(resource, portableResources);
        var envVars = BuildEnvironmentVariables();

        builder.AddWorkloadResource(
            resource.Name,
            image,
            applicationName,
            connections.Count > 0 ? connections : null,
            envVars.Count > 0 ? envVars : null);
    }

    private Dictionary<string, string> BuildRecipeRegistrations(List<IResource> portableResources)
    {
        var recipes = new Dictionary<string, string>();

        foreach (var resource in portableResources)
        {
            var customization = GetCustomization(resource);
            var mapping = ResourceTypeMapper.GetRadiusType(resource, logger);

            if (mapping.Type != "Applications.Core/containers" && !recipes.ContainsKey(mapping.Type))
            {
                var recipeName = customization?.Recipe ?? mapping.DefaultRecipe;
                recipes[mapping.Type] = recipeName;
            }
        }

        return recipes;
    }

    private static string GetApplicationName(DistributedApplicationModel model)
    {
        // Derive a distinct application name from the environment name to avoid
        // Bicep identifier collisions (BCP028) with the environment resource.
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        var envName = radiusEnv?.Name ?? "default";
        return $"{envName}-app";
    }

    private static string GetContainerImage(IResource resource)
    {
        if (resource is ContainerResource container)
        {
            // Try to get image from annotations
            if (container.TryGetContainerImageName(out var imageName))
            {
                return imageName;
            }
        }

        // Default fallback for project resources or resources where image isn't directly available
        return $"{resource.Name}:latest";
    }

    private static Dictionary<string, string> BuildConnections(IResource resource, List<IResource> portableResources)
    {
        var connections = new Dictionary<string, string>();

        // Check for resource relationships in annotations
        if (resource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var refs))
        {
            foreach (var refAnnotation in refs)
            {
                var referencedResource = refAnnotation.Resource;
                if (portableResources.Any(r => r.Name == referencedResource.Name))
                {
                    connections[referencedResource.Name] = BicepTemplateBuilder.SanitizeName(referencedResource.Name);
                }
            }
        }

        return connections;
    }

    private static Dictionary<string, string> BuildEnvironmentVariables()
    {
        // Radius portable resources don't expose a connectionString() Bicep function.
        // Connection information is injected automatically via the 'connections' block
        // on the container resource, so no explicit environment variables are needed
        // for portable resource references.
        return new Dictionary<string, string>();
    }

    private static RadiusResourceCustomization? GetCustomization(IResource resource)
    {
        if (resource.TryGetAnnotationsOfType<RadiusResourceCustomizationAnnotation>(out var annotations))
        {
            return annotations.LastOrDefault()?.Customization;
        }

        return null;
    }
}

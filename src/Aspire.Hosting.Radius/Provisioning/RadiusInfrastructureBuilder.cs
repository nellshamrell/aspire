// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Azure.Provisioning.Expressions;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Builds an <see cref="Infrastructure"/> instance from the Aspire app model and compiles
/// it to Bicep using the Azure Provisioning AST. This replaces string-based Bicep generation
/// with a mutable, inspectable object model.
/// </summary>
internal sealed class RadiusInfrastructureBuilder(ILogger logger)
{
    /// <summary>
    /// Generates Bicep content from the application model using the Azure Provisioning AST.
    /// </summary>
    internal string GenerateBicep(
        DistributedApplicationModel model,
        RadiusEnvironmentResource environment,
        Action<Infrastructure>? configureInfrastructure = null)
    {
        var infra = new Infrastructure("radius");

        // Classify resources
        var portableResources = new List<(IResource Resource, ResourceMapping Mapping)>();
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

            if (ResourceTypeMapper.IsPortableResource(resource))
            {
                var mapping = ResourceTypeMapper.GetRadiusType(resource, logger);
                portableResources.Add((resource, mapping));
            }
            else
            {
                workloadResources.Add(resource);
            }
        }

        // 1. Environment resource
        var envIdentifier = BicepIdentifier.Sanitize(environment.Name);
        var envConstruct = new RadiusEnvironmentConstruct(envIdentifier);
        envConstruct.Name = environment.Name;
        envConstruct.ComputeKind = "kubernetes";
        envConstruct.ComputeNamespace = environment.Namespace;

        // Register default recipes for each portable resource type
        foreach (var (_, mapping) in portableResources)
        {
            if (!envConstruct.Recipes.ContainsKey(mapping.Type))
            {
                var recipePathSuffix = mapping.Type.Split('/').Last().ToLowerInvariant();
                envConstruct.AddRecipe(
                    mapping.Type,
                    "default",
                    "bicep",
                    $"ghcr.io/radius-project/recipes/local-dev/{recipePathSuffix}:latest");
            }
        }

        infra.Add(envConstruct);

        // 2. Application resource
        var appName = GetApplicationName(model);
        var appIdentifier = BicepIdentifier.Sanitize(appName);
        var appConstruct = new RadiusApplicationConstruct(appIdentifier);
        appConstruct.Name = appName;
        SetExpression(appConstruct.Environment, envIdentifier, "id");
        infra.Add(appConstruct);

        // 3. Portable resources
        var portableIdentifiers = new Dictionary<string, string>(); // resource name → bicep identifier
        foreach (var (resource, mapping) in portableResources)
        {
            var identifier = BicepIdentifier.Sanitize(resource.Name);
            portableIdentifiers[resource.Name] = identifier;

            var customization = GetCustomization(resource);

            if (customization?.Provisioning == RadiusResourceProvisioning.Manual)
            {
                if (string.IsNullOrEmpty(customization.Host) || customization.Port is null or 0)
                {
                    throw new InvalidOperationException(
                        $"Resource '{resource.Name}' is configured for manual provisioning but 'Host' and/or 'Port' " +
                        $"are not set. Set Host and Port via PublishAsRadiusResource() when using RadiusResourceProvisioning.Manual.");
                }

                var manualConstruct = new RadiusPortableResourceConstruct(identifier, mapping.Type);
                manualConstruct.Name = resource.Name;
                SetExpression(manualConstruct.Application, appIdentifier, "id");
                SetExpression(manualConstruct.Environment, envIdentifier, "id");
                manualConstruct.ResourceProvisioning = "manual";
                manualConstruct.Host = customization.Host;
                manualConstruct.Port = customization.Port.Value;
                infra.Add(manualConstruct);
            }
            else
            {
                var construct = new RadiusPortableResourceConstruct(identifier, mapping.Type);
                construct.Name = resource.Name;
                SetExpression(construct.Application, appIdentifier, "id");
                SetExpression(construct.Environment, envIdentifier, "id");

                if (!string.IsNullOrEmpty(customization?.Recipe))
                {
                    construct.RecipeName = customization.Recipe;
                }

                infra.Add(construct);
            }
        }

        // 4. Workload containers
        foreach (var resource in workloadResources)
        {
            var identifier = BicepIdentifier.Sanitize(resource.Name);
            var image = GetContainerImage(resource);

            var construct = new RadiusContainerConstruct(identifier);
            construct.Name = resource.Name;
            SetExpression(construct.Application, appIdentifier, "id");
            construct.ContainerImage = image;
            construct.ImagePullPolicy = "Never";

            // Add connections to portable resources
            if (resource.TryGetAnnotationsOfType<ResourceRelationshipAnnotation>(out var refs))
            {
                foreach (var refAnnotation in refs)
                {
                    if (portableIdentifiers.TryGetValue(refAnnotation.Resource.Name, out var portableId))
                    {
                        construct.AddConnection(refAnnotation.Resource.Name, portableId);
                    }
                }
            }

            infra.Add(construct);
        }

        // Allow user customization before compilation
        configureInfrastructure?.Invoke(infra);

        foreach (var envResource in infra.GetProvisionableResources().OfType<RadiusEnvironmentConstruct>())
        {
            envResource.ApplyRecipes();
        }

        foreach (var containerResource in infra.GetProvisionableResources().OfType<RadiusContainerConstruct>())
        {
            containerResource.ApplyConnections();
        }

        // Compile to Bicep
        var plan = infra.Build(new ProvisioningBuildOptions());
        var compilation = plan.Compile();
        var bicepContent = compilation.Values.First();

        // Prepend the `extension radius` directive (not representable in the AST)
        return $"extension radius\n\n{bicepContent}";
    }

    private static void SetExpression(BicepValue<string> property, string resourceIdentifier, string member)
    {
        ((IBicepValue)property).Expression = new MemberExpression(new IdentifierExpression(resourceIdentifier), member);
    }

    private static string GetApplicationName(DistributedApplicationModel model)
    {
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().FirstOrDefault();
        var envName = radiusEnv?.Name ?? "default";
        return $"{envName}-app";
    }

    private static string GetContainerImage(IResource resource)
    {
        if (resource is ContainerResource container && container.TryGetContainerImageName(out var imageName))
        {
            return imageName;
        }
        return $"{resource.Name}:latest";
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

/// <summary>
/// Helper for sanitizing resource names to valid Bicep identifiers.
/// </summary>
internal static class BicepIdentifier
{
    /// <summary>
    /// Sanitizes a resource name to be a valid Bicep identifier.
    /// Removes hyphens, dots, and other invalid characters; ensures it starts with a letter.
    /// Avoids collision with the 'radius' Bicep extension name.
    /// </summary>
    internal static string Sanitize(string name)
    {
        var sb = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString();

        if (result.Length > 0 && !char.IsLetter(result[0]))
        {
            result = "r" + result;
        }

        if (result.Length == 0)
        {
            result = "resource";
        }

        // Avoid collision with the 'radius' Bicep extension name
        if (string.Equals(result, "radius", StringComparison.OrdinalIgnoreCase))
        {
            result = result + "env";
        }

        return result;
    }
}

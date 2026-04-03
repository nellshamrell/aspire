// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Builds Radius-compatible Bicep templates from an Aspire distributed application model.
/// Handles environment, application, portable resource, and container workload generation.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    private readonly DistributedApplicationModel _model;
    private readonly RadiusEnvironmentResource _environment;
    private readonly ILogger _logger;

    /// <summary>
    /// The classified portable resources (datastores, messaging, etc.).
    /// </summary>
    public List<ClassifiedResource> PortableResources { get; } = [];

    /// <summary>
    /// The classified container workload resources.
    /// </summary>
    public List<ClassifiedResource> ContainerResources { get; } = [];

    /// <summary>
    /// The environment name used in Bicep output.
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// The application name used in Bicep output.
    /// </summary>
    public string ApplicationName { get; set; }

    /// <summary>
    /// The Kubernetes namespace for the environment.
    /// </summary>
    public string Namespace { get; set; }

    public RadiusInfrastructureBuilder(DistributedApplicationModel model, RadiusEnvironmentResource environment, ILogger logger)
    {
        _model = model;
        _environment = environment;
        _logger = logger;
        EnvironmentName = environment.EnvironmentName;
        ApplicationName = environment.EnvironmentName;
        Namespace = environment.Namespace;
    }

    /// <summary>
    /// Classifies all model resources into portable resources or container workloads,
    /// scoped to this environment's deployment targets.
    /// </summary>
    public void ClassifyResources(RadiusEnvironmentResource? firstEnvironment)
    {
        foreach (var resource in _model.Resources)
        {
            // Skip Radius infrastructure resources
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            // T042e: scope to deployment target annotations
            if (!IsResourceTargetedToEnvironment(resource, firstEnvironment))
            {
                continue;
            }

            // Skip non-deployable resources (e.g. ParameterResource, password parameters)
            if (resource is ParameterResource)
            {
                continue;
            }

            // Skip child resources — they are processed via their parent
            if (resource is IResourceWithParent)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusMapping(resource, _logger);
            var customization = GetCustomization(resource);

            // T042c: manual-provisioned resources are always portable, not containers
            var isManual = customization?.Provisioning == RadiusResourceProvisioning.Manual
                           || mapping.DefaultProvisioning == RadiusResourceProvisioning.Manual;

            if (ResourceTypeMapper.IsPortableResource(mapping) || isManual)
            {
                PortableResources.Add(new ClassifiedResource(resource, mapping, customization));
            }
            else if (mapping.Type == "Applications.Core/containers")
            {
                ContainerResources.Add(new ClassifiedResource(resource, mapping, customization));
            }
        }
    }

    /// <summary>
    /// Generates the complete Bicep template string.
    /// </summary>
    public string Build()
    {
        var sb = new StringBuilder();

        // Extension directive
        sb.AppendLine("extension radius");
        sb.AppendLine();

        // Environment resource
        WriteEnvironmentBlock(sb);

        // Application resource
        WriteApplicationBlock(sb);

        // Portable resources
        foreach (var pr in PortableResources)
        {
            WritePortableResourceBlock(sb, pr);
        }

        // Container workloads
        foreach (var cr in ContainerResources)
        {
            WriteContainerBlock(sb, cr);
        }

        return sb.ToString();
    }

    private void WriteEnvironmentBlock(StringBuilder sb)
    {
        var envId = BicepIdentifier.Sanitize(EnvironmentName);
        sb.AppendLine($"resource {envId} 'Applications.Core/environments@{ResourceTypeMapper.RadiusApiVersion}' = {{");
        sb.AppendLine($"  name: '{EnvironmentName}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    compute: {");
        sb.AppendLine("      kind: 'kubernetes'");
        sb.AppendLine($"      namespace: '{Namespace}'");
        sb.AppendLine("    }");

        // Recipe configuration
        var recipesForEnv = GetRecipeConfig();
        if (recipesForEnv.Count > 0)
        {
            sb.AppendLine("    recipes: {");
            foreach (var (resourceType, recipes) in recipesForEnv)
            {
                sb.AppendLine($"      '{resourceType}': {{");
                foreach (var (recipeName, templatePath) in recipes)
                {
                    sb.AppendLine($"        '{recipeName}': {{");
                    sb.AppendLine("          templateKind: 'bicep'");
                    sb.AppendLine($"          templatePath: '{templatePath}'");
                    sb.AppendLine("        }");
                }
                sb.AppendLine("      }");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private void WriteApplicationBlock(StringBuilder sb)
    {
        var appId = BicepIdentifier.Sanitize(ApplicationName) + "_app";
        var envId = BicepIdentifier.Sanitize(EnvironmentName);
        sb.AppendLine($"resource {appId} 'Applications.Core/applications@{ResourceTypeMapper.RadiusApiVersion}' = {{");
        sb.AppendLine($"  name: '{ApplicationName}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    environment: {envId}.id");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private void WritePortableResourceBlock(StringBuilder sb, ClassifiedResource classified)
    {
        var id = BicepIdentifier.Sanitize(classified.Resource.Name);
        var envId = BicepIdentifier.Sanitize(EnvironmentName);
        var appId = BicepIdentifier.Sanitize(ApplicationName) + "_app";
        var mapping = classified.Mapping;
        var customization = classified.Customization;
        var isManual = customization?.Provisioning == RadiusResourceProvisioning.Manual
                       || mapping.DefaultProvisioning == RadiusResourceProvisioning.Manual;

        sb.AppendLine($"resource {id} '{mapping.Type}@{mapping.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{classified.Resource.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    environment: {envId}.id");
        sb.AppendLine($"    application: {appId}.id");

        if (isManual)
        {
            sb.AppendLine("    resourceProvisioning: 'manual'");
            if (customization?.Host is not null)
            {
                sb.AppendLine($"    host: '{customization.Host}'");
            }
            if (customization?.Port is not null)
            {
                sb.AppendLine($"    port: {customization.Port}");
            }
        }

        // T042b: emit recipe name when custom recipe is specified
        if (customization?.Recipe is not null)
        {
            sb.AppendLine("    recipe: {");
            sb.AppendLine($"      name: '{customization.Recipe.Name}'");
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private void WriteContainerBlock(StringBuilder sb, ClassifiedResource classified)
    {
        var id = BicepIdentifier.Sanitize(classified.Resource.Name);
        var envId = BicepIdentifier.Sanitize(EnvironmentName);
        var appId = BicepIdentifier.Sanitize(ApplicationName) + "_app";

        sb.AppendLine($"resource {id} 'Applications.Core/containers@{ResourceTypeMapper.RadiusApiVersion}' = {{");
        sb.AppendLine($"  name: '{classified.Resource.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    environment: {envId}.id");
        sb.AppendLine($"    application: {appId}.id");

        // Container configuration
        var image = GetContainerImage(classified.Resource);
        if (image is not null)
        {
            sb.AppendLine("    container: {");
            sb.AppendLine($"      image: '{image}'");

            // Environment variables
            var envVars = GetEnvironmentVariables(classified.Resource);
            if (envVars.Count > 0)
            {
                sb.AppendLine("      env: {");
                foreach (var (key, value) in envVars)
                {
                    sb.AppendLine($"        {BicepIdentifier.Sanitize(key)}: '{value}'");
                }
                sb.AppendLine("      }");
            }

            sb.AppendLine("    }");
        }

        // Connections (from WithReference)
        var connections = GetConnections(classified.Resource);
        if (connections.Count > 0)
        {
            sb.AppendLine("    connections: {");
            foreach (var (connName, sourceId, isLiteral) in connections)
            {
                // T042d: quote connection names to handle hyphens
                sb.AppendLine($"      '{connName}': {{");
                if (isLiteral)
                {
                    sb.AppendLine($"        source: {sourceId}");
                }
                else
                {
                    sb.AppendLine($"        source: {sourceId}.id");
                }
                sb.AppendLine("      }");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private Dictionary<string, List<(string Name, string TemplatePath)>> GetRecipeConfig()
    {
        var recipes = new Dictionary<string, List<(string, string)>>();

        foreach (var pr in PortableResources)
        {
            var mapping = pr.Mapping;
            var customization = pr.Customization;

            // Skip manual provisioned resources — they don't use recipes
            if (customization?.Provisioning == RadiusResourceProvisioning.Manual
                || mapping.DefaultProvisioning == RadiusResourceProvisioning.Manual)
            {
                continue;
            }

            string recipeName;
            string templatePath;

            if (customization?.Recipe is not null)
            {
                recipeName = customization.Recipe.Name;
                templatePath = customization.Recipe.TemplatePath ?? mapping.DefaultRecipe ?? "default";
            }
            else if (mapping.DefaultRecipe is not null)
            {
                recipeName = "default";
                templatePath = mapping.DefaultRecipe;
            }
            else
            {
                continue;
            }

            if (!recipes.TryGetValue(mapping.Type, out var list))
            {
                list = [];
                recipes[mapping.Type] = list;
            }

            // Avoid duplicate recipe registrations for the same type+name
            if (!list.Any(r => r.Item1 == recipeName))
            {
                list.Add((recipeName, templatePath));
            }
        }

        return recipes;
    }

    private static string? GetContainerImage(IResource resource)
    {
        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().LastOrDefault();
        if (imageAnnotation is null)
        {
            return null;
        }

        var image = imageAnnotation.Image;
        if (imageAnnotation.Tag is not null)
        {
            image += ":" + imageAnnotation.Tag;
        }
        else if (imageAnnotation.SHA256 is not null)
        {
            image += "@sha256:" + imageAnnotation.SHA256;
        }

        return image;
    }

    private static Dictionary<string, string> GetEnvironmentVariables(IResource _)
    {
        var envVars = new Dictionary<string, string>();

        // We can capture static env vars but not dynamic expressions in Bicep.
        // For now, skip callback-based env vars as they require runtime resolution.
        // EnvironmentCallbackAnnotation instances are intentionally not processed here.

        return envVars;
    }

    private List<(string Name, string SourceBicepId, bool IsLiteral)> GetConnections(IResource resource)
    {
        var connections = new List<(string, string, bool)>();

        // Build a lookup of portable resource identifiers
        var portableIdentifiers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pr in PortableResources)
        {
            portableIdentifiers[pr.Resource.Name] = BicepIdentifier.Sanitize(pr.Resource.Name);
        }

        foreach (var annotation in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
        {
            if (annotation.Type != "Reference")
            {
                continue;
            }

            var referencedResource = annotation.Resource;
            var refName = referencedResource.Name;

            // Try to find the portable resource ID
            if (portableIdentifiers.TryGetValue(refName, out var bicepId))
            {
                connections.Add((refName, bicepId, false));
                continue;
            }

            // T042a: resolve child resources to their parent
            if (referencedResource is IResourceWithParent childRef)
            {
                var parentName = childRef.Parent.Name;
                if (portableIdentifiers.TryGetValue(parentName, out var parentBicepId))
                {
                    connections.Add((refName, parentBicepId, false));
                    continue;
                }
            }

            // T040c: DNS service name mapping for non-portable references
            connections.Add((refName, $"'{refName}.svc.cluster.local'", true));
        }

        return connections;
    }

    private bool IsResourceTargetedToEnvironment(IResource resource, RadiusEnvironmentResource? firstEnvironment)
    {
        var deploymentAnnotations = resource.Annotations.OfType<DeploymentTargetAnnotation>().ToList();

        // No deployment annotations — T042e: default to first environment only
        if (deploymentAnnotations.Count == 0)
        {
            return _environment == firstEnvironment;
        }

        // Check if any deployment annotation targets this environment
        return deploymentAnnotations.Any(a => a.DeploymentTarget == _environment);
    }

    private static RadiusResourceCustomization? GetCustomization(IResource resource)
    {
        var annotation = resource.Annotations.OfType<RadiusResourceCustomizationAnnotation>().FirstOrDefault();
        return annotation?.Customization;
    }
}

/// <summary>
/// A resource classified with its Radius mapping and optional customization.
/// </summary>
/// <param name="resource">The Aspire resource.</param>
/// <param name="mapping">The Radius resource type mapping.</param>
/// <param name="customization">Optional user-specified customization.</param>
public sealed class ClassifiedResource(IResource resource, ResourceMapping mapping, RadiusResourceCustomization? customization)
{
    /// <summary>Gets the original Aspire resource.</summary>
    public IResource Resource { get; } = resource;

    /// <summary>Gets or sets the Radius resource type mapping.</summary>
    public ResourceMapping Mapping { get; set; } = mapping;

    /// <summary>Gets or sets the optional user-specified customization.</summary>
    public RadiusResourceCustomization? Customization { get; set; } = customization;
}

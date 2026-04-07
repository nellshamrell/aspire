// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Builds an Azure Provisioning SDK <see cref="Infrastructure"/> AST from the Aspire app model,
/// compiles it to Bicep, and post-processes the output for Radius-specific syntax.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    private readonly DistributedApplicationModel _model;
    private readonly RadiusEnvironmentResource _radiusEnvironment;
    private readonly ILogger _logger;

    // Constructs built during classification
    private RadiusEnvironmentConstruct _environmentConstruct = null!;
    private RadiusApplicationConstruct _applicationConstruct = null!;
    private readonly List<RadiusPortableResourceConstruct> _portableResources = [];
    private readonly List<RadiusContainerConstruct> _containers = [];

    // Maps Aspire resource names to their Bicep identifiers for cross-referencing
    private readonly Dictionary<string, string> _portableIdentifiers = new(StringComparer.OrdinalIgnoreCase);

    public RadiusInfrastructureBuilder(
        DistributedApplicationModel model,
        RadiusEnvironmentResource radiusEnvironment,
        ILogger logger)
    {
        _model = model;
        _radiusEnvironment = radiusEnvironment;
        _logger = logger;
    }

    /// <summary>
    /// Builds the Bicep string for the configured Radius environment.
    /// </summary>
    /// <param name="configureCallback">Optional callback to customize the AST before compilation.</param>
    /// <returns>The compiled Bicep string with Radius extensions.</returns>
    public string Build(Action<RadiusInfrastructureOptions>? configureCallback = null)
    {
        ClassifyResources();
        BuildEnvironmentConstruct();
        BuildApplicationConstruct();
        BuildPortableResourceConstructs();
        BuildContainerConstructs();

        // Invoke user customization callback before compilation
        if (configureCallback is not null)
        {
            var options = new RadiusInfrastructureOptions(
                _environmentConstruct,
                _applicationConstruct,
                _portableResources,
                _containers);

            configureCallback(options);
        }

        var bicep = CompileInfrastructure();
        return PostProcess(bicep);
    }

    private void ClassifyResources()
    {
        foreach (var resource in _model.Resources)
        {
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            if (resource is RadiusDeploymentResource)
            {
                continue;
            }

            // Check if this resource is targeted at this Radius environment
            var deploymentAnnotation = resource.Annotations
                .OfType<DeploymentTargetAnnotation>()
                .FirstOrDefault(a => a.ComputeEnvironment == _radiusEnvironment);

            if (deploymentAnnotation is null)
            {
                continue;
            }

            if (ResourceTypeMapper.IsPortableResource(resource))
            {
                _logger.LogDebug("Classified resource '{Name}' as portable resource.", resource.Name);
            }
            else if (resource is ContainerResource or ProjectResource)
            {
                _logger.LogDebug("Classified resource '{Name}' as container workload.", resource.Name);
            }
            else
            {
                _logger.LogDebug("Classified resource '{Name}' as container workload (fallback).", resource.Name);
            }
        }
    }

    private void BuildEnvironmentConstruct()
    {
        var envId = BicepIdentifier.Sanitize(_radiusEnvironment.Name);
        _environmentConstruct = new RadiusEnvironmentConstruct(envId)
        {
            Name = _radiusEnvironment.Name,
            ComputeKind = "kubernetes",
            ComputeNamespace = _radiusEnvironment.Namespace,
        };
    }

    private void BuildApplicationConstruct()
    {
        var appId = BicepIdentifier.Sanitize("app");
        _applicationConstruct = new RadiusApplicationConstruct(appId)
        {
            Name = _radiusEnvironment.Name + "-app",
            EnvironmentId = _radiusEnvironment.Name, // Will be post-processed to Bicep reference
        };
    }

    private void BuildPortableResourceConstructs()
    {
        foreach (var resource in GetTargetedResources())
        {
            if (!ResourceTypeMapper.IsPortableResource(resource))
            {
                continue;
            }

            // Resolve to parent if this is a child resource (e.g., SqlServerDatabaseResource)
            var effectiveResource = ResolveToParent(resource);
            var mapping = ResourceTypeMapper.GetRadiusType(effectiveResource);
            var bicepId = BicepIdentifier.Sanitize(effectiveResource.Name);

            // Track for connection cross-referencing
            _portableIdentifiers[effectiveResource.Name] = bicepId;
            // Also track the child name pointing to the same identifier
            if (effectiveResource != resource)
            {
                _portableIdentifiers[resource.Name] = bicepId;
            }

            // Avoid duplicate constructs if both parent and child are in the model
            if (_portableResources.Any(p => p.BicepIdentifier == bicepId))
            {
                continue;
            }

            var construct = new RadiusPortableResourceConstruct(bicepId, mapping.Type, mapping.ApiVersion)
            {
                Name = effectiveResource.Name,
                ApplicationId = _radiusEnvironment.Name + "-app", // Will be post-processed to Bicep reference
                EnvironmentId = _radiusEnvironment.Name, // Will be post-processed to Bicep reference
            };

            // Apply customization if present
            var customization = GetCustomization(effectiveResource) ?? GetCustomization(resource);
            if (customization is not null)
            {
                ApplyCustomization(construct, customization);
            }
            else if (effectiveResource.GetType().Name == "PostgresServerResource")
            {
                // PostgreSQL defaults to manual provisioning (FR-019)
                construct.ResourceProvisioning = "manual";
            }

            _portableResources.Add(construct);
            _logger.LogDebug("Built portable resource construct '{BicepId}' of type '{Type}'.", bicepId, mapping.Type);
        }
    }

    private void BuildContainerConstructs()
    {
        foreach (var resource in GetTargetedResources())
        {
            if (resource is not (ContainerResource or ProjectResource))
            {
                continue;
            }

            var bicepId = BicepIdentifier.Sanitize(resource.Name);
            var construct = new RadiusContainerConstruct(bicepId)
            {
                Name = resource.Name,
                ApplicationId = _radiusEnvironment.Name + "-app", // Will be post-processed to Bicep reference
                ContainerImage = GetContainerImage(resource),
            };

            // Build connections from ResourceRelationshipAnnotation
            foreach (var relationship in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
            {
                var referencedResource = relationship.Resource;
                var connName = referencedResource.Name;

                // Resolve child resources to parent for portable identifier lookup
                var effectiveRef = ResolveToParent(referencedResource);
                if (_portableIdentifiers.TryGetValue(effectiveRef.Name, out var portableId))
                {
                    construct.Connections[connName] = portableId;
                }
                else if (_portableIdentifiers.TryGetValue(connName, out var directId))
                {
                    construct.Connections[connName] = directId;
                }
            }

            _containers.Add(construct);
            _logger.LogDebug("Built container construct '{BicepId}' with {ConnectionCount} connections.", bicepId, construct.Connections.Count);
        }
    }

    private string CompileInfrastructure()
    {
        var infrastructure = new Infrastructure("radius");

        infrastructure.Add(_environmentConstruct);
        infrastructure.Add(_applicationConstruct);

        foreach (var portable in _portableResources)
        {
            infrastructure.Add(portable);
        }

        foreach (var container in _containers)
        {
            infrastructure.Add(container);
        }

        var plan = infrastructure.Build(new ProvisioningBuildOptions());
        var compilation = plan.Compile();
        return compilation.First().Value;
    }

    private string PostProcess(string bicep)
    {
        var sb = new StringBuilder();

        // Prepend the Radius extension directive
        sb.AppendLine("extension radius");
        sb.AppendLine();

        // Process each line, injecting recipe and connection blocks where needed
        var lines = bicep.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            sb.AppendLine(line.TrimEnd('\r'));

            // After the environment construct's compute properties, inject recipe config
            if (IsEnvironmentRecipeInjectionPoint(line, lines, i))
            {
                InjectRecipeConfig(sb);
            }

            // After the application construct reference, inject environment reference as Bicep expression
            if (IsApplicationEnvironmentInjectionPoint(line))
            {
                // The SDK emits the environment property as a string value.
                // We need to replace the quoted string with a Bicep resource reference.
                // This is handled in FixResourceReferences below.
            }

            // After container image property, inject connections block
            if (IsContainerConnectionInjectionPoint(line, lines, i))
            {
                InjectConnectionsBlock(sb, lines, i);
            }
        }

        var result = sb.ToString();

        // Fix resource references: replace string values with Bicep .id expressions
        result = FixResourceReferences(result);

        return result;
    }

    private bool IsEnvironmentRecipeInjectionPoint(string line, string[] lines, int index)
    {
        // Look for the closing of the compute block in the environment resource
        var trimmed = line.TrimEnd('\r').Trim();
        if (trimmed != "}")
        {
            return false;
        }

        // Walk backward to see if we're inside the environment resource's properties block
        // by checking for the compute namespace line
        for (var j = index - 1; j >= Math.Max(0, index - 5); j--)
        {
            if (lines[j].Contains("namespace:", StringComparison.OrdinalIgnoreCase) &&
                lines[j].Contains(_radiusEnvironment.Namespace, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void InjectRecipeConfig(StringBuilder sb)
    {
        if (_portableResources.Count == 0)
        {
            return;
        }

        // Group recipes by resource type
        var recipesByType = new Dictionary<string, List<(string Name, string TemplatePath)>>();

        foreach (var portable in _portableResources)
        {
            var resourceType = portable.ResourceType.ToString();
            var mapping = ResourceTypeMapper.GetRadiusType(
                _model.Resources.First(r => BicepIdentifier.Sanitize(ResolveToParent(r).Name) == portable.BicepIdentifier));

            if (mapping.DefaultRecipe is null)
            {
                continue;
            }

            if (!recipesByType.TryGetValue(resourceType, out var recipeList))
            {
                recipeList = [];
                recipesByType[resourceType] = recipeList;
            }

            var recipeName = portable.RecipeName ?? "default";
            var templatePath = mapping.DefaultRecipe;

            // Check for custom recipe override
            var originalResource = _model.Resources.FirstOrDefault(r =>
                BicepIdentifier.Sanitize(ResolveToParent(r).Name) == portable.BicepIdentifier);
            if (originalResource is not null)
            {
                var customization = GetCustomization(originalResource);
                if (customization?.Recipe is not null)
                {
                    recipeName = customization.Recipe.Name;
                    templatePath = customization.Recipe.TemplatePath;
                }
            }

            recipeList.Add((recipeName, templatePath));
        }

        if (recipesByType.Count == 0)
        {
            return;
        }

        sb.AppendLine("    recipeConfig: {");
        foreach (var (type, recipes) in recipesByType)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"      '{type}': {{");
            foreach (var (name, templatePath) in recipes)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"        {name}: {{");
                sb.AppendLine("          templateKind: 'bicep'");
                sb.AppendLine(CultureInfo.InvariantCulture, $"          templatePath: '{templatePath}'");
                sb.AppendLine("        }");
            }
            sb.AppendLine("      }");
        }
        sb.AppendLine("    }");
    }

    private bool IsApplicationEnvironmentInjectionPoint(string line)
    {
        return line.Contains("environment:", StringComparison.OrdinalIgnoreCase) &&
               line.Contains(_radiusEnvironment.Name, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContainerConnectionInjectionPoint(string line, string[] lines, int index)
    {
        // Check if we just emitted the container image line and the next line closes properties
        var trimmed = line.TrimEnd('\r').Trim();
        if (!trimmed.StartsWith("image:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Verify we're inside a container resource
        for (var j = index - 1; j >= Math.Max(0, index - 10); j--)
        {
            if (lines[j].Contains("Applications.Core/containers", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void InjectConnectionsBlock(StringBuilder sb, string[] lines, int currentIndex)
    {
        // Find which container construct this belongs to
        var container = FindContainerForLine(lines, currentIndex);
        if (container is null || container.Connections.Count == 0)
        {
            return;
        }

        // Close the container block and add connections
        sb.AppendLine("    }");
        sb.AppendLine("    connections: {");
        foreach (var (connName, portableId) in container.Connections)
        {
            // Quote connection names with special characters
            var quotedName = connName.Contains('-') ? $"'{connName}'" : connName;
            sb.AppendLine(CultureInfo.InvariantCulture, $"      {quotedName}: {{");
            sb.AppendLine(CultureInfo.InvariantCulture, $"        source: {portableId}.id");
            sb.AppendLine("      }");
        }
        sb.AppendLine("    }");
    }

    private RadiusContainerConstruct? FindContainerForLine(string[] lines, int lineIndex)
    {
        // Walk backward to find the resource name
        for (var j = lineIndex - 1; j >= Math.Max(0, lineIndex - 10); j--)
        {
            var trimmed = lines[j].TrimEnd('\r').Trim();
            if (trimmed.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
            {
                var name = trimmed.Split('\'').ElementAtOrDefault(1);
                if (name is not null)
                {
                    return _containers.FirstOrDefault(c => c.Name.Value == name);
                }
            }
        }

        return null;
    }

    private string FixResourceReferences(string bicep)
    {
        var envId = _environmentConstruct.BicepIdentifier;
        var appId = _applicationConstruct.BicepIdentifier;

        // Replace string-based environment references with Bicep .id expressions
        // The SDK emits: environment: 'env-name'
        // We need: environment: <envId>.id
        bicep = bicep.Replace($"environment: '{_radiusEnvironment.Name}'", $"environment: {envId}.id");
        bicep = bicep.Replace($"environment: '{_radiusEnvironment.Name + "-app"}'", $"environment: {envId}.id");

        // Replace application references
        bicep = bicep.Replace($"application: '{_radiusEnvironment.Name}-app'", $"application: {appId}.id");

        return bicep;
    }

    private IEnumerable<IResource> GetTargetedResources()
    {
        foreach (var resource in _model.Resources)
        {
            if (resource is RadiusEnvironmentResource or RadiusDeploymentResource)
            {
                continue;
            }

            var deploymentAnnotation = resource.Annotations
                .OfType<DeploymentTargetAnnotation>()
                .FirstOrDefault(a => a.ComputeEnvironment == _radiusEnvironment);

            if (deploymentAnnotation is not null)
            {
                yield return resource;
            }
        }
    }

    private static IResource ResolveToParent(IResource resource)
    {
        if (resource is IResourceWithParent child && child.Parent is not RadiusEnvironmentResource)
        {
            return child.Parent;
        }

        return resource;
    }

    private static RadiusResourceCustomization? GetCustomization(IResource resource)
    {
        var annotation = resource.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .FirstOrDefault();

        return annotation?.Customization;
    }

    private static void ApplyCustomization(RadiusPortableResourceConstruct construct, RadiusResourceCustomization customization)
    {
        if (customization.Provisioning == RadiusResourceProvisioning.Manual)
        {
            construct.ResourceProvisioning = "manual";

            if (customization.Host is not null)
            {
                construct.Host = customization.Host;
            }

            if (customization.Port is not null)
            {
                construct.Port = customization.Port.Value;
            }
        }

        if (customization.Recipe is not null)
        {
            construct.RecipeName = customization.Recipe.Name;
        }
    }

    private static string GetContainerImage(IResource resource)
    {
        // Try to get the container image from annotations
        if (resource.TryGetContainerImageName(out var imageName) && imageName is not null)
        {
            return imageName;
        }

        // Fallback to a placeholder
        return $"{resource.Name}:latest";
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1305 // StringBuilder interpolation in Bicep generation has no culture-sensitive values

using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Builds Radius infrastructure AST from the Aspire app model and generates Bicep output.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    private readonly DistributedApplicationModel _model;
    private readonly RadiusEnvironmentResource _environment;
    private readonly ILogger _logger;
    private readonly Action<RadiusInfrastructureOptions>? _configureCallback;
    private readonly bool _isFirstEnvironment;

    // AST constructs
    private RadiusEnvironmentConstruct _envConstruct = null!;
    private RadiusApplicationConstruct _appConstruct = null!;
    private readonly List<RadiusPortableResourceConstruct> _portableResources = [];
    private readonly List<RadiusContainerConstruct> _containerResources = [];

    // Lookup: aspire resource name → portable resource bicep identifier
    private readonly Dictionary<string, string> _portableIdentifiers = new(StringComparer.OrdinalIgnoreCase);

    public RadiusInfrastructureBuilder(
        DistributedApplicationModel model,
        RadiusEnvironmentResource environment,
        ILogger logger,
        Action<RadiusInfrastructureOptions>? configureCallback = null,
        bool isFirstEnvironment = true)
    {
        _model = model;
        _environment = environment;
        _logger = logger;
        _configureCallback = configureCallback;
        _isFirstEnvironment = isFirstEnvironment;
    }

    /// <summary>
    /// Builds the Bicep output for this Radius environment.
    /// </summary>
    public string Build()
    {
        // 1. Create environment and application constructs
        var envId = BicepIdentifier.Sanitize(_environment.Name);
        _envConstruct = new RadiusEnvironmentConstruct
        {
            BicepIdentifier = envId,
            Name = $"{_environment.Name}-env",
            Namespace = _environment.Namespace
        };

        _appConstruct = new RadiusApplicationConstruct
        {
            BicepIdentifier = $"{envId}_app",
            Name = $"{_environment.Name}-app",
            EnvironmentBicepIdentifier = _envConstruct.BicepIdentifier
        };

        // 2. Classify and build resource constructs
        var classifiedResources = ClassifyResources();
        BuildPortableResourceConstructs(classifiedResources.PortableResources);
        BuildContainerConstructs(classifiedResources.ComputeResources);

        // 3. Register default recipes on environment
        RegisterDefaultRecipes();

        // 4. Invoke user's ConfigureRadiusInfrastructure callback (last-write-wins)
        if (_configureCallback is not null)
        {
            var options = new RadiusInfrastructureOptions
            {
                Environments = [_envConstruct],
                Applications = [_appConstruct],
                PortableResources = [.. _portableResources],
                Containers = [.. _containerResources]
            };
            _configureCallback(options);

            // Apply mutations back
            if (options.Environments.Count > 0)
            {
                _envConstruct = options.Environments[0];
            }
            if (options.Applications.Count > 0)
            {
                _appConstruct = options.Applications[0];
            }
            _portableResources.Clear();
            _portableResources.AddRange(options.PortableResources);
            _containerResources.Clear();
            _containerResources.AddRange(options.Containers);
        }

        // 5. Validate recipe name consistency (FR-026/D7)
        ValidateRecipeNames();

        // 6. Generate Bicep output
        var bicep = GenerateBicep();

        return bicep;
    }

    // ---- T024: ClassifyResources ----

    private record ClassifiedResources(List<IResource> PortableResources, List<IResource> ComputeResources);

    private ClassifiedResources ClassifyResources()
    {
        var portable = new List<IResource>();
        var compute = new List<IResource>();

        foreach (var resource in _model.Resources)
        {
            // Skip the Radius environment resources themselves
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            // Skip resources targeted to a different compute environment (fix C2)
            var resourceComputeEnvironment = resource.GetComputeEnvironment();
            if (resourceComputeEnvironment is not null && resourceComputeEnvironment != _environment)
            {
                continue;
            }

            // Untargeted resources default to first environment only
            if (resourceComputeEnvironment is null && !_isFirstEnvironment)
            {
                continue;
            }

            if (resource.IsExcludedFromPublish())
            {
                continue;
            }

            // Check for manual provisioning annotation — always portable resource path
            if (resource.TryGetLastAnnotation<RadiusResourceCustomizationAnnotation>(out var custAnnotation) &&
                custAnnotation.Customization.Provisioning == RadiusResourceProvisioning.Manual)
            {
                portable.Add(resource);
                continue;
            }

            // Check if this is a portable resource type (fix C1)
            if (ResourceTypeMapper.IsPortableResource(resource))
            {
                portable.Add(resource);
            }
            else if (ResourceTypeMapper.IsComputeResource(resource))
            {
                compute.Add(resource);
            }
            // else: skip non-portable, non-compute resources (e.g., parameters, connection strings)
        }

        return new ClassifiedResources(portable, compute);
    }

    // ---- T025: BuildPortableResourceConstructs ----

    private void BuildPortableResourceConstructs(List<IResource> resources)
    {
        foreach (var resource in resources)
        {
            var mapping = ResourceTypeMapper.GetMapping(resource);
            var bicepId = BicepIdentifier.Sanitize(resource.Name);

            var construct = new RadiusPortableResourceConstruct
            {
                BicepIdentifier = bicepId,
                Name = resource.Name,
                ResourceType = mapping.RadiusType,
                ApiVersion = mapping.ApiVersion,
                ApplicationBicepIdentifier = _appConstruct.BicepIdentifier,
                EnvironmentBicepIdentifier = _envConstruct.BicepIdentifier
            };

            // Apply customizations (T026)
            if (resource.TryGetLastAnnotation<RadiusResourceCustomizationAnnotation>(out var annotation))
            {
                ApplyCustomization(construct, annotation.Customization, resource.Name);
            }

            _portableResources.Add(construct);
            _portableIdentifiers[resource.Name] = bicepId;
        }
    }

    // ---- T026: ApplyCustomization ----

    private static void ApplyCustomization(RadiusPortableResourceConstruct construct, RadiusResourceCustomization customization, string resourceName)
    {
        // Validate recipe + manual mutual exclusivity (FR-027/D6)
        if (customization.Recipe is not null && customization.Provisioning == RadiusResourceProvisioning.Manual)
        {
            throw new InvalidOperationException(
                $"Resource '{resourceName}' specifies both a custom recipe ('{customization.Recipe.Name}') " +
                $"and manual provisioning. These are mutually exclusive in Radius — manual provisioning " +
                $"bypasses recipes entirely. Remove either the Recipe or set Provisioning to Automatic.");
        }

        // Apply recipe (D3/D4 fix)
        if (customization.Recipe is not null)
        {
            construct.RecipeName = customization.Recipe.Name;
            construct.RecipeParameters = customization.Recipe.Parameters;
        }

        // Apply manual provisioning
        if (customization.Provisioning == RadiusResourceProvisioning.Manual)
        {
            construct.ResourceProvisioning = RadiusResourceProvisioning.Manual;
            construct.Host = customization.Host;
            construct.Port = customization.Port;
        }
    }

    // ---- BuildContainerConstructs ----

    private void BuildContainerConstructs(List<IResource> resources)
    {
        foreach (var resource in resources)
        {
            var bicepId = BicepIdentifier.Sanitize(resource.Name);
            var image = GetContainerImage(resource);

            var construct = new RadiusContainerConstruct
            {
                BicepIdentifier = bicepId,
                Name = resource.Name,
                ApplicationBicepIdentifier = _appConstruct.BicepIdentifier,
                Image = image
            };

            // Build connections from resource references
            foreach (var relationship in resource.Annotations.OfType<ResourceRelationshipAnnotation>())
            {
                var referencedResource = relationship.Resource;
                var resolvedName = ResolveToPortableResource(referencedResource);
                if (resolvedName is not null && _portableIdentifiers.TryGetValue(resolvedName, out var portableBicepId))
                {
                    var connKey = BicepIdentifier.QuotePropertyName(referencedResource.Name);
                    construct.Connections[connKey] = portableBicepId;
                }
            }

            _containerResources.Add(construct);
        }
    }

    /// <summary>
    /// Resolves a referenced resource to its portable resource name.
    /// If the resource implements IResourceWithParent, resolves to the parent.
    /// </summary>
    private string? ResolveToPortableResource(IResource resource)
    {
        // Direct match
        if (_portableIdentifiers.TryGetValue(resource.Name, out _))
        {
            return resource.Name;
        }

        // Resolve child to parent (e.g., SqlServerDatabaseResource → SqlServerServerResource)
        if (resource is IResourceWithParent childResource)
        {
            var parent = childResource.Parent;
            if (_portableIdentifiers.TryGetValue(parent.Name, out _))
            {
                return parent.Name;
            }
        }

        return null;
    }

    private static string GetContainerImage(IResource resource)
    {
        if (resource.TryGetContainerImageName(out var imageName))
        {
            return imageName;
        }
        return $"{resource.Name}:latest";
    }

    // ---- RegisterDefaultRecipes ----

    private void RegisterDefaultRecipes()
    {
        foreach (var portable in _portableResources)
        {
            var mapping = ResourceTypeMapper.GetMapping(
                _model.Resources.First(r => r.Name == portable.Name));

            if (string.IsNullOrEmpty(mapping.DefaultTemplatePath))
            {
                continue;
            }

            // Register default recipe
            if (!_envConstruct.RecipeRegistrations.TryGetValue(portable.ResourceType, out var registrations))
            {
                registrations = [];
                _envConstruct.RecipeRegistrations[portable.ResourceType] = registrations;
            }

            var recipeName = portable.RecipeName ?? "default";
            var templatePath = portable.RecipeName is not null && portable.RecipeName != "default"
                ? GetCustomTemplatePath(portable)
                : mapping.DefaultTemplatePath;

            // Check if this recipe is already registered
            if (!registrations.Any(r => r.RecipeName == recipeName))
            {
                registrations.Add((recipeName, templatePath));
            }
        }
    }

    private string GetCustomTemplatePath(RadiusPortableResourceConstruct construct)
    {
        // Look up the customization annotation for the custom template path
        var resource = _model.Resources.FirstOrDefault(r => r.Name == construct.Name);
        if (resource is not null &&
            resource.TryGetLastAnnotation<RadiusResourceCustomizationAnnotation>(out var annotation) &&
            annotation.Customization.Recipe is not null)
        {
            return annotation.Customization.Recipe.TemplatePath;
        }
        return "";
    }

    // ---- T032: ValidateRecipeNames ----

    private void ValidateRecipeNames()
    {
        foreach (var portable in _portableResources)
        {
            if (portable.RecipeName is null || portable.RecipeName == "default")
            {
                continue;
            }

            // Check if the recipe name is registered
            var isRegistered = _envConstruct.RecipeRegistrations
                .SelectMany(kv => kv.Value)
                .Any(r => r.RecipeName == portable.RecipeName);

            if (!isRegistered)
            {
                _logger.LogWarning(
                    "Resource '{ResourceName}' references recipe '{RecipeName}' which is not registered " +
                    "in the environment's recipeConfig. The recipe may need to be registered externally " +
                    "or added post-generation.",
                    portable.Name, portable.RecipeName);
            }
        }
    }

    // ---- T027-T031: Bicep Generation (PostProcess equivalent) ----

    private string GenerateBicep()
    {
        var sb = new StringBuilder();

        // T028: Extension directive
        InjectExtensionDirective(sb);

        sb.AppendLine();

        // Environment resource
        EmitEnvironmentResource(sb);
        sb.AppendLine();

        // Application resource
        EmitApplicationResource(sb);
        sb.AppendLine();

        // Portable resources
        foreach (var portable in _portableResources)
        {
            EmitPortableResource(sb, portable);
            sb.AppendLine();
        }

        // Container resources
        foreach (var container in _containerResources)
        {
            EmitContainerResource(sb, container);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    // T028
    private static void InjectExtensionDirective(StringBuilder sb)
    {
        sb.AppendLine("extension radius");
    }

    // T029
    private void EmitEnvironmentResource(StringBuilder sb)
    {
        sb.AppendLine($"resource {_envConstruct.BicepIdentifier} '{_envConstruct.ResourceType}@{_envConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{_envConstruct.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    compute: {");
        sb.AppendLine($"      kind: '{_envConstruct.ComputeKind}'");
        sb.AppendLine($"      namespace: '{_envConstruct.Namespace}'");
        sb.AppendLine("    }");

        // Inject recipeConfig
        if (_envConstruct.RecipeRegistrations.Count > 0)
        {
            InjectRecipeConfig(sb);
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    // T029
    private void InjectRecipeConfig(StringBuilder sb)
    {
        sb.AppendLine("    recipes: {");

        foreach (var (resourceType, registrations) in _envConstruct.RecipeRegistrations)
        {
            sb.AppendLine($"      '{resourceType}': {{");
            foreach (var (recipeName, templatePath) in registrations)
            {
                sb.AppendLine($"        {recipeName}: {{");
                sb.AppendLine($"          templateKind: 'bicep'");
                sb.AppendLine($"          templatePath: '{templatePath}'");
                sb.AppendLine("        }");
            }
            sb.AppendLine("      }");
        }

        sb.AppendLine("    }");
    }

    private void EmitApplicationResource(StringBuilder sb)
    {
        sb.AppendLine($"resource {_appConstruct.BicepIdentifier} '{_appConstruct.ResourceType}@{_appConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{_appConstruct.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    environment: {_appConstruct.EnvironmentBicepIdentifier}.id");
        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    // T030
    private static void EmitPortableResource(StringBuilder sb, RadiusPortableResourceConstruct construct)
    {
        sb.AppendLine($"resource {construct.BicepIdentifier} '{construct.ResourceType}@{construct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{construct.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    application: {construct.ApplicationBicepIdentifier}.id");
        sb.AppendLine($"    environment: {construct.EnvironmentBicepIdentifier}.id");

        // Manual provisioning
        if (construct.ResourceProvisioning == RadiusResourceProvisioning.Manual)
        {
            sb.AppendLine("    resourceProvisioning: 'manual'");
            if (construct.Host is not null)
            {
                sb.AppendLine($"    host: '{construct.Host}'");
            }
            if (construct.Port is not null)
            {
                sb.AppendLine($"    port: {construct.Port}");
            }
        }

        // Recipe block (FR-007a/D3) — omit entirely when no custom recipe (FR-007e)
        if (construct.RecipeName is not null)
        {
            InjectRecipeBlock(sb, construct);
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    // T030
    private static void InjectRecipeBlock(StringBuilder sb, RadiusPortableResourceConstruct construct)
    {
        sb.AppendLine("    recipe: {");
        sb.AppendLine($"      name: '{construct.RecipeName}'");

        // Recipe parameters (FR-007b)
        if (construct.RecipeParameters is { Count: > 0 })
        {
            sb.AppendLine("      parameters: {");
            foreach (var (key, value) in construct.RecipeParameters)
            {
                sb.AppendLine($"        {key}: {SerializeBicepValue(value)}");
            }
            sb.AppendLine("      }");
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Serializes a value to its correct Bicep representation.
    /// String → 'value', int → 42, bool → true/false.
    /// </summary>
    internal static string SerializeBicepValue(object value)
    {
        return value switch
        {
            string s => $"'{s.Replace("'", "''")}'",
            int i => i.ToString(),
            long l => l.ToString(),
            bool b => b ? "true" : "false",
            double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => $"'{value.ToString()?.Replace("'", "''") ?? string.Empty}'"
        };
    }

    // T031
    private static void EmitContainerResource(StringBuilder sb, RadiusContainerConstruct construct)
    {
        sb.AppendLine($"resource {construct.BicepIdentifier} '{construct.ResourceType}@{construct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{construct.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    application: {construct.ApplicationBicepIdentifier}.id");
        sb.AppendLine("    container: {");
        sb.AppendLine($"      image: '{construct.Image}'");

        if (construct.ImagePullPolicy is not null)
        {
            sb.AppendLine($"      imagePullPolicy: '{construct.ImagePullPolicy}'");
        }

        sb.AppendLine("    }");

        // Connections block (FR-024/M1)
        if (construct.Connections.Count > 0)
        {
            InjectConnectionBlock(sb, construct);
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    // T031
    private static void InjectConnectionBlock(StringBuilder sb, RadiusContainerConstruct construct)
    {
        sb.AppendLine("    connections: {");
        foreach (var (connKey, portableId) in construct.Connections)
        {
            sb.AppendLine($"      {connKey}: {{");
            sb.AppendLine($"        source: {portableId}.id");
            sb.AppendLine("      }");
        }
        sb.AppendLine("    }");
    }
}

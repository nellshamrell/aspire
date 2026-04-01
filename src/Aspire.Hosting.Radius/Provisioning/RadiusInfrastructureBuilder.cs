// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002
#pragma warning disable CA1305 // StringBuilder.AppendLine with invariant format

using System.Text;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Orchestrator that builds Radius Bicep output from an Aspire distributed application model.
/// Walks the app model, creates construct instances, and compiles them into a single <c>app.bicep</c> file.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    /// <summary>
    /// The Radius API version used in all generated Bicep resources.
    /// </summary>
    public const string RadiusApiVersion = "2023-10-01-preview";

    private const string DefaultRecipeTemplateBase = "ghcr.io/radius-project/recipes/local-dev/";

    // Recipe template path suffix by portable resource type
    private static readonly Dictionary<string, string> s_recipeTemplateSuffixes = new(StringComparer.Ordinal)
    {
        ["Applications.Datastores/redisCaches"] = "rediscaches:latest",
        ["Applications.Datastores/sqlDatabases"] = "sqldatabases:latest",
        ["Applications.Datastores/mongoDatabases"] = "mongodatabases:latest",
        ["Applications.Messaging/rabbitMQQueues"] = "rabbitmqqueues:latest",
    };

    private readonly DistributedApplicationModel _model;
    private readonly ILogger _logger;

    // Constructs built from the model
    private readonly List<RadiusEnvironmentConstruct> _environments = [];
    private readonly List<RadiusApplicationConstruct> _applications = [];
    private readonly List<RadiusPortableResourceConstruct> _portableResources = [];
    private readonly List<RadiusContainerConstruct> _containers = [];

    // Maps resource name → Bicep identifier for cross-referencing
    private readonly Dictionary<string, string> _portableIdentifiers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _allIdentifiers = new(StringComparer.Ordinal);

    public RadiusInfrastructureBuilder(DistributedApplicationModel model, ILogger logger)
    {
        _model = model;
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of environment constructs for AST customization.
    /// </summary>
    public IReadOnlyList<RadiusEnvironmentConstruct> Environments => _environments;

    /// <summary>
    /// Gets the list of application constructs for AST customization.
    /// </summary>
    public IReadOnlyList<RadiusApplicationConstruct> Applications => _applications;

    /// <summary>
    /// Gets the list of portable resource constructs for AST customization.
    /// </summary>
    public IReadOnlyList<RadiusPortableResourceConstruct> PortableResources => _portableResources;

    /// <summary>
    /// Gets the list of container constructs for AST customization.
    /// </summary>
    public IReadOnlyList<RadiusContainerConstruct> Containers => _containers;

    /// <summary>
    /// Builds the Bicep AST from the distributed application model.
    /// </summary>
    public void Build()
    {
        var radiusEnvironments = _model.Resources
            .OfType<RadiusEnvironmentResource>()
            .ToArray();

        if (radiusEnvironments.Length == 0)
        {
            _logger.LogWarning("No RadiusEnvironmentResource found in the model. Skipping Bicep generation.");
            return;
        }

        // Build environment constructs
        foreach (var env in radiusEnvironments)
        {
            var envId = BicepIdentifier.Sanitize(env.Name);
            _allIdentifiers[env.Name] = envId;

            var envConstruct = new RadiusEnvironmentConstruct
            {
                BicepIdentifier = envId,
                Name = env.Name,
                ComputeNamespace = env.Namespace,
            };

            // Build application construct (one per environment)
            var appName = $"{env.Name}-app";
            var appId = BicepIdentifier.Sanitize(appName);
            _allIdentifiers[appName] = appId;

            var appConstruct = new RadiusApplicationConstruct
            {
                BicepIdentifier = appId,
                Name = appName,
                EnvironmentIdentifier = envId,
            };

            // Classify resources
            ClassifyResources(envConstruct, appConstruct);

            _environments.Add(envConstruct);
            _applications.Add(appConstruct);
        }
    }

    /// <summary>
    /// Compiles the constructs into a Bicep string.
    /// </summary>
    /// <returns>The compiled Bicep content.</returns>
    public string Compile()
    {
        var sb = new StringBuilder();

        // 1. Extension directive
        sb.AppendLine("extension radius");
        sb.AppendLine();

        // 2. Environment blocks
        foreach (var env in _environments)
        {
            WriteEnvironmentBlock(sb, env);
            sb.AppendLine();
        }

        // 3. Application blocks
        foreach (var app in _applications)
        {
            WriteApplicationBlock(sb, app);
            sb.AppendLine();
        }

        // 4. Portable resources
        foreach (var portable in _portableResources)
        {
            WritePortableResourceBlock(sb, portable);
            sb.AppendLine();
        }

        // 5. Container workloads
        foreach (var container in _containers)
        {
            WriteContainerBlock(sb, container);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private void ClassifyResources(
        RadiusEnvironmentConstruct envConstruct,
        RadiusApplicationConstruct appConstruct)
    {
        foreach (var resource in _model.Resources)
        {
            // Skip the environment itself and dashboard resources
            if (resource is RadiusEnvironmentResource || resource is RadiusDashboardResource)
            {
                continue;
            }

            // Skip child resources (e.g., SqlServerDatabaseResource) — only process parents
            if (resource is IResourceWithParent)
            {
                continue;
            }

            var mapping = ResourceTypeMapper.GetRadiusType(resource, _logger);

            if (mapping.Type == "Applications.Core/containers")
            {
                // Workload container (ContainerResource, ProjectResource, or fallback)
                ClassifyAsContainer(resource, appConstruct.BicepIdentifier);
            }
            else
            {
                // Portable resource
                ClassifyAsPortableResource(resource, mapping, envConstruct, appConstruct.BicepIdentifier);
            }
        }
    }

    private void ClassifyAsPortableResource(
        IResource resource,
        ResourceMapping mapping,
        RadiusEnvironmentConstruct envConstruct,
        string appIdentifier)
    {
        var resourceId = BicepIdentifier.Sanitize(resource.Name);
        _portableIdentifiers[resource.Name] = resourceId;
        _allIdentifiers[resource.Name] = resourceId;

        // Check for customization annotation
        var customization = resource.Annotations
            .OfType<RadiusResourceCustomizationAnnotation>()
            .FirstOrDefault()?.Customization;

        var isManual = customization?.Provisioning == RadiusResourceProvisioning.Manual
                       || mapping.IsManualProvisioning;

        if (isManual && customization is not null)
        {
            ValidateManualProvisioning(resource.Name, customization);
        }

        // Determine the recipe info
        string? customRecipeTemplatePath = null;
        if (customization?.Recipe is not null)
        {
            customRecipeTemplatePath = customization.Recipe.TemplatePath;
        }

        var construct = new RadiusPortableResourceConstruct
        {
            BicepIdentifier = resourceId,
            Name = resource.Name,
            ResourceType = mapping.Type,
            ApplicationIdentifier = appIdentifier,
            EnvironmentIdentifier = envConstruct.BicepIdentifier,
            IsManualProvisioning = isManual,
            ManualHost = isManual ? customization?.Host : null,
            ManualPort = isManual ? customization?.Port : null,
        };

        _portableResources.Add(construct);

        // Register recipe on the environment if this is an automatically provisioned portable resource
        if (!isManual && !envConstruct.Recipes.ContainsKey(mapping.Type))
        {
            string templatePath;
            if (customRecipeTemplatePath is not null)
            {
                templatePath = customRecipeTemplatePath;
            }
            else if (s_recipeTemplateSuffixes.TryGetValue(mapping.Type, out var suffix))
            {
                templatePath = DefaultRecipeTemplateBase + suffix;
            }
            else
            {
                // No known recipe for this type, skip recipe registration
                return;
            }

            var recipeName = customization?.Recipe?.Name ?? "default";

            envConstruct.Recipes[mapping.Type] = new RecipeRegistration
            {
                Name = recipeName,
                TemplatePath = templatePath,
            };
        }
    }

    private void ClassifyAsContainer(IResource resource, string appIdentifier)
    {
        var resourceId = BicepIdentifier.Sanitize(resource.Name);
        _allIdentifiers[resource.Name] = resourceId;

        // Get container image from annotations
        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();
        var image = imageAnnotation is not null
            ? FormatImageReference(imageAnnotation)
            : $"{resource.Name}:latest";

        var construct = new RadiusContainerConstruct
        {
            BicepIdentifier = resourceId,
            Name = resource.Name,
            ApplicationIdentifier = appIdentifier,
            Image = image,
        };

        // Build connections from resource relationships
        BuildConnections(resource, construct);

        _containers.Add(construct);
    }

    private void BuildConnections(IResource resource, RadiusContainerConstruct construct)
    {
        var relationships = resource.Annotations.OfType<ResourceRelationshipAnnotation>();

        foreach (var rel in relationships)
        {
            var referencedName = rel.Resource.Name;

            // Resolve the Bicep identifier for the referenced resource
            if (_portableIdentifiers.TryGetValue(referencedName, out var portableId))
            {
                construct.Connections[referencedName] = portableId;
            }
            else if (rel.Resource is IResourceWithParent childResource)
            {
                // T042a: When .WithReference() targets a child resource (e.g., SqlServerDatabaseResource),
                // resolve to the parent portable resource identifier.
                var parentName = ResolveParentResourceName(childResource);
                if (parentName is not null && _portableIdentifiers.TryGetValue(parentName, out var parentId))
                {
                    construct.Connections[referencedName] = parentId;
                }
                else
                {
                    _logger.LogDebug(
                        "Connection reference '{ReferenceName}' on container '{ContainerName}' could not be resolved to a portable resource.",
                        referencedName, resource.Name);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Connection reference '{ReferenceName}' on container '{ContainerName}' is not a portable resource; skipping.",
                    referencedName, resource.Name);
            }
        }
    }

    private static string? ResolveParentResourceName(IResource resource)
    {
        var current = resource;
        while (current is IResourceWithParent)
        {
            // Use reflection to get the Parent property, since IResourceWithParent<T> is generic
            var parentProp = current.GetType().GetProperty("Parent");
            if (parentProp?.GetValue(current) is IResource parent)
            {
                current = parent;
            }
            else
            {
                break;
            }
        }

        return current == resource ? null : current.Name;
    }

    private static void ValidateManualProvisioning(string resourceName, RadiusResourceCustomization customization)
    {
        if (string.IsNullOrWhiteSpace(customization.Host))
        {
            throw new InvalidOperationException(
                $"Resource '{resourceName}' has Provisioning = Manual but Host is not set. " +
                "Specify Host via PublishAsRadiusResource(cfg => cfg.Host = \"...\").");
        }

        if (customization.Port is null or <= 0)
        {
            throw new InvalidOperationException(
                $"Resource '{resourceName}' has Provisioning = Manual but Port is not set or invalid. " +
                "Specify Port via PublishAsRadiusResource(cfg => cfg.Port = ...).");
        }
    }

    private static string FormatImageReference(ContainerImageAnnotation annotation)
    {
        var image = annotation.Image;
        if (!string.IsNullOrEmpty(annotation.Registry))
        {
            image = $"{annotation.Registry}/{image}";
        }
        if (!string.IsNullOrEmpty(annotation.Tag))
        {
            image = $"{image}:{annotation.Tag}";
        }
        return image;
    }

    #region Bicep Writers

    private static void WriteEnvironmentBlock(StringBuilder sb, RadiusEnvironmentConstruct env)
    {
        sb.AppendLine($"resource {env.BicepIdentifier} '{RadiusEnvironmentConstruct.ResourceType}@{RadiusEnvironmentConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{env.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine("    compute: {");
        sb.AppendLine($"      kind: '{env.ComputeKind}'");
        sb.AppendLine($"      namespace: '{env.ComputeNamespace}'");
        sb.AppendLine("    }");

        if (env.Recipes.Count > 0)
        {
            sb.AppendLine("    recipes: {");
            foreach (var (portableType, recipe) in env.Recipes)
            {
                sb.AppendLine($"      '{portableType}': {{");
                sb.AppendLine($"        {recipe.Name}: {{");
                sb.AppendLine($"          templateKind: '{recipe.TemplateKind}'");
                sb.AppendLine($"          templatePath: '{recipe.TemplatePath}'");
                sb.AppendLine("        }");
                sb.AppendLine("      }");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    private static void WriteApplicationBlock(StringBuilder sb, RadiusApplicationConstruct app)
    {
        sb.AppendLine($"resource {app.BicepIdentifier} '{RadiusApplicationConstruct.ResourceType}@{RadiusApplicationConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{app.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    environment: {app.EnvironmentIdentifier}.id");
        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    private static void WritePortableResourceBlock(StringBuilder sb, RadiusPortableResourceConstruct portable)
    {
        sb.AppendLine($"resource {portable.BicepIdentifier} '{portable.ResourceType}@{RadiusPortableResourceConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{portable.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    application: {portable.ApplicationIdentifier}.id");
        sb.AppendLine($"    environment: {portable.EnvironmentIdentifier}.id");

        if (portable.IsManualProvisioning)
        {
            sb.AppendLine("    resourceProvisioning: 'manual'");
            if (portable.ManualHost is not null)
            {
                sb.AppendLine($"    host: '{portable.ManualHost}'");
            }
            if (portable.ManualPort is not null)
            {
                sb.AppendLine($"    port: {portable.ManualPort}");
            }
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    private static void WriteContainerBlock(StringBuilder sb, RadiusContainerConstruct container)
    {
        sb.AppendLine($"resource {container.BicepIdentifier} '{RadiusContainerConstruct.ResourceType}@{RadiusContainerConstruct.ApiVersion}' = {{");
        sb.AppendLine($"  name: '{container.Name}'");
        sb.AppendLine("  properties: {");
        sb.AppendLine($"    application: {container.ApplicationIdentifier}.id");
        sb.AppendLine("    container: {");
        sb.AppendLine($"      image: '{container.Image}'");
        sb.AppendLine("      imagePullPolicy: 'Never'");
        sb.AppendLine("    }");

        if (container.Connections.Count > 0)
        {
            sb.AppendLine("    connections: {");
            foreach (var (connName, portableId) in container.Connections)
            {
                sb.AppendLine($"      {connName}: {{");
                sb.AppendLine($"        source: {portableId}.id");
                sb.AppendLine("      }");
            }
            sb.AppendLine("    }");
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
    }

    #endregion
}

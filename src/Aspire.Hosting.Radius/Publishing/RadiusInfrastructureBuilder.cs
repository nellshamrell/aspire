// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Radius.ResourceMapping;
using Azure.Provisioning.Expressions;
using Microsoft.Extensions.Logging;

#pragma warning disable CS0618 // Legacy* constants are [Obsolete] but still used for fallback during UDT migration

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Builds an Azure.Provisioning Infrastructure AST from a <see cref="DistributedApplicationModel"/>
/// for a specific Radius environment. Generates typed <c>ProvisionableResource</c> constructs
/// (environments, applications, recipe packs, resource type instances, containers) that are
/// compiled to Bicep via <c>Infrastructure.Build().Compile()</c>.
/// </summary>
internal sealed class RadiusInfrastructureBuilder
{
    private readonly RadiusEnvironmentResource _environment;
    private readonly DistributedApplicationModel _model;
    private readonly ResourceTypeMapper _typeMapper;
    private readonly ILogger _logger;

    /// <summary>
    /// Default recipe template paths per resource type.
    /// </summary>
    private static readonly Dictionary<string, string> s_defaultRecipeTemplates = new(StringComparer.Ordinal)
    {
        [RadiusResourceTypes.RedisCaches] = "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest",
        [RadiusResourceTypes.SqlDatabases] = "ghcr.io/radius-project/recipes/local-dev/sqldatabases:latest",
        [RadiusResourceTypes.PostgreSqlDatabases] = "ghcr.io/radius-project/recipes/local-dev/postgresqldatabases:latest",
        [RadiusResourceTypes.MongoDatabases] = "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest",
        [RadiusResourceTypes.RabbitMQQueues] = "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest",
        // Legacy fallback types also get default recipes
        [RadiusResourceTypes.LegacyRedisCaches] = "ghcr.io/radius-project/recipes/local-dev/rediscaches:latest",
        [RadiusResourceTypes.LegacyMongoDatabases] = "ghcr.io/radius-project/recipes/local-dev/mongodatabases:latest",
        [RadiusResourceTypes.LegacyRabbitMQQueues] = "ghcr.io/radius-project/recipes/local-dev/rabbitmqqueues:latest",
    };

    internal RadiusInfrastructureBuilder(
        RadiusEnvironmentResource environment,
        DistributedApplicationModel model,
        ResourceTypeMapper typeMapper,
        ILogger logger)
    {
        _environment = environment;
        _model = model;
        _typeMapper = typeMapper;
        _logger = logger;
    }

    /// <summary>
    /// Builds the Bicep AST and populates a <see cref="RadiusInfrastructureOptions"/> with
    /// typed constructs. Runs <c>ConfigureRadiusInfrastructure</c> callbacks last (last-write-wins).
    /// </summary>
    internal RadiusInfrastructureOptions Build()
    {
        var options = new RadiusInfrastructureOptions();
        var envIdentifier = BicepPostProcessor.SanitizeIdentifier(_environment.Name);

        // Classify resources for this environment
        var (radiusResources, computeResources) = ClassifyResources();

        // 1. Recipe pack (created first so environment can reference its ID)
        var recipePackIdentifier = "recipepack";
        var recipeEntries = new Dictionary<string, RecipeEntry>(StringComparer.Ordinal);
        var resourceIdentifierMap = new Dictionary<string, string>(StringComparer.Ordinal);

        // Collect recipe entries from Radius resource type instances
        foreach (var resource in radiusResources)
        {
            var (resourceType, _) = ResolveResourceType(resource);
            var customization = GetCustomization(resource);
            AddRecipeEntry(recipeEntries, resourceType, customization);
        }

        var recipePackConstruct = CreateRecipePackConstruct(recipePackIdentifier, recipeEntries);
        options.RecipePacks.Add(recipePackConstruct);

        // 2. Environment resource (references recipe pack ID)
        var envConstruct = CreateEnvironmentConstruct(envIdentifier, recipePackConstruct);
        options.Environments.Add(envConstruct);

        // 3. Application resource (references environment ID)
        var appIdentifier = "app";
        var appConstruct = CreateApplicationConstruct(appIdentifier, envConstruct);
        options.Applications.Add(appConstruct);

        // 4. Resource type instances
        foreach (var resource in radiusResources)
        {
            var (resourceType, apiVersion) = ResolveResourceType(resource);
            var identifier = BicepPostProcessor.SanitizeIdentifier(resource.Name);
            resourceIdentifierMap[resource.Name] = identifier;

            var customization = GetCustomization(resource);

            var typeInstance = CreateResourceTypeConstruct(
                identifier, resource.Name, resourceType, apiVersion,
                appConstruct, envConstruct, customization);
            options.ResourceTypeInstances.Add(typeInstance);
        }

        // 5. Container workloads
        foreach (var resource in computeResources)
        {
            var identifier = BicepPostProcessor.SanitizeIdentifier(resource.Name);
            var image = GetContainerImage(resource);
            var connections = GetConnectionIdentifiers(resource, radiusResources, resourceIdentifierMap);

            // Warn if image looks like a placeholder or uses :latest without a registry
            WarnIfImageMayNotPull(resource.Name, image);

            var containerConstruct = CreateContainerConstruct(
                identifier, resource.Name, image, appConstruct, connections);
            options.Containers.Add(containerConstruct);
        }

        // 6. Run ConfigureRadiusInfrastructure callbacks (last-write-wins)
        RunConfigureCallbacks(options);

        return options;
    }

    /// <summary>
    /// Resolves the Radius resource type and API version for a resource, checking for
    /// custom type overrides from <see cref="RadiusResourceCustomization"/> before falling
    /// back to the <see cref="ResourceTypeMapper"/>.
    /// </summary>
    private (string ResourceType, string ApiVersion) ResolveResourceType(IResource resource)
    {
        var customization = GetCustomization(resource);
        if (customization?.RadiusType is not null)
        {
            var apiVersion = customization.RadiusApiVersion ?? RadiusResourceTypes.RadiusApiVersion;
            _logger.LogInformation(
                "Resource '{ResourceName}' using custom type override '{RadiusType}' (API {ApiVersion}).",
                resource.Name, customization.RadiusType, apiVersion);
            return (customization.RadiusType, apiVersion);
        }

        return _typeMapper.MapResource(resource);
    }

    private (List<IResource> radiusResources, List<IResource> computeResources) ClassifyResources()
    {
        var radiusTypes = new List<IResource>();
        var compute = new List<IResource>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var resource in _model.Resources)
        {
            // Skip the Radius environment itself
            if (resource is RadiusEnvironmentResource)
            {
                continue;
            }

            // Check deployment target: only include resources targeted to this environment
            // or resources with no explicit target (default to this environment)
            if (!IsTargetedToThisEnvironment(resource))
            {
                continue;
            }

            // Resolve child resources to parent
            var resolvedResource = ResolveToParent(resource);
            if (resolvedResource != resource)
            {
                // Child resources (e.g., SqlServerDatabaseResource) are represented
                // via their parent; skip the child itself
                continue;
            }

            // Avoid duplicates
            if (!seen.Add(resource.Name))
            {
                continue;
            }

            // Use ResourceTypeMapper to determine classification:
            // - Explicit container/project resources with Containers mapping → compute workloads
            // - Resources with a specific resource type mapping → resource type instances
            // - Unmapped resources (ParameterResource, etc.) → skip
            var (resourceType, _) = ResolveResourceType(resource);

            if (resource is ProjectResource ||
                (resource is ContainerResource && resourceType == RadiusResourceTypes.Containers))
            {
                compute.Add(resource);
            }
            else if (resourceType != RadiusResourceTypes.Containers)
            {
                radiusTypes.Add(resource);
            }
            // else: unmapped resource (e.g., ParameterResource) — skip
        }

        return (radiusTypes, compute);
    }

    private bool IsTargetedToThisEnvironment(IResource resource)
    {
        var deploymentTargets = resource.Annotations.OfType<DeploymentTargetAnnotation>().ToArray();

        // No deployment target: unscoped resource defaults to first (this) environment
        if (deploymentTargets.Length == 0)
        {
            return true;
        }

        return deploymentTargets.Any(dt => dt.ComputeEnvironment == _environment);
    }

    /// <summary>
    /// Resolves a child resource (e.g., SqlServerDatabaseResource) to its parent.
    /// Returns the resource itself if it has no parent.
    /// </summary>
    private static IResource ResolveToParent(IResource resource)
    {
        if (resource is IResourceWithParent childResource)
        {
            return childResource.Parent;
        }

        return resource;
    }

    private static RadiusResourceCustomization? GetCustomization(IResource resource)
    {
        var annotation = resource.Annotations.OfType<RadiusResourceCustomizationAnnotation>().LastOrDefault();
        return annotation?.Customization;
    }

    /// <summary>
    /// Builds a <c>.id</c> expression for a resource, e.g., <c>envIdentifier.id</c>.
    /// </summary>
    private static BicepExpression BuildIdExpression(Azure.Provisioning.Primitives.ProvisionableResource resource)
    {
        return new MemberExpression(new IdentifierExpression(resource.BicepIdentifier), "id");
    }

    private RadiusEnvironmentConstruct CreateEnvironmentConstruct(
        string identifier, RadiusRecipePackConstruct recipePackConstruct)
    {
        var construct = new RadiusEnvironmentConstruct(identifier);
        construct.EnvironmentName = _environment.Name;
        construct.RecipePacks.Add(BuildIdExpression(recipePackConstruct));
        return construct;
    }

    private static RadiusApplicationConstruct CreateApplicationConstruct(
        string identifier, RadiusEnvironmentConstruct envConstruct)
    {
        var construct = new RadiusApplicationConstruct(identifier);
        construct.ApplicationName = identifier;
        construct.EnvironmentId = BuildIdExpression(envConstruct);
        return construct;
    }

    private static RadiusResourceTypeConstruct CreateResourceTypeConstruct(
        string identifier, string resourceName, string resourceType, string apiVersion,
        RadiusApplicationConstruct appConstruct, RadiusEnvironmentConstruct envConstruct,
        RadiusResourceCustomization? customization)
    {
        var construct = new RadiusResourceTypeConstruct(identifier, resourceType, apiVersion);
        construct.ResourceName = resourceName;
        construct.ApplicationId = BuildIdExpression(appConstruct);
        construct.EnvironmentId = BuildIdExpression(envConstruct);

        if (customization is not null)
        {
            // Custom recipe
            if (customization.Recipe is not null)
            {
                construct.RecipeName = customization.Recipe.Name;

                if (customization.Recipe.Parameters.Count > 0)
                {
                    foreach (var (key, value) in customization.Recipe.Parameters)
                    {
                        construct.RecipeParameters[key] = BicepPostProcessor.ToBicepLiteral(value);
                    }
                }
            }

            // Manual provisioning
            if (customization.Provisioning == ResourceProvisioning.Manual)
            {
                construct.ResourceProvisioning = "manual";

                if (customization.ConnectionStringOverrides.TryGetValue("host", out var host))
                {
                    construct.Host = host;
                }

                if (customization.ConnectionStringOverrides.TryGetValue("port", out var portStr)
                    && int.TryParse(portStr, out var port))
                {
                    construct.Port = port;
                }
            }
        }

        return construct;
    }

    private void AddRecipeEntry(
        Dictionary<string, RecipeEntry> entries,
        string resourceType,
        RadiusResourceCustomization? customization)
    {
        // Don't add recipe entries for manually provisioned resources
        if (customization?.Provisioning == ResourceProvisioning.Manual)
        {
            return;
        }

        // Custom recipe with template path
        if (customization?.Recipe?.TemplatePath is not null)
        {
            entries[resourceType] = new RecipeEntry("bicep", customization.Recipe.TemplatePath);
            return;
        }

        // Default recipe template
        if (s_defaultRecipeTemplates.TryGetValue(resourceType, out var defaultTemplate))
        {
            // Don't overwrite a custom entry
            entries.TryAdd(resourceType, new RecipeEntry("bicep", defaultTemplate));
        }
        else
        {
            _logger.LogWarning(
                "No default recipe template found for resource type '{ResourceType}'. " +
                "Consider providing a custom recipe via PublishAsRadiusResource().",
                resourceType);
        }
    }

    private static RadiusRecipePackConstruct CreateRecipePackConstruct(
        string identifier, Dictionary<string, RecipeEntry> recipeEntries)
    {
        var construct = new RadiusRecipePackConstruct(identifier);
        construct.PackName = "default";

        foreach (var (type, entry) in recipeEntries)
        {
            var recipeEntry = new RecipeEntryConstruct();
            recipeEntry.TemplateKind = entry.TemplateKind;
            recipeEntry.TemplatePath = entry.TemplatePath;
            construct.Recipes[type] = recipeEntry;
        }

        return construct;
    }

    private static string GetContainerImage(IResource resource)
    {
        var imageAnnotation = resource.Annotations.OfType<ContainerImageAnnotation>().FirstOrDefault();

        if (imageAnnotation is not null)
        {
            var image = imageAnnotation.Image;
            if (!string.IsNullOrEmpty(imageAnnotation.Tag))
            {
                image = $"{image}:{imageAnnotation.Tag}";
            }

            if (!string.IsNullOrEmpty(imageAnnotation.Registry))
            {
                image = $"{imageAnnotation.Registry}/{image}";
            }

            return image;
        }

        // Fallback: use the resource name as a placeholder image
        return $"{resource.Name}:latest";
    }

    private static Dictionary<string, string> GetConnectionIdentifiers(
        IResource resource,
        List<IResource> radiusResources,
        Dictionary<string, string> resourceIdentifierMap)
    {
        var connections = new Dictionary<string, string>(StringComparer.Ordinal);

        // Find all ResourceRelationshipAnnotation with type "Reference"
        var references = resource.Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(r => r.Type == "Reference");

        foreach (var reference in references)
        {
            var referencedResource = reference.Resource;

            // Resolve child resources (e.g., SqlServerDatabaseResource) to parent
            if (referencedResource is IResourceWithParent childResource)
            {
                referencedResource = childResource.Parent;
            }

            // Only create connections for Radius resource type instances (non-compute)
            if (radiusResources.Any(p => p.Name == referencedResource.Name)
                && resourceIdentifierMap.TryGetValue(referencedResource.Name, out var identifier))
            {
                connections[referencedResource.Name] = identifier;
            }
        }

        return connections;
    }

    private static RadiusContainerConstruct CreateContainerConstruct(
        string identifier, string resourceName, string image,
        RadiusApplicationConstruct appConstruct, Dictionary<string, string> connections)
    {
        var construct = new RadiusContainerConstruct(identifier);
        construct.ContainerName = resourceName;
        construct.Image = image;
        construct.ApplicationId = BuildIdExpression(appConstruct);

        if (connections.Count > 0)
        {
            foreach (var (name, sourceIdentifier) in connections)
            {
                var connectionConstruct = new ConnectionConstruct();
                connectionConstruct.Source = new MemberExpression(new IdentifierExpression(sourceIdentifier), "id");
                construct.Connections[name] = connectionConstruct;
            }
        }

        return construct;
    }

    /// <summary>
    /// Warns when a container image may not pull correctly without <c>imagePullPolicy</c>.
    /// The container v2 schema removes <c>imagePullPolicy</c>, so users of kind clusters or
    /// local images need to ensure images are pre-loaded and use explicit tags.
    /// </summary>
    private void WarnIfImageMayNotPull(string resourceName, string image)
    {
        if (image.EndsWith(":latest", StringComparison.Ordinal) || !image.Contains(':'))
        {
            _logger.LogWarning(
                "Resource '{ResourceName}' uses image '{Image}' which may default to 'Always' pull policy " +
                "in Kubernetes. The Radius container v2 schema no longer supports imagePullPolicy. " +
                "For kind clusters, pre-load images with 'kind load docker-image' and use explicit tags.",
                resourceName, image);
        }

        if (!image.Contains('/'))
        {
            _logger.LogWarning(
                "Resource '{ResourceName}' uses image '{Image}' without a registry prefix. " +
                "Ensure the image is available in the target cluster (e.g., pre-loaded via 'kind load docker-image').",
                resourceName, image);
        }
    }

    private void RunConfigureCallbacks(RadiusInfrastructureOptions options)
    {
        var callbacks = _environment.Annotations
            .OfType<RadiusInfrastructureConfigureAnnotation>()
            .ToArray();

        foreach (var callback in callbacks)
        {
            callback.Configure(options);
        }
    }

    internal readonly record struct RecipeEntry(string TemplateKind, string TemplatePath);
}

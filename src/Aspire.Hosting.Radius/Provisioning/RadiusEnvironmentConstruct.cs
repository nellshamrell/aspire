// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/environments</c> resource as a provisionable construct
/// for AST-based Bicep generation.
/// </summary>
internal sealed class RadiusEnvironmentConstruct : ProvisionableResource
{
    internal const string ResourceTypeName = "Applications.Core/environments";

    private readonly Dictionary<string, RecipeRegistration> _recipes = new(StringComparer.Ordinal);
    private bool _recipesApplied;

    public RadiusEnvironmentConstruct(string bicepIdentifier)
        : base(bicepIdentifier, new Azure.Core.ResourceType(ResourceTypeName), ResourceTypeMapper.DefaultApiVersion)
    { }

    public BicepValue<string> Name
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }
    private BicepValue<string>? _name;

    public BicepValue<string> ComputeKind
    {
        get { Initialize(); return _computeKind!; }
        set { Initialize(); _computeKind!.Assign(value); }
    }
    private BicepValue<string>? _computeKind;

    public BicepValue<string> ComputeNamespace
    {
        get { Initialize(); return _computeNamespace!; }
        set { Initialize(); _computeNamespace!.Assign(value); }
    }
    private BicepValue<string>? _computeNamespace;

    /// <summary>
    /// Gets the recipe registrations keyed by Radius resource type (e.g., "Applications.Datastores/redisCaches").
    /// </summary>
    public IReadOnlyDictionary<string, RecipeRegistration> Recipes => _recipes;

    /// <summary>
    /// Adds a recipe registration for the given Radius resource type.
    /// </summary>
    public void AddRecipe(string radiusType, string recipeName, string templateKind, string templatePath)
    {
        _recipes[radiusType] = new RecipeRegistration(recipeName, templateKind, templatePath);
    }

    /// <summary>
    /// Removes a recipe registration for the given Radius resource type.
    /// </summary>
    public bool RemoveRecipe(string radiusType) => _recipes.Remove(radiusType);

    /// <summary>
    /// Materializes dynamic recipe registrations into the provisioning AST before compilation.
    /// </summary>
    internal void ApplyRecipes()
    {
        if (_recipesApplied)
        {
            return;
        }

        Initialize();

        foreach (var (radiusType, recipe) in _recipes)
        {
            DefineProperty<string>(
                $"Recipe_{radiusType}_{recipe.RecipeName}_TemplateKind",
                ["properties", "recipes", radiusType, recipe.RecipeName, "templateKind"])
                .Assign(recipe.TemplateKind);

            DefineProperty<string>(
                $"Recipe_{radiusType}_{recipe.RecipeName}_TemplatePath",
                ["properties", "recipes", radiusType, recipe.RecipeName, "templatePath"])
                .Assign(recipe.TemplatePath);
        }

        _recipesApplied = true;
    }

    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _computeKind = DefineProperty<string>(nameof(ComputeKind), ["properties", "compute", "kind"]);
        _computeNamespace = DefineProperty<string>(nameof(ComputeNamespace), ["properties", "compute", "namespace"]);
    }
}

/// <summary>
/// Represents a Radius recipe registration for an environment.
/// </summary>
internal sealed record RecipeRegistration(string RecipeName, string TemplateKind, string TemplatePath);

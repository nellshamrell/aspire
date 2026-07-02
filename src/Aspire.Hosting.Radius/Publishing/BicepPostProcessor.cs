// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS004 // Experimental: ConfigureRadiusInfrastructure escape-hatch construct types are consumed internally by the publisher.

using System.Text.RegularExpressions;
using Azure.Provisioning;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Utility methods for Bicep post-processing: identifier sanitization,
/// recipe reference validation, and <c>extension radius</c> injection.
/// </summary>
internal static partial class BicepPostProcessor
{
    /// <summary>
    /// Compiles <see cref="RadiusInfrastructureOptions"/> to Bicep using the
    /// Azure.Provisioning SDK pipeline (<c>Infrastructure.Build().Compile()</c>)
    /// and prepends the <c>extension radius</c> directive.
    /// </summary>
    internal static string CompileBicep(RadiusInfrastructureOptions options, string environmentName, ILogger logger)
    {
        // Validate recipe references before compiling
        ValidateRecipeReferences(options, logger);

        var infra = new RadiusResourceInfrastructure(environmentName);

        // Add all constructs in block order:
        // 1. Recipe packs (referenced by environments)
        // 2. UDT environments
        // 3. UDT applications
        // 4. Legacy environments (Applications.Core/environments, fallback types)
        // 5. Legacy applications (Applications.Core/applications)
        // 6. Resource type instances
        // 7. Containers
        foreach (var resource in options.RecipePacks)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.Environments)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.Applications)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.LegacyEnvironments)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.LegacyApplications)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.ResourceTypeInstances)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.Containers)
        {
            infra.Add(resource);
        }

        foreach (var resource in options.LegacyContainers)
        {
            infra.Add(resource);
        }

        // Applications.Core/secretStores declared via AddRadiusSecretStore / WithSecretStore.
        foreach (var resource in options.SecretStores)
        {
            infra.Add(resource);
        }

        // Bicep `param` declarations referenced by recipe parameters bound to an
        // Aspire ParameterResource. Declared once; secret-bound params are secure so
        // no value is written to the published artifact (FR-003a).
        foreach (var parameter in options.RecipeParameters.Values)
        {
            infra.Add(parameter);
        }

        var plan = infra.Build(new ProvisioningBuildOptions());
        var compiled = plan.Compile();

        // The SDK generates a single file named "{infraName}.bicep"
        var bicepContent = compiled.Values.First().ToString();

        // Prepend `extension radius` directive (not natively supported by the SDK)
        var withExtension = $"extension radius\n\n{bicepContent}";

        // Ensure every Radius.Core/recipePacks resource has a `properties` block.
        // Azure.Provisioning's BicepDictionary serializer omits empty dictionaries,
        // so a recipe pack with no entries renders as `name: 'default' }` only,
        // which fails Bicep BCP035 because the recipePacks UDT requires `properties`.
        return EnsureRecipePackProperties(withExtension);
    }

    /// <summary>
    /// Injects <c>properties: { recipes: {} }</c> into <c>Radius.Core/recipePacks</c>
    /// resource blocks that lack a <c>properties:</c> key. See <see cref="CompileBicep"/>
    /// for the underlying SDK behaviour this works around.
    /// </summary>
    internal static string EnsureRecipePackProperties(string bicep)
    {
        return RecipePackWithoutProperties().Replace(bicep, m =>
        {
            var indent = m.Groups["indent"].Value;
            var body = m.Value.TrimEnd();
            if (body.EndsWith('}'))
            {
                body = body[..^1].TrimEnd();
            }
            return $"{body}\n{indent}  properties: {{\n{indent}    recipes: {{}}\n{indent}  }}\n{indent}}}";
        });
    }

    [GeneratedRegex(
        @"(?<indent>[ \t]*)resource[ \t]+\w+[ \t]+'Radius\.Core/recipePacks@[^']+'[ \t]*=[ \t]*\{[^{}]*?\n[ \t]*\}",
        RegexOptions.Singleline)]
    private static partial Regex RecipePackWithoutProperties();

    /// <summary>
    /// Generates the companion bicepconfig.json content.
    /// </summary>
    internal static string RenderBicepConfig()
    {
        // The radius extension version is pinned (not `:latest`) so an upstream
        // tag move can't change the schema this AppHost emits against. See
        // RadiusBicepExtension for the version pin policy.
        // No aws extension is registered here — this integration does not emit
        // AWS resources, so listing it would pull a large extension package
        // for nothing and produce confusing "unknown resource" diagnostics if
        // future code accidentally referenced an AWS type.
        return $$"""
            {
                "experimentalFeaturesEnabled": {
                    "extensibility": true
                },
                "extensions": {
                    "radius": "{{RadiusBicepExtension.Reference}}"
                }
            }
            """;
    }

    /// <summary>
    /// Sanitizes a resource name into a valid Bicep identifier.
    /// </summary>
    internal static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "resource";
        }

        // Replace non-alphanumeric/underscore characters with underscores
        var sanitized = InvalidIdentifierChars().Replace(name, "_");

        // Names starting with a digit are prefixed with 'r'
        if (char.IsDigit(sanitized[0]))
        {
            sanitized = "r" + sanitized;
        }

        // "radius" collides with the `extension radius` directive
        if (string.Equals(sanitized, "radius", StringComparison.OrdinalIgnoreCase))
        {
            sanitized = "radiusenv";
        }

        return sanitized;
    }

    /// <summary>
    /// Converts a .NET value to a type compatible with <c>BicepValue</c> assignment.
    /// </summary>
    internal static object ToBicepLiteral(object value)
    {
        return value switch
        {
            string or int or long or bool or double or float or decimal => value,
            IDictionary<string, object> dictionary => ToBicepObject(dictionary),
            System.Collections.IEnumerable sequence => ToBicepArray(sequence),
            _ => throw new NotSupportedException(
                $"Bicep parameter type '{value.GetType().Name}' is not supported. " +
                $"Supported types: string, int, long, double, float, decimal, bool, " +
                $"arrays/enumerables, and string-keyed objects.")
        };
    }

    /// <summary>
    /// Recursively converts a string-keyed object to a Bicep object literal,
    /// preserving the Bicep type of each value (FR-003).
    /// </summary>
    internal static BicepDictionary<object> ToBicepObject(IDictionary<string, object> dictionary)
    {
        var result = new BicepDictionary<object>();
        var sink = (IDictionary<string, IBicepValue>)result;
        foreach (var (key, value) in dictionary)
        {
            sink[key] = ToBicepValue(value);
        }

        return result;
    }

    /// <summary>
    /// Recursively converts an enumerable to a Bicep array literal, preserving the
    /// Bicep type of each element (FR-003). Strings are handled as scalars before
    /// reaching this method.
    /// </summary>
    private static BicepList<object> ToBicepArray(System.Collections.IEnumerable sequence)
    {
        var result = new BicepList<object>();
        foreach (var element in sequence)
        {
            result.Add(ToBicepArrayElement(element));
        }

        return result;
    }

    /// <summary>
    /// Converts a single array element to a <see cref="BicepValue{T}"/>. Scalar elements
    /// are wrapped directly. Nested arrays/objects as array elements are not supported.
    /// </summary>
    private static BicepValue<object> ToBicepArrayElement(object? element)
    {
        if (element is null)
        {
            throw new NotSupportedException("Null recipe parameter array elements are not supported.");
        }

        var literal = ToBicepLiteral(element);
        if (literal is BicepDictionary<object> or BicepList<object>)
        {
            throw new NotSupportedException(
                "Nested arrays or objects as array elements are not supported in recipe parameters. " +
                "Use a string-keyed object whose values are arrays/objects instead.");
        }

        return new BicepValue<object>(literal);
    }

    /// <summary>
    /// Converts a single value to an <see cref="IBicepValue"/>, recursing into nested
    /// arrays and objects so their element/Bicep types are preserved. Nested collections
    /// are returned directly (they are <see cref="IBicepValue"/>); scalars are wrapped.
    /// </summary>
    internal static IBicepValue ToBicepValue(object? value)
    {
        if (value is null)
        {
            throw new NotSupportedException("Null recipe parameter values are not supported.");
        }

        // Pass through values that are already Bicep AST nodes (e.g. a `<resource>.id`
        // reference expression used by recipeConfig secret-store references).
        if (value is IBicepValue alreadyBicep)
        {
            return alreadyBicep;
        }

        if (value is Azure.Provisioning.Expressions.BicepExpression expression)
        {
            return new BicepValue<object>(expression);
        }

        return ToBicepLiteral(value) switch
        {
            BicepDictionary<object> nestedObject => nestedObject,
            BicepList<object> nestedArray => nestedArray,
            var scalar => new BicepValue<object>(scalar)
        };
    }

    private static void ValidateRecipeReferences(RadiusInfrastructureOptions options, ILogger logger)
    {
        // Collect all recipe type keys from both UDT recipe packs and legacy
        // environments (which carry recipes inline).
        var registeredTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pack in options.RecipePacks)
        {
            foreach (var key in pack.Recipes.Keys)
            {
                registeredTypes.Add(key);
            }
        }

        foreach (var legacyEnv in options.LegacyEnvironments)
        {
            foreach (var key in legacyEnv.Recipes.Keys)
            {
                registeredTypes.Add(key);
            }
        }

        // Check resource type instances for recipes referencing unregistered types
        foreach (var instance in options.ResourceTypeInstances)
        {
            if (!instance.RecipeName.IsEmpty && !registeredTypes.Contains(instance.RadiusType))
            {
                logger.LogWarning(
                    "Resource '{ResourceName}' references a recipe but resource type '{ResourceType}' is not registered in any recipe pack or legacy environment.",
                    instance.BicepIdentifier,
                    instance.RadiusType);
            }
        }
    }

    [GeneratedRegex("[^a-zA-Z0-9_]")]
    private static partial Regex InvalidIdentifierChars();
}

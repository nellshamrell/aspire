// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Utility class for building Bicep template content with proper formatting and syntax.
/// </summary>
internal sealed class BicepTemplateBuilder
{
    private readonly StringBuilder _sb = new();

    private void Line(string text) => _sb.AppendLine(text);
    private void Line() => _sb.AppendLine();
    private void LineF(FormattableString formattable) => _sb.AppendLine(formattable.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// Adds the Radius extension directive required by the Bicep deployment engine.
    /// This must be called before adding any resource blocks.
    /// </summary>
    public void AddExtensionDirective()
    {
        Line("extension radius");
        Line();
    }

    /// <summary>
    /// Adds the Radius environment resource block to the Bicep template.
    /// </summary>
    /// <param name="name">The environment resource name.</param>
    /// <param name="kubernetesNamespace">The Kubernetes namespace for the compute target.</param>
    /// <param name="recipes">Dictionary of resource type → recipe configuration.</param>
    public void AddEnvironmentResource(string name, string kubernetesNamespace, Dictionary<string, string>? recipes = null)
    {
        LineF($"resource {SanitizeName(name)} 'Applications.Core/environments@{ResourceTypeMapper.DefaultApiVersion}' = {{");
        LineF($"  name: '{name}'");
        Line("  properties: {");
        Line("    compute: {");
        Line("      kind: 'kubernetes'");
        LineF($"      namespace: '{kubernetesNamespace}'");
        Line("    }");

        if (recipes is not null && recipes.Count > 0)
        {
            Line("    recipes: {");
            foreach (var (type, recipeName) in recipes)
            {
                LineF($"      '{type}': {{");
                LineF($"        '{recipeName}': {{");
                Line("          templateKind: 'bicep'");
                var recipePathSuffix = type.Split('/').Last().ToLowerInvariant();
                LineF($"          templatePath: 'ghcr.io/radius-project/recipes/local-dev/{recipePathSuffix}:latest'");
                Line("        }");
                Line("      }");
            }
            Line("    }");
        }

        Line("  }");
        Line("}");
        Line();
    }

    /// <summary>
    /// Adds the Radius application resource block to the Bicep template.
    /// </summary>
    /// <param name="name">The application name.</param>
    /// <param name="environmentResourceName">The Bicep variable name of the environment resource to reference.</param>
    public void AddApplicationResource(string name, string environmentResourceName)
    {
        var sanitizedEnvName = SanitizeName(environmentResourceName);
        var sanitizedName = SanitizeName(name);

        LineF($"resource {sanitizedName} 'Applications.Core/applications@{ResourceTypeMapper.DefaultApiVersion}' = {{");
        LineF($"  name: '{name}'");
        Line("  properties: {");
        LineF($"    environment: {sanitizedEnvName}.id");
        Line("  }");
        Line("}");
        Line();
    }

    /// <summary>
    /// Adds a Radius portable resource block to the Bicep template.
    /// </summary>
    /// <param name="radiusType">The Radius resource type (e.g., Applications.Datastores/redisCaches).</param>
    /// <param name="name">The resource name.</param>
    /// <param name="applicationResourceName">The Bicep variable name of the application resource to reference.</param>
    /// <param name="environmentResourceName">The Bicep variable name of the environment resource to reference.</param>
    /// <param name="properties">Additional properties to include in the resource block.</param>
    public void AddPortableResource(
        string radiusType,
        string name,
        string applicationResourceName,
        string environmentResourceName,
        Dictionary<string, string>? properties = null)
    {
        var sanitizedName = SanitizeName(name);
        var sanitizedAppName = SanitizeName(applicationResourceName);
        var sanitizedEnvName = SanitizeName(environmentResourceName);

        LineF($"resource {sanitizedName} '{radiusType}@{ResourceTypeMapper.DefaultApiVersion}' = {{");
        LineF($"  name: '{name}'");
        Line("  properties: {");
        LineF($"    application: {sanitizedAppName}.id");
        LineF($"    environment: {sanitizedEnvName}.id");

        if (properties is not null)
        {
            foreach (var (key, value) in properties)
            {
                LineF($"    {key}: {value}");
            }
        }

        Line("  }");
        Line("}");
        Line();
    }

    /// <summary>
    /// Adds a manually-provisioned portable resource block (for resources without native Radius types).
    /// </summary>
    /// <param name="radiusType">The Radius resource type.</param>
    /// <param name="name">The resource name.</param>
    /// <param name="applicationResourceName">The Bicep variable name of the application resource.</param>
    /// <param name="environmentResourceName">The Bicep variable name of the environment resource.</param>
    /// <param name="host">The host address for manual provisioning.</param>
    /// <param name="port">The port for manual provisioning.</param>
    public void AddManuallyProvisionedResource(
        string radiusType,
        string name,
        string applicationResourceName,
        string environmentResourceName,
        string host,
        int port)
    {
        var sanitizedName = SanitizeName(name);
        var sanitizedAppName = SanitizeName(applicationResourceName);
        var sanitizedEnvName = SanitizeName(environmentResourceName);

        LineF($"resource {sanitizedName} '{radiusType}@{ResourceTypeMapper.DefaultApiVersion}' = {{");
        LineF($"  name: '{name}'");
        Line("  properties: {");
        LineF($"    application: {sanitizedAppName}.id");
        LineF($"    environment: {sanitizedEnvName}.id");
        Line("    resourceProvisioning: 'manual'");
        LineF($"    host: '{host}'");
        LineF($"    port: {port}");
        Line("  }");
        Line("}");
        Line();
    }

    /// <summary>
    /// Adds a workload container resource block to the Bicep template.
    /// </summary>
    /// <param name="name">The container resource name.</param>
    /// <param name="image">The container image reference.</param>
    /// <param name="applicationResourceName">The Bicep variable name of the application resource.</param>
    /// <param name="connections">Connections to other Radius resources (portable resource references).</param>
    /// <param name="environmentVariables">Environment variables for the container.</param>
    public void AddWorkloadResource(
        string name,
        string image,
        string applicationResourceName,
        Dictionary<string, string>? connections = null,
        Dictionary<string, string>? environmentVariables = null)
    {
        var sanitizedName = SanitizeName(name);
        var sanitizedAppName = SanitizeName(applicationResourceName);

        LineF($"resource {sanitizedName} 'Applications.Core/containers@{ResourceTypeMapper.DefaultApiVersion}' = {{");
        LineF($"  name: '{name}'");
        Line("  properties: {");
        LineF($"    application: {sanitizedAppName}.id");
        Line("    container: {");
        LineF($"      image: '{image}'");
        Line("      imagePullPolicy: 'Never'");

        if (environmentVariables is not null && environmentVariables.Count > 0)
        {
            Line("      env: {");
            foreach (var (key, value) in environmentVariables)
            {
                LineF($"        {key}: {value}");
            }
            Line("      }");
        }

        Line("    }");

        if (connections is not null && connections.Count > 0)
        {
            Line("    connections: {");
            foreach (var (connName, connSource) in connections)
            {
                LineF($"      {connName}: {{");
                LineF($"        source: {connSource}.id");
                Line("      }");
            }
            Line("    }");
        }

        Line("  }");
        Line("}");
        Line();
    }

    /// <summary>
    /// Renders the complete Bicep template as a string.
    /// </summary>
    /// <returns>The complete Bicep template content.</returns>
    public string Render()
    {
        return _sb.ToString();
    }

    /// <summary>
    /// Sanitizes a resource name to be a valid Bicep identifier.
    /// Removes hyphens, dots, and other invalid characters; ensures it starts with a letter.
    /// </summary>
    internal static string SanitizeName(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString();

        // Ensure it starts with a letter
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

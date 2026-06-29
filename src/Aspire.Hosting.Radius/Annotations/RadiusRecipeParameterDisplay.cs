// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting.Radius.Annotations;

/// <summary>
/// Produces display-friendly strings for recipe parameter values surfaced on the Radius
/// environment (FR-013). A value bound to an <see cref="IResourceBuilder{ParameterResource}"/>
/// or a <see cref="ParameterResource"/> is shown by its parameter name (never a resolved
/// value), so secret-marked parameters never leak; literal values are rendered directly.
/// </summary>
internal static class RadiusRecipeParameterDisplay
{
    /// <summary>Formats a single recipe parameter value for display.</summary>
    public static string FormatValue(object value)
    {
        switch (value)
        {
            case IResourceBuilder<ParameterResource> parameterBuilder:
                return $"{{{parameterBuilder.Resource.Name}}}";
            case ParameterResource parameter:
                return $"{{{parameter.Name}}}";
            case string s:
                return s;
            case bool or int or long or double or float or decimal:
                return value.ToString() ?? string.Empty;
            case IDictionary<string, object> dictionary:
                return "{ " + string.Join(", ", dictionary.Select(kv => $"{kv.Key}: {FormatValue(kv.Value)}")) + " }";
            case IEnumerable sequence:
                return "[" + string.Join(", ", sequence.Cast<object>().Select(FormatValue)) + "]";
            default:
                return value.ToString() ?? string.Empty;
        }
    }
}

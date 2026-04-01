// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Publishing;

/// <summary>
/// Describes the mapping from an Aspire resource type to a Radius resource type.
/// </summary>
public readonly struct ResourceMapping
{
    /// <summary>
    /// The Radius resource type (e.g., "Applications.Datastores/redisCaches").
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// The Radius API version for this resource type.
    /// </summary>
    public string ApiVersion { get; init; }

    /// <summary>
    /// The default recipe name for this resource type, if any.
    /// </summary>
    public string? DefaultRecipe { get; init; }

    /// <summary>
    /// Whether this mapping represents a manual provisioning fallback
    /// (no native Radius portable type available).
    /// </summary>
    public bool IsManualProvisioning { get; init; }

    /// <summary>
    /// Whether this mapping is a fallback for an unmapped type.
    /// </summary>
    public bool IsFallback { get; init; }
}

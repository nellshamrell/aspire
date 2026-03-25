// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Serialization;

namespace Aspire.Hosting.Radius.Preview;

/// <summary>
/// Metadata about the preview state. Written to status.json.
/// </summary>
internal sealed class PreviewStatus
{
    [JsonPropertyName("previewMode")]
    public bool PreviewMode { get; set; }

    [JsonPropertyName("applicationName")]
    public string ApplicationName { get; set; } = string.Empty;

    [JsonPropertyName("namespace")]
    public string Namespace { get; set; } = "default";

    [JsonPropertyName("resourceCount")]
    public int ResourceCount { get; set; }
}

/// <summary>
/// A list of preview applications matching the Radius ResourceList format.
/// Written to applications.json.
/// </summary>
internal sealed class PreviewApplicationList
{
    [JsonPropertyName("value")]
    public List<PreviewApplication> Value { get; set; } = [];
}

/// <summary>
/// Represents the Aspire application in Radius terms.
/// </summary>
internal sealed class PreviewApplication
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Applications.Core/applications";

    [JsonPropertyName("properties")]
    public PreviewApplicationProperties Properties { get; set; } = new();
}

/// <summary>
/// Properties of a preview application.
/// </summary>
internal sealed class PreviewApplicationProperties
{
    [JsonPropertyName("provisioningState")]
    public string ProvisioningState { get; set; } = "Preview";

    [JsonPropertyName("status")]
    public object Status { get; set; } = new { };
}

/// <summary>
/// The preview graph response matching the Radius ApplicationGraphResponse format.
/// Written to graph.json.
/// </summary>
internal sealed class PreviewGraphResponse
{
    [JsonPropertyName("resources")]
    public List<PreviewResource> Resources { get; set; } = [];
}

/// <summary>
/// A projected Radius resource derived from an Aspire resource.
/// </summary>
internal sealed class PreviewResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "radius";

    [JsonPropertyName("provisioningState")]
    public string ProvisioningState { get; set; } = "Preview";

    [JsonPropertyName("connections")]
    public List<PreviewConnection> Connections { get; set; } = [];
}

/// <summary>
/// A relationship between two preview resources.
/// </summary>
internal sealed class PreviewConnection
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("provider")]
    public string Provider { get; set; } = "radius";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = string.Empty;
}

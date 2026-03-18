// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Represents a progress event from <c>rad deploy --output json</c>.
/// </summary>
/// <remarks>
/// The Radius CLI emits JSON progress events when <c>--output json</c> is specified.
/// Each line of stdout is a JSON object describing a resource deployment event.
///
/// Example events:
/// <code>
/// {"status":"InProgress","resource":"cache","type":"Applications.Datastores/redisCaches","message":"Creating resource"}
/// {"status":"Succeeded","resource":"cache","type":"Applications.Datastores/redisCaches","message":"Resource created successfully"}
/// {"status":"Failed","resource":"db","type":"Applications.Datastores/sqlDatabases","message":"Recipe execution failed: timeout"}
/// </code>
///
/// Known status values:
/// - <c>InProgress</c>: Resource is being created or updated
/// - <c>Succeeded</c>: Resource creation/update completed successfully
/// - <c>Failed</c>: Resource creation/update failed
/// </remarks>
internal sealed class RadDeploymentProgress
{
    /// <summary>
    /// Gets or sets the deployment status (e.g., "InProgress", "Succeeded", "Failed").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>
    /// Gets or sets the name of the resource being deployed.
    /// </summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the Radius resource type (e.g., "Applications.Datastores/redisCaches").
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the human-readable progress message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Attempts to parse a JSON string as a deployment progress event.
    /// </summary>
    /// <param name="json">A single line of JSON output from <c>rad deploy</c>.</param>
    /// <returns>A parsed <see cref="RadDeploymentProgress"/> or <c>null</c> if parsing fails.</returns>
    public static RadDeploymentProgress? ParseProgressEvent(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            var progress = JsonSerializer.Deserialize<RadDeploymentProgress>(json, s_jsonOptions);

            // Only return if we got at least one meaningful field
            if (progress is not null && (progress.Status is not null || progress.Resource is not null || progress.Message is not null))
            {
                return progress;
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Formats this progress event for human-readable CLI output.
    /// </summary>
    /// <returns>A formatted string suitable for console display.</returns>
    public string ToDisplayString()
    {
        var statusIcon = Status switch
        {
            "Succeeded" => "✓",
            "Failed" => "✗",
            "InProgress" => "⟳",
            _ => "·"
        };

        var resourceDisplay = Resource ?? "unknown";
        var typeDisplay = Type is not null ? $" ({Type})" : "";
        var messageDisplay = Message ?? "";

        return $"  {statusIcon} {resourceDisplay}{typeDisplay}: {messageDisplay}";
    }
}

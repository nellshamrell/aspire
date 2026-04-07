// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Parses and formats <c>rad deploy --output json</c> progress events.
/// </summary>
/// <remarks>
/// <para>
/// The Radius CLI outputs JSON progress events when invoked with <c>--output json</c>.
/// Each line of stdout is a JSON object representing a progress event.
/// </para>
/// <para>
/// Example JSON events from <c>rad deploy app.bicep --output json</c>:
/// <code>
/// {"timestamp":"2024-01-15T10:00:00Z","type":"DeploymentStarted","message":"Starting deployment..."}
/// {"timestamp":"2024-01-15T10:00:01Z","type":"ResourceProvisioning","resource":"redis","message":"Provisioning redis..."}
/// {"timestamp":"2024-01-15T10:00:05Z","type":"ResourceReady","resource":"redis","message":"redis is ready"}
/// {"timestamp":"2024-01-15T10:00:10Z","type":"DeploymentCompleted","message":"Deployment completed successfully"}
/// </code>
/// </para>
/// </remarks>
internal static class RadDeploymentProgress
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Attempts to parse a line of <c>rad deploy</c> output as a progress event.
    /// </summary>
    /// <param name="line">A line of JSON output from <c>rad deploy</c>.</param>
    /// <returns>A parsed <see cref="RadProgressEvent"/> or <see langword="null"/> if parsing fails.</returns>
    public static RadProgressEvent? TryParseEvent(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RadProgressEvent>(line, s_jsonOptions);
        }
        catch (JsonException)
        {
            // Non-JSON output lines (e.g., plain text warnings) are treated as info messages
            return new RadProgressEvent
            {
                Type = RadProgressEventType.Info,
                Message = line
            };
        }
    }

    /// <summary>
    /// Formats a progress event for human-readable CLI output.
    /// </summary>
    /// <param name="progressEvent">The progress event to format.</param>
    /// <returns>A human-readable status string.</returns>
    public static string FormatEvent(RadProgressEvent progressEvent)
    {
        var prefix = progressEvent.Type switch
        {
            RadProgressEventType.DeploymentStarted => "🚀 ",
            RadProgressEventType.ResourceProvisioning => "⏳ ",
            RadProgressEventType.ResourceReady => "✅ ",
            RadProgressEventType.DeploymentCompleted => "🎉 ",
            RadProgressEventType.DeploymentFailed => "❌ ",
            RadProgressEventType.Info => "ℹ️  ",
            _ => "   "
        };

        if (!string.IsNullOrEmpty(progressEvent.Resource))
        {
            return $"{prefix}[{progressEvent.Resource}] {progressEvent.Message}";
        }

        return $"{prefix}{progressEvent.Message}";
    }
}

/// <summary>
/// Represents a progress event from <c>rad deploy --output json</c>.
/// </summary>
internal sealed class RadProgressEvent
{
    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public string? Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the event type.
    /// </summary>
    public RadProgressEventType Type { get; set; }

    /// <summary>
    /// Gets or sets the resource name, if applicable.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the human-readable message.
    /// </summary>
    public string? Message { get; set; }
}

/// <summary>
/// Known event types from <c>rad deploy --output json</c>.
/// </summary>
internal enum RadProgressEventType
{
    /// <summary>General informational message.</summary>
    Info,

    /// <summary>Deployment has started.</summary>
    DeploymentStarted,

    /// <summary>A resource is being provisioned.</summary>
    ResourceProvisioning,

    /// <summary>A resource is ready.</summary>
    ResourceReady,

    /// <summary>Deployment completed successfully.</summary>
    DeploymentCompleted,

    /// <summary>Deployment failed.</summary>
    DeploymentFailed
}

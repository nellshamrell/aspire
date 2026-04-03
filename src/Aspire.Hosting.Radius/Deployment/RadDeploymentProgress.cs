// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Parses and formats <c>rad deploy --output json</c> progress events for human-readable output.
/// </summary>
/// <remarks>
/// <para>
/// The Radius CLI <c>rad deploy --output json</c> emits newline-delimited JSON events to stdout.
/// Each event contains status updates for resources being deployed.
/// </para>
/// <para>
/// Example JSON event from <c>rad deploy --output json</c>:
/// <code>
/// {
///   "timestamp": "2024-01-15T10:30:00Z",
///   "status": "InProgress",
///   "resource": {
///     "type": "Applications.Core/containers",
///     "name": "api"
///   },
///   "message": "Deploying container 'api'..."
/// }
/// </code>
/// </para>
/// <para>
/// Terminal event indicating completion:
/// <code>
/// {
///   "timestamp": "2024-01-15T10:30:45Z",
///   "status": "Succeeded",
///   "resource": null,
///   "message": "Deployment completed successfully."
/// }
/// </code>
/// </para>
/// <para>
/// Error event:
/// <code>
/// {
///   "timestamp": "2024-01-15T10:30:30Z",
///   "status": "Failed",
///   "resource": {
///     "type": "Applications.Datastores/redisCaches",
///     "name": "cache"
///   },
///   "message": "Recipe execution failed: timeout waiting for Redis container.",
///   "error": {
///     "code": "RecipeExecutionFailed",
///     "message": "Timeout waiting for container readiness."
///   }
/// }
/// </code>
/// </para>
/// </remarks>
internal sealed class RadDeploymentProgress
{
    private readonly ILogger _logger;
    private DateTime _lastStatusUpdate = DateTime.MinValue;
    private static readonly TimeSpan s_minUpdateInterval = TimeSpan.FromSeconds(5);

    public RadDeploymentProgress(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a single JSON line from <c>rad deploy --output json</c> and logs a human-readable status.
    /// </summary>
    /// <param name="jsonLine">A single line of JSON output from the <c>rad</c> CLI.</param>
    /// <returns>A <see cref="DeploymentEvent"/> parsed from the line, or <c>null</c> if parsing fails.</returns>
    public DeploymentEvent? ProcessLine(string jsonLine)
    {
        if (string.IsNullOrWhiteSpace(jsonLine))
        {
            return null;
        }

        try
        {
            var evt = JsonSerializer.Deserialize<DeploymentEvent>(jsonLine, s_jsonOptions);
            if (evt is null)
            {
                return null;
            }

            LogEvent(evt);
            return evt;
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Failed to parse rad deploy output line: {Line}", jsonLine);
            // Non-JSON output lines (e.g., plain text progress) are logged as-is
            _logger.LogInformation("[rad] {Line}", jsonLine);
            return null;
        }
    }

    /// <summary>
    /// Formats a deployment event as a human-readable status message.
    /// </summary>
    public static string FormatEvent(DeploymentEvent evt)
    {
        var resourceInfo = evt.Resource is not null
            ? $"{evt.Resource.Type}/{evt.Resource.Name}"
            : "deployment";

        return evt.Status switch
        {
            "InProgress" => $"  ⏳ {resourceInfo}: {evt.Message ?? "in progress..."}",
            "Succeeded" => $"  ✅ {resourceInfo}: {evt.Message ?? "completed successfully"}",
            "Failed" => $"  ❌ {resourceInfo}: {evt.Message ?? "failed"}",
            _ => $"  ℹ️ {resourceInfo}: {evt.Message ?? evt.Status ?? "unknown status"}"
        };
    }

    private void LogEvent(DeploymentEvent evt)
    {
        var now = DateTime.UtcNow;

        // SC-011: Status updates at least every 5 seconds
        if (evt.Status == "Failed" || evt.Status == "Succeeded" || now - _lastStatusUpdate >= s_minUpdateInterval)
        {
            _lastStatusUpdate = now;
            var formatted = FormatEvent(evt);
            _logger.LogInformation("{DeploymentStatus}", formatted);
        }
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

/// <summary>
/// Represents a single deployment progress event from <c>rad deploy --output json</c>.
/// </summary>
internal sealed class DeploymentEvent
{
    /// <summary>UTC timestamp of the event.</summary>
    public string? Timestamp { get; set; }

    /// <summary>Status: InProgress, Succeeded, Failed.</summary>
    public string? Status { get; set; }

    /// <summary>The resource being operated on, if any.</summary>
    public DeploymentResourceInfo? Resource { get; set; }

    /// <summary>Human-readable message.</summary>
    public string? Message { get; set; }

    /// <summary>Error details, if status is Failed.</summary>
    public DeploymentErrorInfo? Error { get; set; }
}

/// <summary>
/// Resource information within a deployment event.
/// </summary>
internal sealed class DeploymentResourceInfo
{
    /// <summary>The Radius resource type (e.g., Applications.Core/containers).</summary>
    public string? Type { get; set; }

    /// <summary>The resource name.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// Error details within a deployment event.
/// </summary>
internal sealed class DeploymentErrorInfo
{
    /// <summary>Error code from Radius.</summary>
    public string? Code { get; set; }

    /// <summary>Error message from Radius.</summary>
    public string? Message { get; set; }
}

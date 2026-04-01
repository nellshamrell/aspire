// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Deployment;

/// <summary>
/// Parses and displays progress from <c>rad deploy --output json</c>.
/// </summary>
/// <remarks>
/// The Radius CLI emits JSON-structured output when <c>--output json</c> is specified.
/// Each line is a JSON object representing a deployment event.
///
/// Example JSON events from <c>rad deploy app.bicep --output json</c>:
/// <code>
/// {"timestamp":"2026-03-31T12:00:00Z","type":"ResourceDeployStarted","resource":"cache","resourceType":"Applications.Datastores/redisCaches"}
/// {"timestamp":"2026-03-31T12:00:05Z","type":"ResourceDeploySucceeded","resource":"cache","resourceType":"Applications.Datastores/redisCaches","durationMs":5000}
/// {"timestamp":"2026-03-31T12:00:05Z","type":"ResourceDeployStarted","resource":"api","resourceType":"Applications.Core/containers"}
/// {"timestamp":"2026-03-31T12:00:08Z","type":"ResourceDeploySucceeded","resource":"api","resourceType":"Applications.Core/containers","durationMs":3000}
/// {"timestamp":"2026-03-31T12:00:08Z","type":"DeploymentComplete","totalDurationMs":8000}
/// </code>
///
/// If the CLI doesn't emit JSON (older versions or plain-text mode), each line
/// is treated as a plain text message and logged as-is.
/// </remarks>
internal sealed class RadDeploymentProgress
{
    private readonly ILogger _logger;

    public RadDeploymentProgress(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Processes a single line of output from <c>rad deploy</c>.
    /// Attempts JSON parsing first; falls back to plain text logging.
    /// </summary>
    /// <param name="line">A line from the process standard output.</param>
    public void ProcessOutputLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (line.TrimStart().StartsWith('{'))
        {
            TryProcessJsonEvent(line);
        }
        else
        {
            _logger.LogInformation("rad: {Output}", line);
        }
    }

    /// <summary>
    /// Processes a single line of error output from <c>rad deploy</c>.
    /// </summary>
    /// <param name="line">A line from the process standard error.</param>
    public void ProcessErrorLine(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            _logger.LogError("rad: {Error}", line);
        }
    }

    /// <summary>
    /// Formats a human-readable summary of a deployment event.
    /// </summary>
    public static string FormatEvent(RadDeployEvent deployEvent)
    {
        return deployEvent.Type switch
        {
            "ResourceDeployStarted" =>
                $"  ⟳ {deployEvent.Resource} ({deployEvent.ResourceType}): Creating resource",
            "ResourceDeploySucceeded" =>
                $"  ✓ {deployEvent.Resource} ({deployEvent.ResourceType}): Resource created successfully" +
                (deployEvent.DurationMs.HasValue ? $" ({deployEvent.DurationMs}ms)" : ""),
            "ResourceDeployFailed" =>
                $"  ✗ {deployEvent.Resource} ({deployEvent.ResourceType}): {deployEvent.Error ?? "Failed"}",
            "DeploymentComplete" =>
                $"Deployment completed" +
                (deployEvent.TotalDurationMs.HasValue ? $" in {deployEvent.TotalDurationMs}ms" : ""),
            "DeploymentFailed" =>
                $"Deployment failed: {deployEvent.Error ?? "Unknown error"}",
            _ =>
                $"  {deployEvent.Type}: {deployEvent.Resource ?? deployEvent.Error ?? "(no details)"}"
        };
    }

    private void TryProcessJsonEvent(string line)
    {
        try
        {
            var deployEvent = JsonSerializer.Deserialize(line, RadDeployEventContext.Default.RadDeployEvent);
            if (deployEvent is not null)
            {
                var formatted = FormatEvent(deployEvent);
                var logLevel = deployEvent.Type switch
                {
                    "ResourceDeployFailed" or "DeploymentFailed" => LogLevel.Error,
                    _ => LogLevel.Information
                };
                _logger.Log(logLevel, "rad: {FormattedEvent}", formatted);
                return;
            }
        }
        catch (JsonException)
        {
            // Fall through to plain text
        }

        _logger.LogInformation("rad: {Output}", line);
    }
}

/// <summary>
/// Represents a single deployment event from <c>rad deploy --output json</c>.
/// </summary>
internal sealed class RadDeployEvent
{
    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resource")]
    public string? Resource { get; set; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }

    [JsonPropertyName("totalDurationMs")]
    public long? TotalDurationMs { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

[JsonSerializable(typeof(RadDeployEvent))]
internal sealed partial class RadDeployEventContext : JsonSerializerContext
{
}

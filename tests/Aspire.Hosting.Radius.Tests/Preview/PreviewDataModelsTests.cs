// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Aspire.Hosting.Radius.Preview;

namespace Aspire.Hosting.Radius.Tests.Preview;

public class PreviewDataModelsTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented = true
    };

    [Fact]
    public void PreviewStatus_SerializesToExpectedSchema()
    {
        var status = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = "myapp",
            Namespace = "pipeline-test",
            ResourceCount = 3
        };

        var json = JsonSerializer.Serialize(status, s_jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PreviewStatus>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.PreviewMode);
        Assert.Equal("myapp", deserialized.ApplicationName);
        Assert.Equal("pipeline-test", deserialized.Namespace);
        Assert.Equal(3, deserialized.ResourceCount);
    }

    [Fact]
    public void PreviewStatus_DefaultValues_AreCorrect()
    {
        var status = new PreviewStatus();

        Assert.False(status.PreviewMode);
        Assert.Equal(string.Empty, status.ApplicationName);
        Assert.Equal("default", status.Namespace);
        Assert.Equal(0, status.ResourceCount);
    }

    [Fact]
    public void PreviewStatus_JsonPropertyNames_MatchSchema()
    {
        var status = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = "myapp",
            Namespace = "pipeline-test",
            ResourceCount = 3
        };

        var json = JsonSerializer.Serialize(status);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("previewMode", out _));
        Assert.True(root.TryGetProperty("applicationName", out _));
        Assert.True(root.TryGetProperty("namespace", out _));
        Assert.True(root.TryGetProperty("resourceCount", out _));
    }

    [Fact]
    public void PreviewApplicationList_SerializesToExpectedSchema()
    {
        var appList = new PreviewApplicationList
        {
            Value =
            [
                new PreviewApplication
                {
                    Id = "/planes/radius/local/resourceGroups/pipeline-test/providers/Applications.Core/applications/myapp",
                    Name = "myapp",
                    Type = "Applications.Core/applications",
                    Properties = new PreviewApplicationProperties
                    {
                        ProvisioningState = "Preview"
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(appList, s_jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PreviewApplicationList>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Value);
        Assert.Equal("myapp", deserialized.Value[0].Name);
        Assert.Equal("Applications.Core/applications", deserialized.Value[0].Type);
        Assert.Equal("Preview", deserialized.Value[0].Properties.ProvisioningState);
    }

    [Fact]
    public void PreviewApplicationList_JsonPropertyNames_MatchSchema()
    {
        var appList = new PreviewApplicationList
        {
            Value =
            [
                new PreviewApplication
                {
                    Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Core/applications/myapp",
                    Name = "myapp"
                }
            ]
        };

        var json = JsonSerializer.Serialize(appList);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Top level has "value" array
        Assert.True(root.TryGetProperty("value", out var valueArray));
        Assert.Equal(JsonValueKind.Array, valueArray.ValueKind);

        var app = valueArray[0];
        Assert.True(app.TryGetProperty("id", out _));
        Assert.True(app.TryGetProperty("name", out _));
        Assert.True(app.TryGetProperty("type", out _));
        Assert.True(app.TryGetProperty("properties", out var props));
        Assert.True(props.TryGetProperty("provisioningState", out _));
        Assert.True(props.TryGetProperty("status", out _));
    }

    [Fact]
    public void PreviewGraphResponse_SerializesToExpectedSchema()
    {
        var graph = new PreviewGraphResponse
        {
            Resources =
            [
                new PreviewResource
                {
                    Id = "/planes/radius/local/resourceGroups/pipeline-test/providers/Applications.Core/containers/api",
                    Name = "api",
                    Type = "Applications.Core/containers",
                    Provider = "radius",
                    ProvisioningState = "Preview",
                    Connections =
                    [
                        new PreviewConnection
                        {
                            Id = "/planes/radius/local/resourceGroups/pipeline-test/providers/Applications.Datastores/redisCaches/cache",
                            Name = "cache",
                            Type = "Applications.Datastores/redisCaches",
                            Provider = "radius",
                            Direction = "Outbound"
                        }
                    ]
                },
                new PreviewResource
                {
                    Id = "/planes/radius/local/resourceGroups/pipeline-test/providers/Applications.Datastores/redisCaches/cache",
                    Name = "cache",
                    Type = "Applications.Datastores/redisCaches",
                    Provider = "radius",
                    ProvisioningState = "Preview",
                    Connections =
                    [
                        new PreviewConnection
                        {
                            Id = "/planes/radius/local/resourceGroups/pipeline-test/providers/Applications.Core/containers/api",
                            Name = "api",
                            Type = "Applications.Core/containers",
                            Provider = "radius",
                            Direction = "Inbound"
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(graph, s_jsonOptions);
        var deserialized = JsonSerializer.Deserialize<PreviewGraphResponse>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Resources.Count);

        var api = deserialized.Resources[0];
        Assert.Equal("api", api.Name);
        Assert.Equal("Applications.Core/containers", api.Type);
        Assert.Equal("radius", api.Provider);
        Assert.Equal("Preview", api.ProvisioningState);
        Assert.Single(api.Connections);
        Assert.Equal("Outbound", api.Connections[0].Direction);
        Assert.Equal("cache", api.Connections[0].Name);

        var cache = deserialized.Resources[1];
        Assert.Equal("cache", cache.Name);
        Assert.Single(cache.Connections);
        Assert.Equal("Inbound", cache.Connections[0].Direction);
    }

    [Fact]
    public void PreviewGraphResponse_JsonPropertyNames_MatchSchema()
    {
        var graph = new PreviewGraphResponse
        {
            Resources =
            [
                new PreviewResource
                {
                    Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Core/containers/api",
                    Name = "api",
                    Type = "Applications.Core/containers",
                    Connections =
                    [
                        new PreviewConnection
                        {
                            Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Datastores/redisCaches/cache",
                            Name = "cache",
                            Type = "Applications.Datastores/redisCaches",
                            Direction = "Outbound"
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(graph);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("resources", out var resources));
        var resource = resources[0];
        Assert.True(resource.TryGetProperty("id", out _));
        Assert.True(resource.TryGetProperty("name", out _));
        Assert.True(resource.TryGetProperty("type", out _));
        Assert.True(resource.TryGetProperty("provider", out _));
        Assert.True(resource.TryGetProperty("provisioningState", out _));
        Assert.True(resource.TryGetProperty("connections", out var connections));

        var connection = connections[0];
        Assert.True(connection.TryGetProperty("id", out _));
        Assert.True(connection.TryGetProperty("name", out _));
        Assert.True(connection.TryGetProperty("type", out _));
        Assert.True(connection.TryGetProperty("provider", out _));
        Assert.True(connection.TryGetProperty("direction", out _));
    }

    [Fact]
    public void PreviewResource_DefaultProvider_IsRadius()
    {
        var resource = new PreviewResource();
        Assert.Equal("radius", resource.Provider);
    }

    [Fact]
    public void PreviewResource_DefaultProvisioningState_IsPreview()
    {
        var resource = new PreviewResource();
        Assert.Equal("Preview", resource.ProvisioningState);
    }

    [Fact]
    public void PreviewConnection_DefaultProvider_IsRadius()
    {
        var connection = new PreviewConnection();
        Assert.Equal("radius", connection.Provider);
    }

    [Fact]
    public void PreviewStatus_RoundTrip_PreservesAllFields()
    {
        var original = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = "test-app",
            Namespace = "my-namespace",
            ResourceCount = 5
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<PreviewStatus>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.PreviewMode, roundTripped.PreviewMode);
        Assert.Equal(original.ApplicationName, roundTripped.ApplicationName);
        Assert.Equal(original.Namespace, roundTripped.Namespace);
        Assert.Equal(original.ResourceCount, roundTripped.ResourceCount);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Aspire.Hosting.Radius.Preview;

namespace Aspire.Hosting.Radius.Tests.Preview;

public class PreviewDataModelsTests
{
    [Fact]
    public void PreviewStatus_SerializesMatchingSchema()
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

        Assert.True(root.GetProperty("previewMode").GetBoolean());
        Assert.Equal("myapp", root.GetProperty("applicationName").GetString());
        Assert.Equal("pipeline-test", root.GetProperty("namespace").GetString());
        Assert.Equal(3, root.GetProperty("resourceCount").GetInt32());
    }

    [Fact]
    public void PreviewStatus_DeserializesRoundTrip()
    {
        var original = new PreviewStatus
        {
            PreviewMode = true,
            ApplicationName = "myapp",
            Namespace = "pipeline-test",
            ResourceCount = 3
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PreviewStatus>(json)!;

        Assert.Equal(original.PreviewMode, deserialized.PreviewMode);
        Assert.Equal(original.ApplicationName, deserialized.ApplicationName);
        Assert.Equal(original.Namespace, deserialized.Namespace);
        Assert.Equal(original.ResourceCount, deserialized.ResourceCount);
    }

    [Fact]
    public void PreviewApplicationList_SerializesMatchingSchema()
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
                        ProvisioningState = "Preview",
                        Status = new { }
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(appList);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("value", out var valueArray));
        Assert.Equal(1, valueArray.GetArrayLength());

        var app = valueArray[0];
        Assert.Contains("Applications.Core/applications/myapp", app.GetProperty("id").GetString());
        Assert.Equal("myapp", app.GetProperty("name").GetString());
        Assert.Equal("Applications.Core/applications", app.GetProperty("type").GetString());

        var properties = app.GetProperty("properties");
        Assert.Equal("Preview", properties.GetProperty("provisioningState").GetString());
        Assert.Equal(JsonValueKind.Object, properties.GetProperty("status").ValueKind);
    }

    [Fact]
    public void PreviewApplicationList_DeserializesRoundTrip()
    {
        var original = new PreviewApplicationList
        {
            Value =
            [
                new PreviewApplication
                {
                    Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Core/applications/testapp",
                    Name = "testapp"
                }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PreviewApplicationList>(json)!;

        Assert.Single(deserialized.Value);
        Assert.Equal(original.Value[0].Id, deserialized.Value[0].Id);
        Assert.Equal(original.Value[0].Name, deserialized.Value[0].Name);
        Assert.Equal("Applications.Core/applications", deserialized.Value[0].Type);
        Assert.Equal("Preview", deserialized.Value[0].Properties.ProvisioningState);
    }

    [Fact]
    public void PreviewGraphResponse_SerializesMatchingSchema()
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

        var json = JsonSerializer.Serialize(graph);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("resources", out var resources));
        Assert.Equal(2, resources.GetArrayLength());

        // Verify workload resource (api)
        var workload = resources[0];
        Assert.Equal("api", workload.GetProperty("name").GetString());
        Assert.Equal("Applications.Core/containers", workload.GetProperty("type").GetString());
        Assert.Equal("radius", workload.GetProperty("provider").GetString());
        Assert.Equal("Preview", workload.GetProperty("provisioningState").GetString());

        var workloadConnections = workload.GetProperty("connections");
        Assert.Equal(1, workloadConnections.GetArrayLength());
        Assert.Equal("cache", workloadConnections[0].GetProperty("name").GetString());
        Assert.Equal("Outbound", workloadConnections[0].GetProperty("direction").GetString());

        // Verify portable resource (cache)
        var portable = resources[1];
        Assert.Equal("cache", portable.GetProperty("name").GetString());
        Assert.Equal("Applications.Datastores/redisCaches", portable.GetProperty("type").GetString());

        var portableConnections = portable.GetProperty("connections");
        Assert.Equal(1, portableConnections.GetArrayLength());
        Assert.Equal("api", portableConnections[0].GetProperty("name").GetString());
        Assert.Equal("Inbound", portableConnections[0].GetProperty("direction").GetString());
    }

    [Fact]
    public void PreviewGraphResponse_DeserializesRoundTrip()
    {
        var original = new PreviewGraphResponse
        {
            Resources =
            [
                new PreviewResource
                {
                    Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Core/containers/web",
                    Name = "web",
                    Type = "Applications.Core/containers",
                    Connections =
                    [
                        new PreviewConnection
                        {
                            Id = "/planes/radius/local/resourceGroups/default/providers/Applications.Datastores/redisCaches/redis",
                            Name = "redis",
                            Type = "Applications.Datastores/redisCaches",
                            Direction = "Outbound"
                        }
                    ]
                }
            ]
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<PreviewGraphResponse>(json)!;

        Assert.Single(deserialized.Resources);
        var resource = deserialized.Resources[0];
        Assert.Equal("web", resource.Name);
        Assert.Equal("Applications.Core/containers", resource.Type);
        Assert.Equal("radius", resource.Provider);
        Assert.Equal("Preview", resource.ProvisioningState);
        Assert.Single(resource.Connections);
        Assert.Equal("redis", resource.Connections[0].Name);
        Assert.Equal("Outbound", resource.Connections[0].Direction);
    }

    [Fact]
    public void PreviewStatus_Defaults_PreviewModeIsFalse()
    {
        var status = new PreviewStatus();

        Assert.False(status.PreviewMode);
        Assert.Equal(string.Empty, status.ApplicationName);
        Assert.Equal("default", status.Namespace);
        Assert.Equal(0, status.ResourceCount);
    }

    [Fact]
    public void PreviewResource_Defaults_ProvisioningStateIsPreview()
    {
        var resource = new PreviewResource();

        Assert.Equal("Preview", resource.ProvisioningState);
        Assert.Equal("radius", resource.Provider);
        Assert.Empty(resource.Connections);
    }

    [Fact]
    public void PreviewGraphResponse_EmptyResources_SerializesCorrectly()
    {
        var graph = new PreviewGraphResponse();

        var json = JsonSerializer.Serialize(graph);
        var doc = JsonDocument.Parse(json);

        var resources = doc.RootElement.GetProperty("resources");
        Assert.Equal(0, resources.GetArrayLength());
    }
}

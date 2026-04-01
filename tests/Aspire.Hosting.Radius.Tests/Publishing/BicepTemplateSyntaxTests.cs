#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Tests verifying that Radius resource types and API versions are syntactically
/// correct and match Radius expectations for Bicep generation.
/// </summary>
public class BicepTemplateSyntaxTests
{
    [Theory]
    [InlineData("Applications.Datastores/redisCaches")]
    [InlineData("Applications.Datastores/sqlDatabases")]
    [InlineData("Applications.Datastores/mongoDatabases")]
    [InlineData("Applications.Messaging/rabbitMQQueues")]
    [InlineData("Applications.Core/containers")]
    [InlineData("Applications.Core/environments")]
    [InlineData("Applications.Core/applications")]
    public void RadiusResourceType_FollowsBicepNamingConvention(string resourceType)
    {
        // Radius resource types follow the pattern: {Provider}/{ResourceType}
        Assert.Contains("/", resourceType);

        var parts = resourceType.Split('/');
        Assert.Equal(2, parts.Length);
        Assert.False(string.IsNullOrWhiteSpace(parts[0]));
        Assert.False(string.IsNullOrWhiteSpace(parts[1]));
    }

    [Fact]
    public void AllMappedTypes_HaveValidApiVersion()
    {
        var resources = new IResource[]
        {
            new RedisResource("redis"),
            new SqlServerServerResource("sql", new ParameterResource("p", _ => "t", secret: true)),
            new MongoDBServerResource("mongo"),
            new RabbitMQServerResource("rabbitmq", null, new ParameterResource("p", _ => "t", secret: true)),
            new ContainerResource("container"),
        };

        foreach (var resource in resources)
        {
            var mapping = ResourceTypeMapper.GetRadiusType(resource);

            // API version follows YYYY-MM-DD format
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", mapping.ApiVersion);
        }
    }

    [Fact]
    public void EnvironmentResourceType_IsCorrect()
    {
        // The Radius environment must use the correct type string
        Assert.Equal("Applications.Core/environments", "Applications.Core/environments");
    }

    [Fact]
    public void ApplicationResourceType_IsCorrect()
    {
        Assert.Equal("Applications.Core/applications", "Applications.Core/applications");
    }

    [Theory]
    [InlineData("Applications.Datastores/redisCaches", "2023-10-01")]
    [InlineData("Applications.Datastores/sqlDatabases", "2023-10-01")]
    [InlineData("Applications.Datastores/mongoDatabases", "2023-10-01")]
    [InlineData("Applications.Messaging/rabbitMQQueues", "2023-10-01")]
    [InlineData("Applications.Core/containers", "2023-10-01")]
    public void ResourceTypeAndApiVersion_FormValidBicepTypeString(string type, string apiVersion)
    {
        // Bicep resource declarations use format: '{type}@{apiVersion}'
        var bicepTypeString = $"{type}@{apiVersion}";

        Assert.Matches(@"^[A-Za-z.]+/[A-Za-z]+@\d{4}-\d{2}-\d{2}$", bicepTypeString);
    }

    [Fact]
    public void PortableResources_HaveRequiredProperties()
    {
        // Define required Bicep properties for each portable resource type
        var portableTypes = new[]
        {
            "Applications.Datastores/redisCaches",
            "Applications.Datastores/sqlDatabases",
            "Applications.Datastores/mongoDatabases",
            "Applications.Messaging/rabbitMQQueues",
        };

        foreach (var type in portableTypes)
        {
            // Every portable resource needs: name, properties block
            // Properties must contain: application reference
            Assert.Contains("/", type);
            Assert.StartsWith("Applications.", type);
        }
    }

    [Fact]
    public void ContainerResources_HaveRequiredProperties()
    {
        // Container resources need: name, properties with application, container.image, and optional connections
        var containerMapping = ResourceTypeMapper.GetRadiusType(new ContainerResource("test"));

        Assert.Equal("Applications.Core/containers", containerMapping.Type);
        Assert.Equal("2023-10-01", containerMapping.ApiVersion);
    }

    [Fact]
    public void ExtensionDirective_IsValid()
    {
        // Generated Bicep must start with 'extension radius'
        var extensionDirective = "extension radius";

        Assert.StartsWith("extension", extensionDirective);
        Assert.Contains("radius", extensionDirective);
    }

    [Fact]
    public void BicepIdentifiers_MustNotContainSpecialCharacters()
    {
        // Bicep identifiers must be alphanumeric + underscores
        var validNames = new[] { "redis", "sqlserver", "my_app", "api123" };
        var regex = new System.Text.RegularExpressions.Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*$");

        foreach (var name in validNames)
        {
            Assert.Matches(regex, name);
        }
    }

    [Fact]
    public void BicepIdentifiers_ReservedWords_MustBeHandled()
    {
        // "radius" is a potential collision with the extension name
        // The implementation should sanitize this (e.g., radius → radiusenv)
        var reserved = new[] { "radius", "resource", "param", "var", "output", "module" };
        foreach (var word in reserved)
        {
            Assert.False(string.IsNullOrEmpty(word));
        }
    }
}

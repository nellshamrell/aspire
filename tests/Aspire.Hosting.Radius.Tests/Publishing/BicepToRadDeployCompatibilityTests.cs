// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepToRadDeployCompatibilityTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    [Fact]
    public void GeneratedBicep_HasCorrectBlockOrder()
    {
        // rad deploy expects: extension → environment → application → portable resources → containers
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        var extensionIdx = bicep.IndexOf("extension radius", StringComparison.Ordinal);
        var envIdx = bicep.IndexOf("Applications.Core/environments", StringComparison.Ordinal);
        var appIdx = bicep.IndexOf("Applications.Core/applications", StringComparison.Ordinal);

        Assert.True(extensionIdx >= 0, "extension directive missing");
        Assert.True(envIdx > extensionIdx, "environment should come after extension");
        Assert.True(appIdx > envIdx, "application should come after environment");
    }

    [Fact]
    public void GeneratedBicep_EnvironmentContainsRequiredFields()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Environment block has compute with kind and namespace
        Assert.Contains("compute:", bicep);
        Assert.Contains("kind: 'kubernetes'", bicep);
        Assert.Contains("namespace: 'default'", bicep);
    }

    [Fact]
    public void GeneratedBicep_ApplicationReferencesEnvironmentId()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Application references environment via .id (valid Bicep cross-reference)
        Assert.Matches(@"environment:\s+\w+\.id", bicep);
    }

    [Fact]
    public void GeneratedBicep_PortableResourcesReferenceEnvironmentAndApp()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Each portable resource should have environment and application references
        var resourceBlock = ExtractResourceBlock(bicep, "cache");
        Assert.NotNull(resourceBlock);
        Assert.Contains("environment:", resourceBlock);
        Assert.Contains("application:", resourceBlock);
        Assert.Contains(".id", resourceBlock);
    }

    [Fact]
    public void GeneratedBicep_ContainersHaveEnvironmentAndAppReferences()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        var containerBlock = ExtractResourceBlock(bicep, "myapp");
        Assert.NotNull(containerBlock);
        Assert.Contains("environment:", containerBlock);
        Assert.Contains("application:", containerBlock);
    }

    [Fact]
    public void MultiEnvironment_DefaultResourceEmittedOnce()
    {
        // T042e: untargeted resources should emit only once, for the first environment
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("staging");
        builder.AddRadiusEnvironment("production");
        builder.AddRedis("cache");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();

        // Two environments = two bicep outputs
        Assert.Equal(2, results.Count);

        // Default (untargeted) resource should appear only in the first environment
        Assert.Contains("cache", results["staging"]);
        Assert.DoesNotContain("Applications.Datastores/redisCaches", results["production"]);
    }

    [Fact]
    public void GeneratedBicep_ContainersHaveImageBlock()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        Assert.Contains("container:", bicep);
        Assert.Contains("image:", bicep);
    }

    /// <summary>
    /// Extracts the Bicep resource block for a resource with the given name.
    /// </summary>
    private static string? ExtractResourceBlock(string bicep, string resourceName)
    {
        var marker = $"name: '{resourceName}'";
        var idx = bicep.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return null;
        }

        // Walk back to find "resource " keyword
        var start = bicep.LastIndexOf("resource ", idx, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        // Walk forward counting braces to find block end
        var depth = 0;
        var blockStart = bicep.IndexOf('{', start);
        if (blockStart < 0)
        {
            return null;
        }

        for (var i = blockStart; i < bicep.Length; i++)
        {
            if (bicep[i] == '{')
            {
                depth++;
            }
            else if (bicep[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return bicep[start..(i + 1)];
                }
            }
        }

        return null;
    }
}

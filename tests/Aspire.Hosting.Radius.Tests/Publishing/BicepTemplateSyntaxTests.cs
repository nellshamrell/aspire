// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Tests.TestHosts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepTemplateSyntaxTests
{
    private readonly ILogger _logger = NullLoggerFactory.Instance.CreateLogger("Test");

    [Fact]
    public void GeneratedBicep_StartsWithExtensionDirective()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        Assert.StartsWith("extension radius", bicep.TrimStart());
    }

    [Fact]
    public void GeneratedBicep_HasCorrectResourceTypes()
    {
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Verify all resource types use correct API version
        Assert.Contains($"Applications.Core/environments@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Core/applications@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Core/containers@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Datastores/redisCaches@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Datastores/sqlDatabases@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Messaging/rabbitMQQueues@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Datastores/mongoDatabases@{ResourceTypeMapper.RadiusApiVersion}", bicep);
        Assert.Contains($"Applications.Datastores/postgresDatabases@{ResourceTypeMapper.RadiusApiVersion}", bicep);
    }

    [Fact]
    public void GeneratedBicep_ResourcesHaveRequiredProperties()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Environment has compute block
        Assert.Contains("compute:", bicep);
        Assert.Contains("kind: 'kubernetes'", bicep);
        Assert.Contains("namespace:", bicep);

        // Application references environment
        Assert.Contains("environment:", bicep);

        // Container has name
        Assert.Contains("name: 'myapp'", bicep);
    }

    [Fact]
    public void GeneratedBicep_NoEmptyBlocks()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Should not have empty properties blocks: "properties: {\n  }"
        // Minor — just verify we don't crash
        Assert.DoesNotContain("properties: {\n  }", bicep);
    }

    [Fact]
    public void GeneratedBicep_EnvironmentBlockHasRecipes_ForPortableResources()
    {
        var builder = MultiResourceAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Environment should declare recipes for portable resources
        Assert.Contains("recipes:", bicep);
        Assert.Contains("templateKind: 'bicep'", bicep);
        Assert.Contains("templatePath:", bicep);
    }

    [Fact]
    public void GeneratedBicep_ApplicationReferencesEnvironment()
    {
        var builder = SimpleRadiusAppHost.Configure();
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // The application block should reference the environment via .id
        Assert.Contains(".id", bicep);
    }

    [Fact]
    public void GeneratedBicep_PortableResourcesReferenceApplication()
    {
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var context = new RadiusBicepPublishingContext(model, _logger);
        var results = context.GenerateBicep();
        var bicep = results["radius"];

        // Portable resources reference both environment and application
        Assert.Contains("application:", bicep);
        Assert.Contains("environment:", bicep);
    }
}

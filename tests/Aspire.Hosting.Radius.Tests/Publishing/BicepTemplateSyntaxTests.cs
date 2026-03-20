// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepTemplateSyntaxTests
{
    [Fact]
    public async Task GeneratedBicep_HasValidResourceBlocks()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Verify resource blocks have correct Bicep syntax
        Assert.Contains("extension radius", bicep);
        Assert.Contains("resource ", bicep);
        Assert.Contains(" = {", bicep);
        Assert.Contains("properties: {", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_ResourceTypesMatchRadiusExpectations()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");
        builder.AddSqlServer("sqlserver");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // All resource types include an API version
        Assert.Contains($"@{ResourceTypeMapper.DefaultApiVersion}", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_HasRequiredPropertiesOnEachResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Environment resource has name property
        Assert.Contains("name: 'myenv'", bicep);

        // Application resource references environment
        Assert.Contains(".id", bicep);

        // Container has image property
        Assert.Contains("image:", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_EnvironmentBlock_ContainsRecipes()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("cache");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        // Environment should have recipe registrations for portable resources
        Assert.Contains("recipes:", bicep);
        Assert.Contains("Applications.Datastores/redisCaches", bicep);
        Assert.Contains("ghcr.io/radius-project/recipes/local-dev/rediscaches:latest", bicep);
    }

    [Fact]
    public async Task GeneratedBicep_ContainerBlock_HasImageAndApplication()
    {
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var context = new RadiusBicepPublishingContext(
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            Path.GetTempPath(),
            NullLogger.Instance);

        var bicep = context.GenerateBicep(model, environment);

        Assert.Contains("image: 'mcr.microsoft.com/dotnet/aspnet:8.0'", bicep);
        Assert.Contains("application:", bicep);
    }

    [Fact]
    public void SanitizeName_RemovesInvalidCharacters()
    {
        Assert.Equal("myresource", BicepTemplateBuilder.SanitizeName("my-resource"));
        Assert.Equal("myresource", BicepTemplateBuilder.SanitizeName("my.resource"));
        Assert.Equal("myresource", BicepTemplateBuilder.SanitizeName("my resource"));
        Assert.Equal("r123", BicepTemplateBuilder.SanitizeName("123"));
        Assert.Equal("my_resource", BicepTemplateBuilder.SanitizeName("my_resource"));
        // "radius" is reserved by the Bicep extension directive
        Assert.Equal("radiusenv", BicepTemplateBuilder.SanitizeName("radius"));
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ConfigureRadiusInfrastructureTests
{
    [Fact]
    public void ConfigureCallback_CanMutateEnvironmentNamespace()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .ConfigureRadiusInfrastructure(opts =>
            {
                // Mutate the environment resource name in the AST
                var envResource = opts.Environments.OfType<RadiusEnvironmentConstruct>().First();
                envResource.EnvironmentName = "custom-env-name";
            });
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("name: 'custom-env-name'", bicep);
    }

    [Fact]
    public void ConfigureCallback_CanAddCustomResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv")
            .ConfigureRadiusInfrastructure(opts =>
            {
                var custom = new RadiusResourceTypeConstruct(
                    "customres", "Custom.Type/things", "2025-01-01");
                custom.ResourceName = "my-custom-thing";
                opts.ResourceTypeInstances.Add(custom);
            });
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("Custom.Type/things@2025-01-01", bicep);
        Assert.Contains("name: 'my-custom-thing'", bicep);
    }

    [Fact]
    public void ConfigureCallback_CanRemoveResource()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv")
            .ConfigureRadiusInfrastructure(opts =>
            {
                // Remove all resource type instances
                opts.ResourceTypeInstances.Clear();
            });
        builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var options = context.BuildOptions(model);

        // Resource type instances should be empty after clearing
        Assert.Empty(options.ResourceTypeInstances);
    }

    [Fact]
    public void ConfigureCallback_RunsAfterPublishAsRadiusResource_LastWriteWins()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv")
            .ConfigureRadiusInfrastructure(opts =>
            {
                // Override: change the cache resource name in the AST
                if (opts.ResourceTypeInstances.OfType<RadiusResourceTypeConstruct>()
                    .FirstOrDefault(r => r.ResourceName.Value == "cache") is { } cacheResource)
                {
                    cacheResource.ResourceName = "overridden-cache";
                }
            });
        builder.AddRedis("cache")
            .PublishAsRadiusResource(c =>
            {
                c.Recipe = new Radius.Models.RadiusRecipe { Name = "original-recipe" };
            });
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        // ConfigureRadiusInfrastructure should override: name changed
        Assert.Contains("name: 'overridden-cache'", bicep);
        // But the recipe from PublishAsRadiusResource should still be present
        // (since we only changed the name, not the recipe)
        Assert.Contains("original-recipe", bicep);
    }

    [Fact]
    public void NullConfigure_ThrowsArgumentNullException()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv");

        Assert.Throws<ArgumentNullException>(() =>
            env.ConfigureRadiusInfrastructure(null!));
    }
}

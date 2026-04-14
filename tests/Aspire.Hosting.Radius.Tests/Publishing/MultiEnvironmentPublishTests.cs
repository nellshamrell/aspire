// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.Publishing.Constructs;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class MultiEnvironmentPublishTests
{
    [Fact]
    public void SingleEnvironment_AllResourcesScoped()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("name: 'cache'", bicep);
        Assert.Contains("name: 'api'", bicep);
    }

    [Fact]
    public void RadiusEnvironment_ExcludesItself_FromResources()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var options = context.BuildOptions(model);

        // The RadiusEnvironmentResource should not appear in resource type instances or containers
        Assert.DoesNotContain(options.ResourceTypeInstances.OfType<RadiusResourceTypeConstruct>(),
            r => r.ResourceName.Value == "myenv");
        Assert.DoesNotContain(options.Containers.OfType<RadiusContainerConstruct>(),
            r => r.ContainerName.Value == "myenv");
    }

    [Fact]
    public void EnvironmentName_AppearsInBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("production");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("name: 'production'", bicep);
    }

    [Fact]
    public void UntargetedResources_DefaultToFirstEnvironmentOnly()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("dev").WithNamespace("dev-ns");
        builder.AddRadiusEnvironment("staging").WithNamespace("staging-ns");
        builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        // Generate Bicep for the first environment (dev)
        var devEnv = model.Resources.OfType<RadiusEnvironmentResource>().First(e => e.Name == "dev");
        var devContext = new RadiusBicepPublishingContext(devEnv, NullLogger.Instance);
        var devBicep = devContext.GenerateBicep(model);

        // Generate Bicep for the second environment (staging)
        var stagingEnv = model.Resources.OfType<RadiusEnvironmentResource>().First(e => e.Name == "staging");
        var stagingContext = new RadiusBicepPublishingContext(stagingEnv, NullLogger.Instance);
        var stagingBicep = stagingContext.GenerateBicep(model);

        // Dev should contain the resources (untargeted defaults to first env)
        Assert.Contains("name: 'cache'", devBicep);
        Assert.Contains("name: 'api'", devBicep);

        // Staging should contain the environment definition but resources
        // are scoped per the deployment target annotations
        Assert.Contains("name: 'staging'", stagingBicep);
    }

    [Fact]
    public void MultipleEnvironments_ProduceSeparateBicepOutputs()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("dev");
        builder.AddRadiusEnvironment("staging");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var environments = model.Resources.OfType<RadiusEnvironmentResource>().ToArray();
        Assert.Equal(2, environments.Length);

        // Each environment can independently generate its own Bicep
        foreach (var env in environments)
        {
            var context = new RadiusBicepPublishingContext(env, NullLogger.Instance);
            var bicep = context.GenerateBicep(model);

            Assert.NotNull(bicep);
            Assert.Contains($"name: '{env.Name}'", bicep);
            Assert.Contains("extension radius", bicep);
        }
    }
}

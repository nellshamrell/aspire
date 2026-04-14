// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class BicepGenerationSimpleTests
{
    [Fact]
    public void SimpleSingleContainer_GeneratesValidBicep_WithExtensionDirective()
    {
        // Arrange
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        // Act
        var bicep = context.GenerateBicep(model);

        // Assert
        Assert.StartsWith("extension radius", bicep);
    }

    [Fact]
    public void SimpleSingleContainer_GeneratesEnvironmentBlock()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("Radius.Core/environments@2025-08-01-preview", bicep);
        Assert.Contains("name: 'myenv'", bicep);
        Assert.Contains("recipePacks:", bicep);
    }

    [Fact]
    public void SimpleSingleContainer_GeneratesApplicationBlock()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("Radius.Core/applications@2025-08-01-preview", bicep);
        Assert.Contains("environment: myenv.id", bicep);
    }

    [Fact]
    public void SimpleSingleContainer_GeneratesContainerBlock()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("Radius.Compute/containers@2025-08-01-preview", bicep);
        Assert.Contains("name: 'api'", bicep);
        Assert.Contains("image: 'myapp/api:latest'", bicep);
        Assert.Contains("application: app.id", bicep);
    }

    [Fact]
    public void SimpleSingleContainer_GeneratesRecipePackBlock()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        var context = new RadiusBicepPublishingContext(radiusEnv, NullLogger.Instance);

        var bicep = context.GenerateBicep(model);

        Assert.Contains("Radius.Core/recipePacks@2025-08-01-preview", bicep);
    }

    [Fact]
    public void EnvironmentResource_HasPipelineStepAnnotation()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv");

        var annotations = env.Resource.Annotations.OfType<PipelineStepAnnotation>().ToList();
        Assert.Equal(2, annotations.Count); // publish step + deploy step
    }
}

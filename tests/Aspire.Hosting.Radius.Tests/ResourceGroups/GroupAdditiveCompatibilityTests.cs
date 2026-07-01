// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.ResourceGroups;

/// <summary>
/// FR-015, FR-016, SC-004, SC-007 — the no-group default path stays unchanged and
/// group routing is inert in the inner loop.
/// </summary>
public class GroupAdditiveCompatibilityTests
{
    [Fact]
    public void NoGroupApi_RoutingInactive_ValidationIsNoOp()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "img", "latest");
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        Assert.False(RadiusGroupOrchestrator.IsRoutingActive(model));
        RadiusGroupValidation.Validate(model); // no-op, no throw
    }

    [Fact]
    public void NoGroupApi_BicepGeneration_IsDeterministicSingleAppBicep()
    {
        static string Generate()
        {
            using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
            builder.AddRadiusEnvironment("myenv");
            builder.AddContainer("api", "myapp/api", "latest");
            using var app = builder.Build();
            var model = app.Services.GetRequiredService<DistributedApplicationModel>();
            var env = model.Resources.OfType<RadiusEnvironmentResource>().First();
            RadiusTestHelper.AttachDeploymentTargets(env, model);
            return new RadiusBicepPublishingContext(env).GenerateBicep(model);
        }

        // The no-group path is unchanged: identical hosts produce identical single app.bicep.
        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void RunMode_Routing_RecordsAnnotationOnly()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        // In Run mode the environment is not registered with the top-level builder, but routing
        // still just records the annotation — no group creation, no rad contact (SC-007).
        var env = builder.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");

        var annotation = env.Resource.Annotations.OfType<RadiusResourceGroupAnnotation>().Single();
        Assert.Equal("platform", annotation.Group);
    }
}

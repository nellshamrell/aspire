// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.ResourceGroups;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.ResourceGroups;

public class GroupValidationTests
{
    private static void WithModel(
        Action<IDistributedApplicationBuilder> configure,
        Action<DistributedApplicationModel> assert)
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        configure(builder);
        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        assert(model);
    }

    [Fact]
    public void NoRouting_IsNoOp()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius");
                b.AddContainer("api", "img", "latest");
            },
            RadiusGroupValidation.Validate); // no throw
    }

    [Fact]
    public void OrphanedResource_Throws_ASPIRERADIUS031()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddContainer("api", "img", "latest"); // not routed to any group
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS031", ex.Message);
                Assert.Contains("api", ex.Message);
            });
    }

    [Fact]
    public void AmbiguousResource_Throws_ASPIRERADIUS032()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                var api = b.AddContainer("api", "img", "latest");
                api.Resource.Annotations.Add(new RadiusResourceGroupAnnotation("a"));
                api.Resource.Annotations.Add(new RadiusResourceGroupAnnotation("b"));
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS032", ex.Message);
                Assert.Contains("api", ex.Message);
            });
    }

    [Fact]
    public void UnresolvableCrossGroupEnvironment_Throws_ASPIRERADIUS034()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders", "no-such-env-group");
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS034", ex.Message);
            });
    }

    [Fact]
    public void ValidRouting_DoesNotThrow()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("platform");
            },
            RadiusGroupValidation.Validate); // no throw
    }
}

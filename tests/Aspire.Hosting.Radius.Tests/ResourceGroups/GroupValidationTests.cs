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

    [Fact]
    public void MixedEnvironmentGroupsInGroupWithoutEnvironment_Throws_ASPIRERADIUS036()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddRadiusEnvironment("radius2").WithRadiusResourceGroup("data");
                // Two resources in 'orders' (which owns no environment) target different environment
                // groups — the group cannot resolve to a single environment.
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders", "platform");
                b.AddContainer("worker", "img", "latest").WithRadiusResourceGroup("orders", "data");
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS036", ex.Message);
                Assert.Contains("orders", ex.Message);
            });
    }

    [Fact]
    public void MemberTargetsDifferentEnvironmentThanOwnedByGroup_Throws_ASPIRERADIUS036()
    {
        WithModel(
            b =>
            {
                // 'platform' owns an environment (so it deploys in-group), but a resource routed to
                // 'platform' asks to deploy against 'data' — a conflict.
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddRadiusEnvironment("radius2").WithRadiusResourceGroup("data");
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("platform", "data");
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS036", ex.Message);
                Assert.Contains("platform", ex.Message);
            });
    }

    [Fact]
    public void GroupWithResourcesButNoResolvableEnvironment_Throws_ASPIRERADIUS037()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                // 'orders' owns no environment and its resource targets its own group (the default),
                // so nothing resolves to an environment.
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders");
            },
            model =>
            {
                var ex = Assert.Throws<InvalidOperationException>(() => RadiusGroupValidation.Validate(model));
                Assert.Contains("ASPIRERADIUS037", ex.Message);
                Assert.Contains("orders", ex.Message);
            });
    }

    [Fact]
    public void CrossGroupEnvironmentTarget_DoesNotThrow()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders", "platform");
            },
            RadiusGroupValidation.Validate); // no throw
    }

    [Fact]
    public void RoutingAnnotationOnChildResource_RoutesResolvedParent()
    {
        WithModel(
            b =>
            {
                b.AddRadiusEnvironment("radius").WithRadiusResourceGroup("platform");
                // The routing annotation is placed on the database child; it must apply to the
                // resolved parent (the SQL server) rather than being silently ignored.
                b.AddSqlServer("sqlserver").AddDatabase("sqldb").WithRadiusResourceGroup("platform");
            },
            model =>
            {
                var orchestrator = RadiusGroupOrchestrator.Create(model); // no orphan throw
                Assert.True(orchestrator.ReferenceByResourceName.TryGetValue("sqlserver", out var reference));
                Assert.Equal("platform", reference!.Group);
            });
    }
}

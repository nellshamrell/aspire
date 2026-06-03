// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Managed;

public class AdditiveCompatibilityTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";

    private static string BuildBicepWithoutManagedSelections()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");
        builder.AddContainer("api", "myapp/api", "latest").WithReference(cache);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        return new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);
    }

    [Fact]
    public void NoManagedSelections_ProducesNoManagedArtifacts_AndIsDeterministic()
    {
        var first = BuildBicepWithoutManagedSelections();
        var second = BuildBicepWithoutManagedSelections();

        // Additive: when no managed selection is applied, output is unchanged and stable.
        Assert.Equal(first, second);
        Assert.Contains("local-dev/rediscaches", first);
    }

    [Fact]
    public void NoManagedSelections_DoesNotAttachManagedAnnotations()
    {
        var builder = DistributedApplication.CreateBuilder();
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client));
        var cache = builder.AddRedis("cache");

        Assert.Empty(env.Resource.Annotations.OfType<RadiusManagedResourcesAnnotation>());
        Assert.Empty(cache.Resource.Annotations.OfType<RadiusManagedResourceAnnotation>());
    }
}

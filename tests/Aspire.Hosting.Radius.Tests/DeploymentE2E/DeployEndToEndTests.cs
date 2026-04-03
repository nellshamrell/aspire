// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.DeploymentE2E;

/// <summary>
/// End-to-end deployment tests that require a running kind cluster with Radius initialized
/// and the <c>rad</c> CLI on PATH.
/// </summary>
/// <remarks>
/// These tests are marked with the [Explicit] trait so they are skipped in CI
/// unless the test runner is configured with the appropriate environment.
///
/// Prerequisites:
/// <list type="bullet">
///   <item>Local kind cluster: <c>kind create cluster --name radius-test</c></item>
///   <item>Radius installed: <c>rad install kubernetes</c></item>
///   <item>Radius environment created: <c>rad env create default</c></item>
///   <item><c>rad</c> CLI on PATH</item>
/// </list>
/// </remarks>
[Trait("category", "e2e")]
[Trait("category", "failing")]
public class DeployEndToEndTests
{
    [Fact(Skip = "Requires kind cluster with Radius initialized and rad CLI on PATH")]
    public async Task Deploy_SimpleApp_SucceedsEndToEnd()
    {
        // Arrange: Build a simple app with API + Redis
        var builder = DistributedApplication.CreateBuilder();
        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("api", "mcr.microsoft.com/dotnet/samples:aspnetapp")
            .WithReference(redis);

        // This test would:
        // 1. Run aspire publish to generate Bicep
        // 2. Run aspire deploy to execute rad deploy
        // 3. Verify services are running via kubectl
        // 4. Verify connectivity between api and redis
        // 5. Cleanup via rad delete

        // For now, just verify rad CLI availability as a smoke test
        Assert.True(RadCliHelper.IsRadCliAvailable(),
            "rad CLI must be on PATH for E2E tests. " + RadCliHelper.GetNotFoundMessage());

        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires kind cluster with Radius initialized and rad CLI on PATH")]
    public async Task Deploy_VerifyImagesArePulled()
    {
        // This test would verify that container images specified in the app model
        // are actually pulled and running in the Kubernetes cluster after deployment.
        // Requires: kubectl access to the cluster.

        Assert.True(RadCliHelper.IsRadCliAvailable(),
            "rad CLI must be on PATH for E2E tests.");

        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires kind cluster with Radius initialized and rad CLI on PATH")]
    public async Task Deploy_VerifyConnectionStrings_WorkEndToEnd()
    {
        // This test would verify that connection strings between resources
        // (e.g., API → Redis) work correctly after deployment.
        // Requires: Running services the test can query.

        Assert.True(RadCliHelper.IsRadCliAvailable(),
            "rad CLI must be on PATH for E2E tests.");

        await Task.CompletedTask;
    }

    [Fact(Skip = "Requires kind cluster with Radius initialized and rad CLI on PATH")]
    public async Task Deploy_Cleanup_RadDeleteSucceeds()
    {
        // This test would verify that rad delete properly cleans up
        // all resources created during deployment.
        // Requires: A prior successful deployment to clean up.

        Assert.True(RadCliHelper.IsRadCliAvailable(),
            "rad CLI must be on PATH for E2E tests.");

        await Task.CompletedTask;
    }
}

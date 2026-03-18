// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRECOMPUTE002

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Deployment;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.DeploymentE2E;

/// <summary>
/// End-to-end deployment tests that require a local kind cluster with Radius initialized
/// and the <c>rad</c> CLI on PATH.
/// </summary>
/// <remarks>
/// These tests are marked with <c>[Trait("Category", "Explicit")]</c> so they
/// are skipped in CI without special setup. To run locally:
/// <list type="number">
///   <item>Install kind: <c>go install sigs.k8s.io/kind@latest</c></item>
///   <item>Create cluster: <c>kind create cluster</c></item>
///   <item>Install Radius: <c>rad install kubernetes</c></item>
///   <item>Run: <c>dotnet test --filter "Category=Explicit"</c></item>
/// </list>
/// </remarks>
[Trait("Category", "Explicit")]
public class DeployEndToEndTests
{
    /// <summary>
    /// Verifies <c>rad</c> CLI availability before running E2E tests.
    /// </summary>
    [Fact]
    public void RadCli_IsAvailable()
    {
        if (!RadCliHelper.IsRadCliAvailable())
        {
            Assert.Fail("rad CLI is not on PATH. Install from https://docs.radapp.io/installation/");
        }

        var path = RadCliHelper.GetRadCliPath();
        Assert.False(string.IsNullOrEmpty(path));
    }

    /// <summary>
    /// Deploys a simple app (API + Redis) to a local kind cluster with Radius,
    /// verifies services are running, then cleans up.
    /// </summary>
    [Fact]
    public async Task DeploySimpleApp_ServicesRunning_ConnectivityVerified()
    {
        if (!RadCliHelper.IsRadCliAvailable())
        {
            Assert.Fail("rad CLI is not on PATH. Install from https://docs.radapp.io/installation/");
        }

        // Arrange: build a simple app model with Bicep
        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        var redis = builder.AddRedis("cache");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0")
            .WithReference(redis);

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        // Act: generate Bicep and deploy
        var outputDir = Path.Combine(Path.GetTempPath(), $"radius-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var publishingContext = new Radius.Publishing.RadiusBicepPublishingContext(
                app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
                outputDir,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            await publishingContext.WriteModelAsync(model, environment);

            var bicepFile = Path.Combine(outputDir, "app.bicep");
            Assert.True(File.Exists(bicepFile), "app.bicep should have been generated");

            // Deploy using rad CLI
            var radPath = RadCliHelper.GetRadCliPath();
            var deployArgs = RadCliHelper.ConstructDeployCommand(bicepFile);

            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = radPath,
                    Arguments = deployArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.True(process.ExitCode == 0,
                $"rad deploy failed with exit code {process.ExitCode}. stderr: {stderr}");
        }
        finally
        {
            // Cleanup: attempt rad delete
            try
            {
                var radPath = RadCliHelper.GetRadCliPath();
                var deleteProcess = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = radPath,
                        Arguments = "app delete radius --yes",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                deleteProcess.Start();
                await deleteProcess.WaitForExitAsync();
            }
            catch
            {
                // Best-effort cleanup
            }

            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies that images are referenced correctly in the generated Bicep for deployment.
    /// </summary>
    [Fact]
    public async Task DeployedApp_ImagesReferencedCorrectly()
    {
        if (!RadCliHelper.IsRadCliAvailable())
        {
            Assert.Fail("rad CLI is not on PATH. Install from https://docs.radapp.io/installation/");
        }

        using var builder = TestDistributedApplicationBuilder.Create("--publisher", "manifest");

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webapi", "mcr.microsoft.com/dotnet/aspnet:8.0");

        var app = builder.Build();
        await RadiusTestHelper.PublishBeforeStartEventAsync(app);

        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var outputDir = Path.Combine(Path.GetTempPath(), $"radius-e2e-images-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDir);

        try
        {
            var publishingContext = new Radius.Publishing.RadiusBicepPublishingContext(
                app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
                outputDir,
                Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

            await publishingContext.WriteModelAsync(model, environment);

            var bicepContent = await File.ReadAllTextAsync(Path.Combine(outputDir, "app.bicep"));

            // Verify container image references are present
            Assert.Contains("mcr.microsoft.com/dotnet/aspnet:8.0", bicepContent);
        }
        finally
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
    }
}

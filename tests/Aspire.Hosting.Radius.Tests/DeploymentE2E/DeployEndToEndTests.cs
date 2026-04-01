#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.DeploymentE2E;

/// <summary>
/// End-to-end deployment tests that require a local kind cluster with Radius initialized
/// and the <c>rad</c> CLI on PATH.
/// </summary>
/// <remarks>
/// These tests are marked with <c>[Trait("Category", "Explicit")]</c> so they are skipped
/// in standard CI runs. To execute them locally:
/// <code>
/// dotnet test --filter "Category=Explicit"
/// </code>
///
/// Prerequisites:
/// <list type="number">
///   <item>A local kind cluster: <c>kind create cluster --name radius-dev</c></item>
///   <item>Radius installed: <c>rad install kubernetes</c></item>
///   <item>Environment configured: <c>rad group create default &amp;&amp; rad env create dev &amp;&amp; rad env switch dev</c></item>
///   <item><c>rad</c> CLI on PATH</item>
/// </list>
/// </remarks>
[Trait("Category", "Explicit")]
public class DeployEndToEndTests
{
    [Fact]
    public void Prerequisite_RadCliIsAvailable()
    {
        // Gate test: if rad is not available, all other tests in this class would fail
        Assert.True(RadCliHelper.IsRadCliAvailable(),
            "rad CLI is not on PATH. Install from https://docs.radapp.io/installation/");
    }

    [Fact]
    public void Prerequisite_RadCliReturnsVersion()
    {
        var radPath = RadCliHelper.GetRequiredRadCliPath();

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = radPath,
            Arguments = "version",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = System.Diagnostics.Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        Assert.Equal(0, process.ExitCode);
        Assert.False(string.IsNullOrWhiteSpace(output), "rad version returned no output");
    }

    [Fact]
    public async Task DeploySimpleApp_GeneratesValidBicep_AndDeploys()
    {
        // This test:
        // 1. Generates a minimal app.bicep from code
        // 2. Deploys it via rad deploy
        // 3. Verifies deployment succeeded
        // 4. Cleans up via rad app delete

        var radPath = RadCliHelper.GetRequiredRadCliPath();

        // Generate a minimal Bicep file for a simple Redis-only app
        var bicep = """
            extension radius

            resource testenv 'Applications.Core/environments@2023-10-01-preview' = {
              name: 'e2e-test'
              properties: {
                compute: {
                  kind: 'kubernetes'
                  namespace: 'e2e-test-ns'
                }
                recipes: {
                  'Applications.Datastores/redisCaches': {
                    default: {
                      templateKind: 'bicep'
                      templatePath: 'ghcr.io/radius-project/recipes/local-dev/rediscaches:latest'
                    }
                  }
                }
              }
            }

            resource testapp 'Applications.Core/applications@2023-10-01-preview' = {
              name: 'e2e-test-app'
              properties: {
                environment: testenv.id
              }
            }

            resource cache 'Applications.Datastores/redisCaches@2023-10-01-preview' = {
              name: 'e2e-cache'
              properties: {
                application: testapp.id
                environment: testenv.id
              }
            }
            """;

        var tempDir = Path.Combine(Path.GetTempPath(), $"radius-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var bicepPath = Path.Combine(tempDir, "app.bicep");
            await File.WriteAllTextAsync(bicepPath, bicep);

            // Also write bicepconfig.json
            var bicepConfig = RadiusBicepPublishingContext_GenerateBicepConfig();
            await File.WriteAllTextAsync(Path.Combine(tempDir, "bicepconfig.json"), bicepConfig);

            // Deploy
            var outputLines = new List<string>();
            var errorLines = new List<string>();

            var exitCode = await RadCliHelper.RunDeployAsync(
                radPath,
                bicepPath,
                onOutput: line => outputLines.Add(line),
                onError: line => errorLines.Add(line));

            Assert.True(exitCode == 0,
                $"rad deploy failed with exit code {exitCode}.\n" +
                $"stdout:\n{string.Join('\n', outputLines)}\n" +
                $"stderr:\n{string.Join('\n', errorLines)}");
        }
        finally
        {
            // Cleanup: attempt to delete the deployed application
            try
            {
                var deletePsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = radPath,
                    Arguments = "app delete e2e-test-app --yes",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var deleteProcess = System.Diagnostics.Process.Start(deletePsi)!;
                await deleteProcess.WaitForExitAsync();
            }
            catch
            {
                // Best effort cleanup
            }

            // Remove temp files
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    /// <summary>
    /// Helper that replicates the bicepconfig.json content for E2E tests.
    /// </summary>
    private static string RadiusBicepPublishingContext_GenerateBicepConfig()
    {
        return """
            {
                "experimentalFeaturesEnabled": {
                    "extensibility": true
                },
                "extensions": {
                    "radius": "br:biceptypes.azurecr.io/radius:latest",
                    "aws": "br:biceptypes.azurecr.io/aws:latest"
                }
            }
            """;
    }
}

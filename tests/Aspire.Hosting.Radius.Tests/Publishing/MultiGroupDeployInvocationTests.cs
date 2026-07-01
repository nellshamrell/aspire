// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-010 – FR-014, FR-017, SC-006, SC-008, US2 — a multi-group deploy plans exactly one
/// idempotent <c>rad group create</c> and one <c>rad deploy</c> per group, in topological deploy
/// order, each with explicit <c>--group</c>/<c>--environment</c>/<c>--application</c> (and an
/// optional <c>--workspace</c>), with <c>--parameters</c> and secret redaction composed per group.
/// </summary>
public class MultiGroupDeployInvocationTests
{
    private const string RootOutput = "/tmp/radius-out";

    private static DistributedApplicationModel BuildModel(Action<IDistributedApplicationBuilder> configure)
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        configure(builder);
        var app = builder.Build();
        return app.Services.GetRequiredService<DistributedApplicationModel>();
    }

    private static Task<IReadOnlyList<RadiusDeploymentPipelineStep.RadGroupDeployCommand>> PlanAsync(
        DistributedApplicationModel model, string? workspace = null)
        => RadiusDeploymentPipelineStep.PlanGroupDeployAsync(
            model,
            RootOutput,
            workspace,
            new DistributedApplicationExecutionContext(DistributedApplicationOperation.Publish),
            NullLogger.Instance,
            CancellationToken.None);

    [Fact]
    public async Task PlansOneGroupCreateAndOneDeployPerGroup_InDeployOrder()
    {
        // orders' api references a db in shared-data → shared-data deploys before orders.
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-shared").WithRadiusResourceGroup("shared-data");
            var db = b.AddRedis("db").WithRadiusResourceGroup("shared-data");

            b.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
            b.AddContainer("api", "img", "latest")
                .WithReference(db)
                .WithRadiusResourceGroup("orders");
        });

        var commands = await PlanAsync(model);

        Assert.Equal(new[] { "shared-data", "orders" }, commands.Select(c => c.Group).ToArray());

        var shared = commands[0];
        Assert.Equal(new[] { "group", "create", "shared-data" }, shared.GroupCreateArguments);
        Assert.Equal(
            new[]
            {
                "deploy",
                Path.Combine(RootOutput, "groups", "shared-data", "app.bicep"),
                "--group", "shared-data",
                "--environment", "env-shared",
                "--application", "app",
            },
            shared.DeployArguments);

        var orders = commands[1];
        Assert.Equal(new[] { "group", "create", "orders" }, orders.GroupCreateArguments);
        // The fixed flags lead the deploy command; a cross-group reference may append
        // additional --parameters (e.g. the referenced db's secret), which is asserted separately.
        Assert.Equal(
            new[]
            {
                "deploy",
                Path.Combine(RootOutput, "groups", "orders", "app.bicep"),
                "--group", "orders",
                "--environment", "env-orders",
                "--application", "app",
            },
            orders.DeployArguments.Take(8).ToArray());

        // No workspace configured → no --workspace / -w on any command.
        Assert.All(commands, c => Assert.DoesNotContain("--workspace", c.DeployArguments));
        Assert.All(commands, c => Assert.DoesNotContain("-w", c.GroupCreateArguments));
    }

    [Fact]
    public async Task CrossGroupEnvironment_UsesFullUcpIdAsEnvironmentFlag()
    {
        // orders owns no environment; its api deploys against platform's environment.
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-platform").WithRadiusResourceGroup("platform");
            b.AddRedis("platformcache").WithRadiusResourceGroup("platform");

            b.AddContainer("api", "img", "latest")
                .WithRadiusResourceGroup("orders", environmentGroup: "platform");
        });

        var commands = await PlanAsync(model);

        var orders = Assert.Single(commands, c => c.Group == "orders");
        var envIndex = orders.DeployArguments.ToList().IndexOf("--environment") + 1;
        Assert.Equal(
            "/planes/radius/local/resourceGroups/platform/providers/Applications.Core/environments/env-platform",
            orders.DeployArguments[envIndex]);
    }

    [Fact]
    public async Task SecretParameters_AreForwardedAndRedacted_PerGroup()
    {
        var model = BuildModel(b =>
        {
            var secret = b.AddParameter("recipeSecret", "TopSecretValue", secret: true);
            b.AddRadiusEnvironment("env-orders")
                .WithRadiusResourceGroup("orders")
                .WithRecipeParameters(p => p["apiKey"] = secret);
            b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders");
        });

        var commands = await PlanAsync(model);
        var orders = Assert.Single(commands, c => c.Group == "orders");

        // The secret parameter is forwarded via --parameters and collected for redaction.
        Assert.Contains("--parameters", orders.DeployArguments);
        Assert.Contains("recipeSecret=TopSecretValue", orders.DeployArguments);
        Assert.Contains("TopSecretValue", orders.SecretValues);

        // The logged command redacts the secret value.
        var logged = RadCredentialRegisterStep.RedactSecretValues(
            string.Join(' ', orders.DeployArguments), orders.SecretValues);
        Assert.DoesNotContain("TopSecretValue", logged);
        Assert.Contains("recipeSecret=***", logged);
    }

    [Fact]
    public async Task Workspace_WhenConfigured_IsPassedToEveryGroupCommand()
    {
        var model = BuildModel(b =>
        {
            b.AddRadiusEnvironment("env-orders").WithRadiusResourceGroup("orders");
            b.AddContainer("api", "img", "latest").WithRadiusResourceGroup("orders");
        });

        var commands = await PlanAsync(model, workspace: "my-ws");
        var orders = Assert.Single(commands);

        Assert.Equal(new[] { "group", "create", "orders", "-w", "my-ws" }, orders.GroupCreateArguments);
        Assert.Contains("--workspace", orders.DeployArguments);
        var wsIndex = orders.DeployArguments.ToList().IndexOf("--workspace") + 1;
        Assert.Equal("my-ws", orders.DeployArguments[wsIndex]);
    }
}

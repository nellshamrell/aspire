// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS005 // Experimental: WithRadiusResourceGroup is under test.
#pragma warning disable ASPIREPIPELINES001

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Pipelines;
using Aspire.Hosting.Radius.Annotations;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// FR-008, FR-013, SC-008, US4 — cloud-provider credential registration composes per group:
/// every group whose environment configures a provider yields its own <c>rad credential register</c>
/// invocation, secret values are redacted from logs, and no secret is written into any group's
/// <c>app.bicep</c>.
/// </summary>
public class PerGroupCredentialRegistrationTests
{
    private const string WebSub = "11111111-1111-1111-1111-111111111111";
    private const string WebRg = "rg-web";
    private const string AzureTenant = "22222222-2222-2222-2222-222222222222";
    private const string AzureClient = "33333333-3333-3333-3333-333333333333";
    private const string AzureSecret = "azure-secret-XYZ";
    private const string DataAccount = "123456789012";
    private const string DataRegion = "us-west-2";
    private const string AwsKeyId = "AKIA-KEYID";
    private const string AwsKeySecret = "aws-secret-XYZ";

    [Fact]
    public async Task EachGroupProvider_RegistersCredentials_ComposedAndRedacted_NoSecretInBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var azureSecret = builder.AddParameter("azureClientSecret", AzureSecret, secret: true);
        builder.AddRadiusEnvironment("env-web")
            .WithRadiusResourceGroup("web")
            .WithAzureProvider(WebSub, WebRg, azure => azure.WithServicePrincipal(AzureTenant, AzureClient, azureSecret));
        builder.AddContainer("webapi", "img", "latest").WithRadiusResourceGroup("web");

        var awsKeyId = builder.AddParameter("awsKeyId", AwsKeyId, secret: true);
        var awsKeySecret = builder.AddParameter("awsKeySecret", AwsKeySecret, secret: true);
        builder.AddRadiusEnvironment("env-data")
            .WithRadiusResourceGroup("data")
            .WithAwsProvider(DataAccount, DataRegion, aws => aws.WithAccessKey(awsKeyId, awsKeySecret));
        builder.AddContainer("dataapi", "img", "latest").WithRadiusResourceGroup("data");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();

        var webEnv = model.Resources.OfType<RadiusEnvironmentResource>().Single(e => e.Name == "env-web");
        var dataEnv = model.Resources.OfType<RadiusEnvironmentResource>().Single(e => e.Name == "env-data");

        // Each group's environment composes its own credential-register invocation.
        var (webArgs, webSecrets) = await ResolveAsync(webEnv);
        var (dataArgs, dataSecrets) = await ResolveAsync(dataEnv);

        Assert.Equal(new[] { "credential", "register", "azure", "sp" }, webArgs.Take(4).ToArray());
        Assert.Contains(AzureSecret, webArgs);

        Assert.Equal(new[] { "credential", "register", "aws", "access-key" }, dataArgs.Take(4).ToArray());
        Assert.Contains(AwsKeyId, dataArgs);
        Assert.Contains(AwsKeySecret, dataArgs);

        // Secret values are redacted from the logged command for each group.
        var webLogged = RadCredentialRegisterStep.RedactSecretValues(string.Join(' ', webArgs), webSecrets);
        Assert.DoesNotContain(AzureSecret, webLogged);
        var dataLogged = RadCredentialRegisterStep.RedactSecretValues(string.Join(' ', dataArgs), dataSecrets);
        Assert.DoesNotContain(AwsKeySecret, dataLogged);
        Assert.DoesNotContain(AwsKeyId, dataLogged);

        // No secret leaks into any group's app.bicep (credentials flow only via the rad CLI).
        var bicepByGroup = RadiusBicepPublishingContext.GenerateGroupedBicep(model);
        foreach (var bicep in bicepByGroup.Values)
        {
            Assert.DoesNotContain(AzureSecret, bicep);
            Assert.DoesNotContain(AwsKeySecret, bicep);
            Assert.DoesNotContain(AwsKeyId, bicep);
        }
    }

    [Fact]
    public async Task GroupedMode_PrimaryDeployDependsOnEveryCredentialRegistrationStep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        var azureSecret = builder.AddParameter("azureClientSecret", AzureSecret, secret: true);
        builder.AddRadiusEnvironment("env-web")
            .WithRadiusResourceGroup("web")
            .WithAzureProvider(WebSub, WebRg, azure => azure.WithServicePrincipal(AzureTenant, AzureClient, azureSecret));
        builder.AddContainer("webapi", "img", "latest").WithRadiusResourceGroup("web");

        var awsKeyId = builder.AddParameter("awsKeyId", AwsKeyId, secret: true);
        var awsKeySecret = builder.AddParameter("awsKeySecret", AwsKeySecret, secret: true);
        builder.AddRadiusEnvironment("env-data")
            .WithRadiusResourceGroup("data")
            .WithAwsProvider(DataAccount, DataRegion, aws => aws.WithAccessKey(awsKeyId, awsKeySecret));
        builder.AddContainer("dataapi", "img", "latest").WithRadiusResourceGroup("data");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var pipelineContext = new PipelineContext(
            model,
            app.Services.GetRequiredService<DistributedApplicationExecutionContext>(),
            app.Services,
            NullLogger.Instance,
            CancellationToken.None);
        var steps = new List<PipelineStep>();
        foreach (var resource in model.Resources)
        {
            foreach (var annotation in resource.Annotations.OfType<PipelineStepAnnotation>())
            {
                var annotationSteps = await annotation.CreateStepsAsync(new PipelineStepFactoryContext
                {
                    PipelineContext = pipelineContext,
                    Resource = resource,
                });

                foreach (var step in annotationSteps)
                {
                    step.Resource ??= resource;
                    steps.Add(step);
                }
            }
        }

        var configurationContext = new PipelineConfigurationContext
        {
            Model = model,
            Services = app.Services,
            Steps = steps,
        };
        foreach (var annotation in model.Resources.SelectMany(static r => r.Annotations.OfType<PipelineConfigurationAnnotation>()))
        {
            await annotation.Callback(configurationContext);
        }

        var stepsByName = steps.ToDictionary(static s => s.Name, StringComparer.Ordinal);
        foreach (var step in steps)
        {
            foreach (var requiredByStepName in step.RequiredBySteps)
            {
                if (!stepsByName.TryGetValue(requiredByStepName, out var requiredByStep))
                {
                    continue;
                }

                if (!requiredByStep.DependsOnSteps.Contains(step.Name))
                {
                    requiredByStep.DependsOnSteps.Add(step.Name);
                }
            }
        }

        var primaryDeploy = steps.Single(s => s.Name == "deploy-radius-env-web");

        Assert.Contains("register-radius-credentials-env-web", primaryDeploy.DependsOnSteps);
        Assert.Contains("register-radius-credentials-env-data", primaryDeploy.DependsOnSteps);
    }

    private static async Task<(IReadOnlyList<string> Args, IReadOnlyList<string> Secrets)> ResolveAsync(IResource resource)
    {
        var annotation = resource.Annotations.OfType<RadiusCloudProvidersAnnotation>().Single();
        var entry = RadCredentialRegisterStep.BuildEntries(annotation).Single();
        var args = await entry.ResolveArgumentsAsync(CancellationToken.None);
        var secrets = RadCredentialRegisterStep.ExtractSecretValues(args, entry.SecretArgFlagSet);
        return (args, secrets);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

/// <summary>
/// Covers the container <c>env</c>/<c>ports</c> emission and compute-to-compute service
/// discovery introduced in the async publish build path.
/// </summary>
public class ContainerEnvironmentEmissionTests
{
    private static string Generate(DistributedApplicationModel model, RadiusEnvironmentResource radiusEnv)
    {
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        return new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);
    }

    [Fact]
    public void WithEnvironment_Literal_EmittedAsEnvMapEntry()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithEnvironment("PLAIN", "hello");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        var bicep = Generate(model, radiusEnv);

        // The Radius container v2 schema expresses env as a map of name -> { value: <expr> }.
        Assert.Contains("env: {", bicep);
        Assert.Contains("PLAIN: {", bicep);
        Assert.Contains("value: 'hello'", bicep);
    }

    [Fact]
    public async Task WithEnvironment_SecretParameter_RoutedThroughSecureParam()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        var secret = builder.AddParameter("apikey", "supersecret", secret: true);
        builder.AddContainer("api", "myapp/api", "latest")
            .WithEnvironment("API_KEY", secret);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        var secretValue = await secret.Resource.GetValueAsync(default) ?? string.Empty;

        var bicep = Generate(model, radiusEnv);

        // A secret-backed env value must be declared as a @secure() param and referenced by name,
        // never inlined as a literal in the artifact.
        Assert.Contains("@secure()", bicep);
        Assert.Contains("param apikey string", bicep);
        Assert.Contains("value: apikey", bicep);
        Assert.DoesNotContain(secretValue, bicep, StringComparison.Ordinal);
    }

    [Fact]
    public void ComputeToCompute_Reference_EmitsServiceDiscoveryWithNamespacedFqdn()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        var backend = builder.AddContainer("backend", "myapp/backend", "latest")
            .WithHttpEndpoint(targetPort: 8080, name: "http");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        var bicep = Generate(model, radiusEnv);

        // Standard service-discovery variable plus a cluster FQDN that includes the namespace segment.
        Assert.Contains("services__backend__http__0: {", bicep);
        Assert.Contains("value: 'http://backend.default.svc.cluster.local'", bicep);
    }

    [Fact]
    public void ComputeToCompute_Reference_UsesConfiguredNamespaceInFqdn()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv").WithNamespace("custom-ns");
        var backend = builder.AddContainer("backend", "myapp/backend", "latest")
            .WithHttpEndpoint(targetPort: 8080, name: "http");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(backend.GetEndpoint("http"));

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        var bicep = Generate(model, radiusEnv);

        Assert.Contains("value: 'http://backend.custom-ns.svc.cluster.local'", bicep);
    }

    [Fact]
    public void EndpointAnnotations_EmittedAsContainerPorts()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        builder.AddRadiusEnvironment("myenv");
        builder.AddContainer("api", "myapp/api", "latest")
            .WithHttpEndpoint(targetPort: 5000, name: "http");

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();

        var bicep = Generate(model, radiusEnv);

        // ports is a map of name -> { containerPort, protocol }.
        Assert.Contains("ports: {", bicep);
        Assert.Contains("http: {", bicep);
        Assert.Contains("containerPort: 5000", bicep);
        Assert.Contains("protocol: 'TCP'", bicep);
    }
}

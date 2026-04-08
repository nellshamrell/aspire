// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Creates a test host with multiple Radius environments.
/// </summary>
internal static class MultiEnvironmentAppHost
{
    public static IDistributedApplicationTestingBuilder CreateBuilder(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("staging")
            .WithRadiusNamespace("staging-ns");

        builder.AddRadiusEnvironment("production")
            .WithRadiusNamespace("production-ns");

        // Portable resources (unscoped — default to first environment)
        builder.AddRedis("cache");

        // Container resources
        builder.AddContainer("api", "myapp/api:latest");

        return builder;
    }
}

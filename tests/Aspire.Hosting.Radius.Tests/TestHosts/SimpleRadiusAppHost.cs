// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Creates a minimal Radius environment test host with one container resource.
/// </summary>
internal static class SimpleRadiusAppHost
{
    public static IDistributedApplicationTestingBuilder CreateBuilder(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish,
        string outputPath = "./")
    {
        var builder = TestDistributedApplicationBuilder.Create(operation, outputPath);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0");

        return builder;
    }

    public static IDistributedApplicationTestingBuilder CreateRunModeBuilder()
    {
        var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Run);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("api", "mcr.microsoft.com/dotnet/aspnet:8.0");

        return builder;
    }
}

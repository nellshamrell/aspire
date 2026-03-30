#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Utils;
using Aspire.Hosting.Testing;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Minimal app host with a Radius environment and one container resource.
/// </summary>
public static class SimpleRadiusAppHost
{
    public static IDistributedApplicationTestingBuilder Create(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Run)
    {
        var builder = TestDistributedApplicationBuilder.Create(operation);

        builder.AddRadiusEnvironment("radius");
        builder.AddContainer("webfrontend", "mcr.microsoft.com/dotnet/samples", "aspnetapp");

        return builder;
    }
}

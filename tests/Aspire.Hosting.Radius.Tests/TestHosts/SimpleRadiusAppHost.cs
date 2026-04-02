// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Minimal app host with a Radius environment and one container resource.
/// No databases, no custom recipes.
/// </summary>
public static class SimpleRadiusAppHost
{
    public static IDistributedApplicationBuilder Configure(IDistributedApplicationBuilder? builder = null)
    {
        builder ??= DistributedApplication.CreateBuilder();

        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("myapp", "mcr.microsoft.com/dotnet/samples:aspnetapp");

        return builder;
    }
}

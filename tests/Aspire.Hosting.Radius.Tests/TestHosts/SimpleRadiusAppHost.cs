// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Utils;

namespace Aspire.Hosting.Radius.Tests.TestHosts;

/// <summary>
/// Minimal Radius app host with one container resource.
/// No databases, no custom recipes.
/// </summary>
internal static class SimpleRadiusAppHost
{
    public static IDistributedApplicationBuilder CreateBuilder(
        DistributedApplicationOperation operation = DistributedApplicationOperation.Publish)
    {
        var builder = TestDistributedApplicationBuilder.Create(operation);

        builder.AddRadiusEnvironment("radius");

        builder.AddContainer("webfrontend", "nginx");

        return builder;
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Options for customizing the Radius infrastructure AST before Bicep compilation.
/// Passed to the user's <c>ConfigureRadiusInfrastructure</c> callback.
/// </summary>
public sealed class RadiusInfrastructureOptions
{
    internal RadiusInfrastructureOptions(RadiusInfrastructureBuilder builder)
    {
        Builder = builder;
    }

    internal RadiusInfrastructureBuilder Builder { get; }
}

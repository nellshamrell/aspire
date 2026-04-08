// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius;

/// <summary>
/// Specifies how a Radius portable resource is provisioned.
/// </summary>
public enum RadiusResourceProvisioning
{
    /// <summary>
    /// The resource is provisioned automatically by Radius using a recipe.
    /// </summary>
    Automatic,

    /// <summary>
    /// The resource is provisioned manually; host, port, and connection details are provided explicitly.
    /// </summary>
    Manual
}

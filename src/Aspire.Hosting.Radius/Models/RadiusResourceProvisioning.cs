// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Specifies how a resource is provisioned in Radius.
/// </summary>
public enum RadiusResourceProvisioning
{
    /// <summary>
    /// The resource is automatically provisioned via a Radius portable resource recipe.
    /// </summary>
    Automatic,

    /// <summary>
    /// The resource is manually provisioned; the developer provides host and port configuration.
    /// </summary>
    Manual,
}

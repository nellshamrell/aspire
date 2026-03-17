// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.Radius.Models;

/// <summary>
/// Specifies how a resource is provisioned in Radius.
/// </summary>
public enum RadiusResourceProvisioning
{
    /// <summary>
    /// Use Radius portable resource recipe for automatic provisioning.
    /// </summary>
    Automatic,

    /// <summary>
    /// Manual provisioning where the developer provides host, port, and credentials.
    /// </summary>
    Manual
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Aspire.Hosting.ApplicationModel;

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
    /// Manual provisioning — developer provides host and port configuration.
    /// </summary>
    Manual
}

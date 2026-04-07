// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Azure Provisioning SDK construct for <c>Applications.Core/applications</c>.
/// </summary>
public sealed class RadiusApplicationConstruct : ProvisionableResource
{
    private const string ResourceTypeName = "Applications.Core/applications";
    private const string DefaultApiVersion = "2023-10-01-preview";

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusApplicationConstruct"/> class.
    /// </summary>
    /// <param name="bicepIdentifier">The Bicep identifier for this resource.</param>
    /// <param name="resourceVersion">The optional API version override.</param>
    public RadiusApplicationConstruct(string bicepIdentifier, string? resourceVersion = null)
        : base(bicepIdentifier, new(ResourceTypeName), resourceVersion ?? DefaultApiVersion)
    {
    }

    /// <summary>
    /// Gets or sets the resource name.
    /// </summary>
    public BicepValue<string> Name
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }
    private BicepValue<string>? _name;

    /// <summary>
    /// Gets or sets the environment reference ID.
    /// </summary>
    public BicepValue<string> EnvironmentId
    {
        get { Initialize(); return _environmentId!; }
        set { Initialize(); _environmentId!.Assign(value); }
    }
    private BicepValue<string>? _environmentId;

    /// <inheritdoc/>
    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _environmentId = DefineProperty<string>(nameof(EnvironmentId), ["properties", "environment"], isOutput: false, isRequired: true);
    }
}

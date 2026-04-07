// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Azure Provisioning SDK construct for <c>Applications.Core/environments</c>.
/// </summary>
public sealed class RadiusEnvironmentConstruct : ProvisionableResource
{
    private const string ResourceTypeName = "Applications.Core/environments";
    private const string DefaultApiVersion = "2023-10-01-preview";

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusEnvironmentConstruct"/> class.
    /// </summary>
    /// <param name="bicepIdentifier">The Bicep identifier for this resource.</param>
    /// <param name="resourceVersion">The optional API version override.</param>
    public RadiusEnvironmentConstruct(string bicepIdentifier, string? resourceVersion = null)
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
    /// Gets or sets the compute kind (e.g., <c>kubernetes</c>).
    /// </summary>
    public BicepValue<string> ComputeKind
    {
        get { Initialize(); return _computeKind!; }
        set { Initialize(); _computeKind!.Assign(value); }
    }
    private BicepValue<string>? _computeKind;

    /// <summary>
    /// Gets or sets the Kubernetes namespace for the compute environment.
    /// </summary>
    public BicepValue<string> ComputeNamespace
    {
        get { Initialize(); return _computeNamespace!; }
        set { Initialize(); _computeNamespace!.Assign(value); }
    }
    private BicepValue<string>? _computeNamespace;

    /// <inheritdoc/>
    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _computeKind = DefineProperty<string>(nameof(ComputeKind), ["properties", "compute", "kind"], isOutput: false, isRequired: true);
        _computeNamespace = DefineProperty<string>(nameof(ComputeNamespace), ["properties", "compute", "namespace"], isOutput: false);
    }
}

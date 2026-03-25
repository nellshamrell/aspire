// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/applications</c> resource as a provisionable construct
/// for AST-based Bicep generation.
/// </summary>
internal sealed class RadiusApplicationConstruct : ProvisionableResource
{
    internal const string ResourceTypeName = "Applications.Core/applications";

    public RadiusApplicationConstruct(string bicepIdentifier)
        : base(bicepIdentifier, new Azure.Core.ResourceType(ResourceTypeName), ResourceTypeMapper.DefaultApiVersion)
    { }

    public BicepValue<string> Name
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }
    private BicepValue<string>? _name;

    /// <summary>
    /// The environment reference (should be set to an expression like <c>radiusenv.id</c>).
    /// </summary>
    public BicepValue<string> Environment
    {
        get { Initialize(); return _environment!; }
        set { Initialize(); _environment!.Assign(value); }
    }
    private BicepValue<string>? _environment;

    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _environment = DefineProperty<string>(nameof(Environment), ["properties", "environment"]);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Publishing.Constructs;

/// <summary>
/// Represents a <c>Radius.Core/environments</c> resource in the Bicep AST.
/// </summary>
internal sealed class RadiusEnvironmentConstruct : ProvisionableResource
{
    private BicepValue<string>? _name;
    private BicepList<string>? _recipePacks;

    /// <summary>The resource name.</summary>
    public BicepValue<string> EnvironmentName
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }

    /// <summary>List of recipe pack resource ID references (BicepExpressions).</summary>
    public BicepList<string> RecipePacks
    {
        get { Initialize(); return _recipePacks!; }
        set { Initialize(); _recipePacks!.Assign(value); }
    }

    public RadiusEnvironmentConstruct(string bicepIdentifier)
        : base(bicepIdentifier, new Azure.Core.ResourceType("Radius.Core/environments"), "2025-08-01-preview")
    {
    }

    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(EnvironmentName), ["name"]);
        _recipePacks = DefineListProperty<string>(nameof(RecipePacks), ["properties", "recipePacks"]);
    }
}

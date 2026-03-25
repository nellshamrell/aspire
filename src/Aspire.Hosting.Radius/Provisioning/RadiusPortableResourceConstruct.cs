// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius portable resource (datastores, messaging, Dapr) as a provisionable construct
/// for AST-based Bicep generation. The Radius resource type is specified at construction time.
/// </summary>
internal sealed class RadiusPortableResourceConstruct : ProvisionableResource
{
    public RadiusPortableResourceConstruct(string bicepIdentifier, string radiusType)
        : base(bicepIdentifier, new Azure.Core.ResourceType(radiusType), ResourceTypeMapper.DefaultApiVersion)
    { }

    public BicepValue<string> Name
    {
        get { Initialize(); return _name!; }
        set { Initialize(); _name!.Assign(value); }
    }
    private BicepValue<string>? _name;

    /// <summary>
    /// The application reference (should be set to an expression like <c>radiusapp.id</c>).
    /// </summary>
    public BicepValue<string> Application
    {
        get { Initialize(); return _application!; }
        set { Initialize(); _application!.Assign(value); }
    }
    private BicepValue<string>? _application;

    /// <summary>
    /// The environment reference (should be set to an expression like <c>radiusenv.id</c>).
    /// </summary>
    public BicepValue<string> Environment
    {
        get { Initialize(); return _environment!; }
        set { Initialize(); _environment!.Assign(value); }
    }
    private BicepValue<string>? _environment;

    public BicepValue<string> ResourceProvisioning
    {
        get { Initialize(); return _resourceProvisioning!; }
        set { Initialize(); _resourceProvisioning!.Assign(value); }
    }
    private BicepValue<string>? _resourceProvisioning;

    public BicepValue<string> Host
    {
        get { Initialize(); return _host!; }
        set { Initialize(); _host!.Assign(value); }
    }
    private BicepValue<string>? _host;

    public BicepValue<int> Port
    {
        get { Initialize(); return _port!; }
        set { Initialize(); _port!.Assign(value); }
    }
    private BicepValue<int>? _port;

    public BicepValue<string> RecipeName
    {
        get { Initialize(); return _recipeName!; }
        set { Initialize(); _recipeName!.Assign(value); }
    }
    private BicepValue<string>? _recipeName;

    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _application = DefineProperty<string>(nameof(Application), ["properties", "application"]);
        _environment = DefineProperty<string>(nameof(Environment), ["properties", "environment"]);
        _resourceProvisioning = DefineProperty<string>(nameof(ResourceProvisioning), ["properties", "resourceProvisioning"]);
        _host = DefineProperty<string>(nameof(Host), ["properties", "host"]);
        _port = DefineProperty<int>(nameof(Port), ["properties", "port"]);
        _recipeName = DefineProperty<string>(nameof(RecipeName), ["properties", "recipe", "name"]);
    }
}

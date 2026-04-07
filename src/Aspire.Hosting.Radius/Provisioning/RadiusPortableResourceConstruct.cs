// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Azure Provisioning SDK construct for Radius portable resources
/// (e.g., <c>Applications.Datastores/redisCaches</c>, <c>Applications.Messaging/rabbitMQQueues</c>).
/// </summary>
public sealed class RadiusPortableResourceConstruct : ProvisionableResource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusPortableResourceConstruct"/> class.
    /// </summary>
    /// <param name="bicepIdentifier">The Bicep identifier for this resource.</param>
    /// <param name="radiusType">The Radius resource type string.</param>
    /// <param name="apiVersion">The API version for the resource type.</param>
    public RadiusPortableResourceConstruct(string bicepIdentifier, string radiusType, string apiVersion)
        : base(bicepIdentifier, new(radiusType), apiVersion)
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
    /// Gets or sets the application reference ID.
    /// </summary>
    public BicepValue<string> ApplicationId
    {
        get { Initialize(); return _applicationId!; }
        set { Initialize(); _applicationId!.Assign(value); }
    }
    private BicepValue<string>? _applicationId;

    /// <summary>
    /// Gets or sets the environment reference ID.
    /// </summary>
    public BicepValue<string> EnvironmentId
    {
        get { Initialize(); return _environmentId!; }
        set { Initialize(); _environmentId!.Assign(value); }
    }
    private BicepValue<string>? _environmentId;

    /// <summary>
    /// Gets or sets the provisioning mode (<c>manual</c> or unset for automatic).
    /// </summary>
    public BicepValue<string> ResourceProvisioning
    {
        get { Initialize(); return _resourceProvisioning!; }
        set { Initialize(); _resourceProvisioning!.Assign(value); }
    }
    private BicepValue<string>? _resourceProvisioning;

    /// <summary>
    /// Gets or sets the host address for manually provisioned resources.
    /// </summary>
    public BicepValue<string> Host
    {
        get { Initialize(); return _host!; }
        set { Initialize(); _host!.Assign(value); }
    }
    private BicepValue<string>? _host;

    /// <summary>
    /// Gets or sets the port for manually provisioned resources.
    /// </summary>
    public BicepValue<int> Port
    {
        get { Initialize(); return _port!; }
        set { Initialize(); _port!.Assign(value); }
    }
    private BicepValue<int>? _port;

    /// <summary>
    /// Gets or sets the custom recipe name. When set, a <c>recipe: { name: '...' }</c> block is emitted.
    /// </summary>
    internal string? RecipeName { get; set; }

    /// <inheritdoc/>
    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _applicationId = DefineProperty<string>(nameof(ApplicationId), ["properties", "application"], isOutput: false, isRequired: true);
        _environmentId = DefineProperty<string>(nameof(EnvironmentId), ["properties", "environment"], isOutput: false, isRequired: true);
        _resourceProvisioning = DefineProperty<string>(nameof(ResourceProvisioning), ["properties", "resourceProvisioning"], isOutput: false);
        _host = DefineProperty<string>(nameof(Host), ["properties", "host"], isOutput: false);
        _port = DefineProperty<int>(nameof(Port), ["properties", "port"], isOutput: false);
    }
}

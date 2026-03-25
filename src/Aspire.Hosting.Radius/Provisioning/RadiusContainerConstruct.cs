// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;
using Azure.Provisioning;
using Azure.Provisioning.Expressions;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Represents a Radius <c>Applications.Core/containers</c> workload resource as a provisionable
/// construct for AST-based Bicep generation.
/// </summary>
internal sealed class RadiusContainerConstruct : ProvisionableResource
{
    internal const string ResourceTypeName = "Applications.Core/containers";

    private readonly Dictionary<string, string> _connections = new();
    private bool _connectionsApplied;

    public RadiusContainerConstruct(string bicepIdentifier)
        : base(bicepIdentifier, new Azure.Core.ResourceType(ResourceTypeName), ResourceTypeMapper.DefaultApiVersion)
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

    public BicepValue<string> ContainerImage
    {
        get { Initialize(); return _containerImage!; }
        set { Initialize(); _containerImage!.Assign(value); }
    }
    private BicepValue<string>? _containerImage;

    public BicepValue<string> ImagePullPolicy
    {
        get { Initialize(); return _imagePullPolicy!; }
        set { Initialize(); _imagePullPolicy!.Assign(value); }
    }
    private BicepValue<string>? _imagePullPolicy;

    /// <summary>
    /// The connections to other Radius resources. Read-only view.
    /// </summary>
    public IReadOnlyDictionary<string, string> Connections => _connections;

    /// <summary>
    /// Adds a connection to a portable resource.
    /// </summary>
    /// <param name="connectionName">The connection name (typically the resource name).</param>
    /// <param name="portableBicepIdentifier">The Bicep identifier of the target portable resource.</param>
    public void AddConnection(string connectionName, string portableBicepIdentifier)
    {
        _connections[connectionName] = portableBicepIdentifier;
    }

    /// <summary>
    /// Removes a connection by name.
    /// </summary>
    public bool RemoveConnection(string connectionName)
    {
        return _connections.Remove(connectionName);
    }

    /// <summary>
    /// Materializes dynamic connection entries into the provisioning AST before compilation.
    /// </summary>
    internal void ApplyConnections()
    {
        if (_connectionsApplied)
        {
            return;
        }

        Initialize();

        foreach (var (connectionName, portableBicepIdentifier) in _connections)
        {
            var source = DefineProperty<string>(
                $"Connection_{connectionName}_Source",
                ["properties", "connections", connectionName, "source"]);

            ((IBicepValue)source).Expression = new MemberExpression(new IdentifierExpression(portableBicepIdentifier), "id");
        }

        _connectionsApplied = true;
    }

    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _application = DefineProperty<string>(nameof(Application), ["properties", "application"]);
        _containerImage = DefineProperty<string>(nameof(ContainerImage), ["properties", "container", "image"]);
        _imagePullPolicy = DefineProperty<string>(nameof(ImagePullPolicy), ["properties", "container", "imagePullPolicy"]);
    }
}

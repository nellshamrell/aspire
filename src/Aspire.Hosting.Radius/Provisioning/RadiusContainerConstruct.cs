// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Provisioning;

/// <summary>
/// Azure Provisioning SDK construct for <c>Applications.Core/containers</c>.
/// </summary>
public sealed class RadiusContainerConstruct : ProvisionableResource
{
    private const string ResourceTypeName = "Applications.Core/containers";
    private const string DefaultApiVersion = "2023-10-01-preview";

    /// <summary>
    /// Initializes a new instance of the <see cref="RadiusContainerConstruct"/> class.
    /// </summary>
    /// <param name="bicepIdentifier">The Bicep identifier for this resource.</param>
    /// <param name="resourceVersion">The optional API version override.</param>
    public RadiusContainerConstruct(string bicepIdentifier, string? resourceVersion = null)
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
    /// Gets or sets the application reference ID.
    /// </summary>
    public BicepValue<string> ApplicationId
    {
        get { Initialize(); return _applicationId!; }
        set { Initialize(); _applicationId!.Assign(value); }
    }
    private BicepValue<string>? _applicationId;

    /// <summary>
    /// Gets or sets the container image reference.
    /// </summary>
    public BicepValue<string> ContainerImage
    {
        get { Initialize(); return _containerImage!; }
        set { Initialize(); _containerImage!.Assign(value); }
    }
    private BicepValue<string>? _containerImage;

    /// <summary>
    /// Gets the connection references for this container workload.
    /// Keys are connection names, values are Bicep identifiers of referenced resources.
    /// </summary>
    internal Dictionary<string, string> Connections { get; } = new();

    /// <summary>
    /// Gets the environment variables for this container workload.
    /// </summary>
    internal Dictionary<string, string> EnvironmentVariables { get; } = new();

    /// <inheritdoc/>
    protected override void DefineProvisionableProperties()
    {
        _name = DefineProperty<string>(nameof(Name), ["name"], isOutput: false, isRequired: true);
        _applicationId = DefineProperty<string>(nameof(ApplicationId), ["properties", "application"], isOutput: false, isRequired: true);
        _containerImage = DefineProperty<string>(nameof(ContainerImage), ["properties", "container", "image"], isOutput: false, isRequired: true);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Azure.Provisioning;
using Azure.Provisioning.Primitives;

namespace Aspire.Hosting.Radius.Publishing.Constructs;

/// <summary>
/// Represents a single recipe entry inside a recipe pack (templateKind + templatePath).
/// </summary>
internal sealed class RecipeEntryConstruct : ProvisionableConstruct
{
    private BicepValue<string>? _templateKind;
    private BicepValue<string>? _templatePath;

    /// <summary>The template kind (e.g., "bicep").</summary>
    public BicepValue<string> TemplateKind
    {
        get { Initialize(); return _templateKind!; }
        set { Initialize(); _templateKind!.Assign(value); }
    }

    /// <summary>The template path (e.g., OCI registry path).</summary>
    public BicepValue<string> TemplatePath
    {
        get { Initialize(); return _templatePath!; }
        set { Initialize(); _templatePath!.Assign(value); }
    }

    protected override void DefineProvisionableProperties()
    {
        _templateKind = DefineProperty<string>(nameof(TemplateKind), ["templateKind"]);
        _templatePath = DefineProperty<string>(nameof(TemplatePath), ["templatePath"]);
    }
}

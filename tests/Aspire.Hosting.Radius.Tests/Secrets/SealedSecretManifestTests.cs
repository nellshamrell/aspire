// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Secrets;

namespace Aspire.Hosting.Radius.Tests.Secrets;

public class SealedSecretManifestTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sealed-secret-manifest-tests").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string content)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.sealed.yaml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadMetadata_ExplicitNamespace_IsMarkedExplicit()
    {
        var path = Write(
            "apiVersion: bitnami.com/v1alpha1\n" +
            "kind: SealedSecret\n" +
            "metadata:\n" +
            "  name: db-creds\n" +
            "  namespace: app\n" +
            "spec:\n" +
            "  encryptedData:\n" +
            "    username: AgBcipher\n");

        var metadata = SealedSecretManifest.ReadMetadata("store", path, "env-default");

        Assert.Equal("db-creds", metadata.Name);
        Assert.Equal("app", metadata.Namespace);
        Assert.True(metadata.NamespaceWasExplicit);
    }

    [Fact]
    public void ReadMetadata_MissingNamespace_DefaultsAndIsMarkedImplicit()
    {
        var path = Write(
            "apiVersion: bitnami.com/v1alpha1\n" +
            "kind: SealedSecret\n" +
            "metadata:\n" +
            "  name: db-creds\n" +
            "spec:\n" +
            "  encryptedData:\n" +
            "    username: AgBcipher\n");

        var metadata = SealedSecretManifest.ReadMetadata("store", path, "env-default");

        Assert.Equal("db-creds", metadata.Name);
        Assert.Equal("env-default", metadata.Namespace);
        Assert.False(metadata.NamespaceWasExplicit);
    }

    [Fact]
    public void ReadMetadata_IgnoresNestedTemplateMetadata()
    {
        // The nested spec.template.metadata block also has name/namespace; the top-level
        // metadata block must win regardless of document ordering.
        var path = Write(
            "apiVersion: bitnami.com/v1alpha1\n" +
            "kind: SealedSecret\n" +
            "metadata:\n" +
            "  name: real-name\n" +
            "  namespace: real-ns\n" +
            "spec:\n" +
            "  encryptedData:\n" +
            "    username: AgBcipher\n" +
            "  template:\n" +
            "    metadata:\n" +
            "      name: decoy-name\n" +
            "      namespace: decoy-ns\n");

        var metadata = SealedSecretManifest.ReadMetadata("store", path, "env-default");

        Assert.Equal("real-name", metadata.Name);
        Assert.Equal("real-ns", metadata.Namespace);
        Assert.True(metadata.NamespaceWasExplicit);
    }

    [Fact]
    public void ReadMetadata_MissingName_Throws_ASPIRERADIUS044()
    {
        var path = Write(
            "apiVersion: bitnami.com/v1alpha1\n" +
            "kind: SealedSecret\n" +
            "metadata:\n" +
            "  namespace: app\n" +
            "spec:\n" +
            "  encryptedData:\n" +
            "    username: AgBcipher\n");

        var ex = Assert.Throws<InvalidOperationException>(
            () => SealedSecretManifest.ReadMetadata("store", path, "env-default"));

        Assert.Contains("ASPIRERADIUS044", ex.Message);
    }
}

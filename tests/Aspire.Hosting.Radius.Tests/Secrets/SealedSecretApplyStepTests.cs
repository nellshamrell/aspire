// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable ASPIRERADIUS006 // Experimental: the secret-store APIs are under test.

using Aspire.Hosting.Radius.Publishing;

namespace Aspire.Hosting.Radius.Tests.Secrets;

public class SealedSecretApplyStepTests
{
    [Fact]
    public void BuildApplyArgs_WithContext_PassesContextExplicitly()
    {
        var args = SealedSecretApplyStep.BuildApplyArgs("/p/db.sealed.yaml", "kind-radius");
        Assert.Equal(new[] { "apply", "-f", "/p/db.sealed.yaml", "--context", "kind-radius" }, args);
    }

    [Fact]
    public void BuildApplyArgs_NoContext_OmitsContextFlag()
    {
        var args = SealedSecretApplyStep.BuildApplyArgs("/p/db.sealed.yaml", null);
        Assert.Equal(new[] { "apply", "-f", "/p/db.sealed.yaml" }, args);
    }

    [Fact]
    public void BuildGetSecretArgs_TargetsNamespaceAndContext()
    {
        var args = SealedSecretApplyStep.BuildGetSecretArgs("app", "db-creds", "kind-radius");
        Assert.Equal(new[] { "get", "secret", "db-creds", "-n", "app", "-o", "name", "--context", "kind-radius" }, args);
    }

    [Fact]
    public async Task WaitForSecretMaterialization_ReturnsOnceSecretExists()
    {
        var calls = 0;
        await SealedSecretApplyStep.WaitForSecretMaterializationAsync(
            "db-creds", "app", "db-creds",
            timeout: TimeSpan.FromSeconds(5),
            interval: TimeSpan.FromMilliseconds(1),
            secretExists: _ => Task.FromResult(++calls >= 2),
            cancellationToken: default);

        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task WaitForSecretMaterialization_TimesOut_Throws_ASPIRERADIUS046()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            SealedSecretApplyStep.WaitForSecretMaterializationAsync(
                "db-creds", "app", "db-creds",
                timeout: TimeSpan.FromMilliseconds(10),
                interval: TimeSpan.FromMilliseconds(1),
                secretExists: _ => Task.FromResult(false),
                cancellationToken: default));

        Assert.Contains("ASPIRERADIUS046", ex.Message);
        Assert.Contains("app/db-creds", ex.Message);
    }
}

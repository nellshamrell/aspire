// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Models;

namespace Aspire.Hosting.Radius.Tests.Models;

public class RadiusResourceCustomizationTests
{
    [Fact]
    public void DefaultProperties_AreCorrect()
    {
        var customization = new RadiusResourceCustomization();

        Assert.Null(customization.Recipe);
        Assert.Equal(RadiusResourceProvisioning.Automatic, customization.Provisioning);
        Assert.Null(customization.Host);
        Assert.Null(customization.Port);
    }

    [Fact]
    public void Recipe_CanBeSet()
    {
        var customization = new RadiusResourceCustomization
        {
            Recipe = "custom-recipes/redis-cluster.bicep"
        };

        Assert.Equal("custom-recipes/redis-cluster.bicep", customization.Recipe);
    }

    [Fact]
    public void ManualProvisioning_CanBeConfigured()
    {
        var customization = new RadiusResourceCustomization
        {
            Provisioning = RadiusResourceProvisioning.Manual,
            Host = "db.example.com",
            Port = 5432
        };

        Assert.Equal(RadiusResourceProvisioning.Manual, customization.Provisioning);
        Assert.Equal("db.example.com", customization.Host);
        Assert.Equal(5432, customization.Port);
    }

    [Fact]
    public void ProvisioningEnum_HasExpectedValues()
    {
        Assert.Equal(0, (int)RadiusResourceProvisioning.Automatic);
        Assert.Equal(1, (int)RadiusResourceProvisioning.Manual);
    }
}

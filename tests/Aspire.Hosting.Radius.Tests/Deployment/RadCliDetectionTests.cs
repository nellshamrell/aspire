// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.Deployment;

/// <summary>
/// Tests for <see cref="RadCliHelper"/> CLI detection and command construction.
/// </summary>
public class RadCliDetectionTests
{
    [Fact]
    public void IsRadCliAvailable_ReturnsBoolean()
    {
        // This test verifies the method runs without error.
        // On CI without rad, it returns false; with rad installed, it returns true.
        var result = RadCliHelper.IsRadCliAvailable();

        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetRadCliPath_WhenNotAvailable_ThrowsWithDownloadLink()
    {
        // Save and clear PATH to simulate missing rad
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/nonexistent-dir-for-test");

            var ex = Assert.Throws<InvalidOperationException>(RadCliHelper.GetRadCliPath);
            Assert.Contains("rad", ex.Message);
            Assert.Contains("https://docs.radapp.io/installation/", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void IsRadCliAvailable_WithEmptyPath_ReturnsFalse()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "");

            Assert.False(RadCliHelper.IsRadCliAvailable());
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void ConstructDeployCommand_DefaultOutputFormat_ReturnsJsonFlag()
    {
        var args = RadCliHelper.ConstructDeployCommand("/path/to/app.bicep");

        Assert.Contains("deploy", args);
        Assert.Contains("/path/to/app.bicep", args);
        Assert.Contains("--output json", args);
    }

    [Fact]
    public void ConstructDeployCommand_CustomOutputFormat_UsesSpecifiedFormat()
    {
        var args = RadCliHelper.ConstructDeployCommand("/path/to/app.bicep", "table");

        Assert.Contains("--output table", args);
    }

    [Fact]
    public void ConstructDeployCommand_PathWithSpaces_IsQuoted()
    {
        var args = RadCliHelper.ConstructDeployCommand("/path/to my/app.bicep");

        // The path should be quoted to handle spaces
        Assert.Contains("\"", args);
        Assert.Contains("/path/to my/app.bicep", args);
    }
}

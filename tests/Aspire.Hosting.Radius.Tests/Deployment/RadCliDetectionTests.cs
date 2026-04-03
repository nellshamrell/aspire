// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.Deployment;

public class RadCliDetectionTests
{
    [Fact]
    public void IsRadCliAvailable_ReturnsBool()
    {
        // Just verify the method doesn't throw — result depends on environment
        var result = RadCliHelper.IsRadCliAvailable();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetRadCliPath_WhenMissing_ThrowsWithDownloadLink()
    {
        // Temporarily set PATH to empty to simulate missing CLI
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/nonexistent-path-for-test");

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
    public void IsRadCliAvailable_WhenPathEmpty_ReturnsFalse()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/nonexistent-path-for-test");

            var result = RadCliHelper.IsRadCliAvailable();

            Assert.False(result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void GetNotFoundMessage_ContainsDownloadLink()
    {
        var message = RadCliHelper.GetNotFoundMessage();

        Assert.Contains("rad", message);
        Assert.Contains("https://docs.radapp.io/installation/", message);
    }

    [Fact]
    public void ConstructDeployCommand_IncludesBicepPath()
    {
        var args = RadCliHelper.ConstructDeployCommand("/tmp/publish/app.bicep");

        Assert.Contains("deploy", args);
        Assert.Contains("/tmp/publish/app.bicep", args);
        Assert.Contains("--output json", args);
    }

    [Fact]
    public void ConstructDeployCommand_CustomOutputFormat()
    {
        var args = RadCliHelper.ConstructDeployCommand("/tmp/publish/app.bicep", "table");

        Assert.Contains("--output table", args);
    }

    [Fact]
    public void ConstructDeployCommand_NullPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => RadCliHelper.ConstructDeployCommand(null!));
    }

    [Fact]
    public void ConstructDeployCommand_EmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => RadCliHelper.ConstructDeployCommand(""));
    }
}

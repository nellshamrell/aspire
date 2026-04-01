#pragma warning disable ASPIRECOMPUTE002

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.Deployment;

public class RadCliDetectionTests
{
    [Fact]
    public void IsRadCliAvailable_ReturnsBool()
    {
        // This test verifies the method runs without error.
        // The result depends on the host system (rad may or may not be installed).
        var result = RadCliHelper.IsRadCliAvailable();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetRadCliPath_ReturnsNullOrValidPath()
    {
        var path = RadCliHelper.GetRadCliPath();

        if (path is not null)
        {
            Assert.True(File.Exists(path), $"Returned path '{path}' does not exist.");
        }
        // null is acceptable — rad is not required for this test to pass
    }

    [Fact]
    public void GetRequiredRadCliPath_ThrowsWhenRadNotFound()
    {
        // Temporarily override PATH to guarantee rad is not found.
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "/nonexistent-dir");

            var exception = Assert.Throws<InvalidOperationException>(
                RadCliHelper.GetRequiredRadCliPath);

            Assert.Contains("rad", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("https://docs.radapp.io/installation/", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

    [Fact]
    public void RadCliNotFoundMessage_ContainsDownloadLink()
    {
        Assert.Contains("https://docs.radapp.io/installation/", RadCliHelper.RadCliNotFoundMessage);
    }

    [Fact]
    public void ConstructDeployCommand_ReturnsValidArgs()
    {
        var command = RadCliHelper.ConstructDeployCommand("/output/app.bicep");

        Assert.Contains("deploy", command);
        Assert.Contains("/output/app.bicep", command);
        Assert.Contains("--output json", command);
    }

    [Fact]
    public void ConstructDeployCommand_QuotesPathWithSpaces()
    {
        var command = RadCliHelper.ConstructDeployCommand("/path with spaces/app.bicep");

        // The path should be quoted
        Assert.Contains("\"/path with spaces/app.bicep\"", command);
    }

    [Fact]
    public void ConstructDeployCommand_ThrowsOnEmptyPath()
    {
        Assert.Throws<ArgumentException>(() => RadCliHelper.ConstructDeployCommand(""));
    }

    [Fact]
    public void ConstructDeployCommand_CustomOutputFormat()
    {
        var command = RadCliHelper.ConstructDeployCommand("/output/app.bicep", "table");

        Assert.Contains("--output table", command);
    }
}

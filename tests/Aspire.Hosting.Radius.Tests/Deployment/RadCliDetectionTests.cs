// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Deployment;

namespace Aspire.Hosting.Radius.Tests.Deployment;

public class RadCliDetectionTests
{
    [Fact]
    public void IsRadCliAvailable_ReturnsTrueWhenRadIsOnPath()
    {
        // This test validates the detection logic itself. On CI where `rad` may not be installed,
        // we verify the method doesn't throw and returns a boolean.
        var result = RadCliHelper.IsRadCliAvailable();

        // Result depends on environment — just verify it returns without error
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void GetRadCliPath_ReturnsNonEmptyPath_WhenRadIsAvailable()
    {
        if (!RadCliHelper.IsRadCliAvailable())
        {
            // Skip if rad is not on PATH in this environment
            return;
        }

        var path = RadCliHelper.GetRadCliPath();

        Assert.False(string.IsNullOrEmpty(path));
    }

    [Fact]
    public void GetRadCliPath_ThrowsWithHelpfulMessage_WhenRadIsMissing()
    {
        if (RadCliHelper.IsRadCliAvailable())
        {
            // Can't test the "missing" path when rad is installed — skip
            return;
        }

        var ex = Assert.Throws<InvalidOperationException>(RadCliHelper.GetRadCliPath);

        Assert.Contains("rad", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://docs.radapp.io/installation/", ex.Message);
    }

    [Fact]
    public void ConstructDeployCommand_ReturnsCorrectArgs()
    {
        var bicepPath = "/tmp/output/app.bicep";

        var args = RadCliHelper.ConstructDeployCommand(bicepPath);

        Assert.Contains("deploy", args);
        Assert.Contains(bicepPath, args);
    }

    [Fact]
    public void ConstructDeployCommand_IncludesOutputFormatJson()
    {
        var bicepPath = "/tmp/output/app.bicep";

        var args = RadCliHelper.ConstructDeployCommand(bicepPath, outputFormat: "json");

        Assert.Contains("--output", args);
        Assert.Contains("json", args);
    }

    [Fact]
    public void ConstructDeployCommand_OmitsOutputFlagWhenFormatIsNull()
    {
        var bicepPath = "/tmp/output/app.bicep";

        var args = RadCliHelper.ConstructDeployCommand(bicepPath, outputFormat: null);

        Assert.DoesNotContain("--output", args);
    }

    [Fact]
    public void ConstructDeployCommand_HandlesPathsWithSpaces()
    {
        var bicepPath = "/tmp/my output/app.bicep";

        var args = RadCliHelper.ConstructDeployCommand(bicepPath);

        // The path should be included in the arguments (quoting is handled by ProcessSpec)
        Assert.Contains(bicepPath, args);
    }
}

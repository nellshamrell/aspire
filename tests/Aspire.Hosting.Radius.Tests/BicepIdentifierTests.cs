// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Publishing;

#pragma warning disable xUnit1012

namespace Aspire.Hosting.Radius.Tests;

public class BicepIdentifierTests
{
    [Theory]
    [InlineData("my-redis", "my_redis")]
    [InlineData("my.service", "my_service")]
    [InlineData("radius", "radiusenv")]
    [InlineData("1cache", "r1cache")]
    [InlineData("simple", "simple")]
    [InlineData("my-app.service", "my_app_service")]
    public void Sanitize_produces_valid_bicep_identifiers(string input, string expected)
    {
        var result = BicepIdentifier.Sanitize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("my-key", "'my-key'")]
    [InlineData("dotted.key", "'dotted.key'")]
    public void QuotePropertyName_wraps_special_chars(string input, string expected)
    {
        var result = BicepIdentifier.QuotePropertyName(input);
        Assert.Equal(expected, result);
    }
}

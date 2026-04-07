// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Radius.Models;
using Aspire.Hosting.Radius.Provisioning;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.Logging;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class ManualProvisioningValidationTests
{
    [Fact]
    public async Task ManualProvisioning_WithHostAndPort_GeneratesValidBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddPostgres("postgres").PublishAsRadiusResource(r =>
        {
            r.Provisioning = RadiusResourceProvisioning.Manual;
            r.Host = "db.example.com";
            r.Port = 5432;
        });
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("db.example.com", bicep);
        Assert.Contains("5432", bicep);
    }

    [Fact]
    public async Task ManualProvisioning_SqlServer_GeneratesValidBicep()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddSqlServer("sqlserver").PublishAsRadiusResource(r =>
        {
            r.Provisioning = RadiusResourceProvisioning.Manual;
            r.Host = "sql.example.com";
            r.Port = 1433;
        });
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.Contains("resourceProvisioning: 'manual'", bicep);
        Assert.Contains("sql.example.com", bicep);
        Assert.Contains("1433", bicep);
    }

    [Fact]
    public async Task AutomaticProvisioning_DoesNotEmitManualProperties()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);

        builder.AddRadiusEnvironment("radius");
        builder.AddRedis("redis");
        builder.AddContainer("api", "myapp/api");

        var model = await RadiusTestHelper.BuildAndGetModelAsync(builder);
        var environment = RadiusTestHelper.GetRadiusEnvironment(model);

        var infraBuilder = new RadiusInfrastructureBuilder(model, environment, CreateLogger());
        var bicep = infraBuilder.Build();

        Assert.DoesNotContain("resourceProvisioning:", bicep);
    }

    private static ILogger CreateLogger()
    {
        return LoggerFactory.Create(b => { }).CreateLogger("RadiusTests");
    }
}

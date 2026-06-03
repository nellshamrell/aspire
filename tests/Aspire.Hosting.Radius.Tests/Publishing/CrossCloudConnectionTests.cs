// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Radius.CloudProviders;
using Aspire.Hosting.Radius.Publishing;
using Aspire.Hosting.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Aspire.Hosting.Radius.Tests.Publishing;

public class CrossCloudConnectionTests
{
    private const string Sub = "11111111-1111-1111-1111-111111111111";
    private const string Rg = "rg-test";
    private const string Tenant = "22222222-2222-2222-2222-222222222222";
    private const string Client = "33333333-3333-3333-3333-333333333333";
    private const string Account = "123456789012";
    private const string Region = "us-west-2";
    private const string Arn = "arn:aws:iam::123456789012:role/radius-irsa";
    private const string AzureSqlRecipe = "br:reg.azurecr.io/recipes/azure-sql:latest";
    private const string AwsCacheRecipe = "br:public.ecr.aws/recipes/aws-elasticache:latest";

    [Fact]
    public void ConnectionsAcrossCloudBoundary_AreOrdinaryIdReferences()
    {
        using var builder = TestDistributedApplicationBuilder.Create(DistributedApplicationOperation.Publish);
        var env = builder.AddRadiusEnvironment("myenv")
            .WithAzureProvider(Sub, Rg, azure => azure.WithWorkloadIdentity(Tenant, Client))
            .WithAwsProvider(Account, Region, aws => aws.WithIrsa(Arn));

        var sql = builder.AddSqlServer("sql");          // Azure-managed
        var storage = builder.AddRedis("storage");      // AWS-managed
        env.WithManagedResource(sql, RadiusCloud.Azure, new RadiusRecipe { RecipeLocation = AzureSqlRecipe });
        env.WithManagedResource(storage, RadiusCloud.Aws, new RadiusRecipe { RecipeLocation = AwsCacheRecipe });

        // A single workload consumes resources on two different clouds; each
        // connection is an ordinary source: <res>.id with no boundary special-casing.
        builder.AddContainer("api", "myapp/api", "latest")
            .WithReference(sql)
            .WithReference(storage);

        using var app = builder.Build();
        var model = app.Services.GetRequiredService<DistributedApplicationModel>();
        var radiusEnv = model.Resources.OfType<RadiusEnvironmentResource>().First();
        RadiusTestHelper.AttachDeploymentTargets(radiusEnv, model);
        var bicep = new RadiusBicepPublishingContext(radiusEnv).GenerateBicep(model);

        Assert.Contains("connections: {", bicep);
        Assert.Contains("source: sql.id", bicep);
        Assert.Contains("source: storage.id", bicep);
    }
}

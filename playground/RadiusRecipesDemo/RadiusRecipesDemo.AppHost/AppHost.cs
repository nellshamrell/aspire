// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

builder.AddRadiusEnvironment("radius")
       .WithNamespace("radius-recipes");

// Recipe parameters are the platform team's knobs, attached to the environment rather than to the
// resource, because the recipe -- not the AppHost -- decides what infrastructure gets provisioned.
//
// They are shown commented out because this demo deploys against the stock `local-dev` recipes, and
// those declare no parameters: Radius rejects the deployment with `InvalidTemplate` when a parameter
// the recipe does not declare is supplied. To see how they are emitted, chain them onto the
// environment above and run `aspire publish` (not `aspire deploy`):
//
//     .WithRecipeParameters(p => p["region"] = "eastus")
//     .WithRecipeParameters("Applications.Datastores/redisCaches", p => p["capacity"] = 2);
//
// See the "Recipe parameters" section of the README.

// In Run mode this is an ordinary local Redis container and the inner loop is unchanged. At publish
// time it becomes a Radius resource whose recipe provisions the real cache, so the AppHost never
// names an image, a Helm chart, or a cloud SKU.
var cache = builder.AddRedis("cache");

// Referencing a backing resource emits a Radius `connections` entry on the container in addition to
// the usual Aspire connection-string environment variable.
builder.AddContainer("web", "mcr.microsoft.com/azuredocs/aci-helloworld", "latest")
       .WithHttpEndpoint(targetPort: 80)
       .WithReference(cache);

builder.Build().Run();

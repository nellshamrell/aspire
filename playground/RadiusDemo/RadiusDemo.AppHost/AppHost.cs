// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// Radius is a publish/deploy target. It is intentionally inert in Run mode, so local
// development continues to use the normal Aspire container workflow without requiring
// Kubernetes or a Radius installation.
builder.AddRadiusEnvironment("radius")
       .WithNamespace("radius-demo");

// Use the same public image as the Radius CLI end-to-end test. ProjectResource image
// build and push support is not part of the Radius publisher yet, so a container keeps
// this introductory demo focused on the Aspire model -> Radius -> Kubernetes flow.
builder.AddContainer("web", "mcr.microsoft.com/azuredocs/aci-helloworld", "latest")
       .WithHttpEndpoint(targetPort: 80);

builder.Build().Run();

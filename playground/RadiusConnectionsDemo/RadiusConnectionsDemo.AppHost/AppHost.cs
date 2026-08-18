// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

builder.AddRadiusEnvironment("radius")
       .WithNamespace("radius-connections");

// Both workloads use the same public image so the demo stays focused on the wiring rather than on
// application code. The image runs a long-lived HTTP server and ships a shell and curl, which is
// what makes the `kubectl exec` verification in the README possible.
var backend = builder.AddContainer("backend", "mcr.microsoft.com/azuredocs/aci-helloworld", "latest")
                     .WithHttpEndpoint(targetPort: 80);

// WithReference is the only line that differs from the RadiusDemo walkthrough. A container is not a
// connection-string resource, so it is referenced by endpoint. Aspire resolves service discovery
// itself and emits `services__*` environment variables into the consumer; it does not emit a Radius
// `connections` entry. Radius `connections` are reserved for backing resources (see the
// RadiusRecipesDemo walkthrough).
builder.AddContainer("frontend", "mcr.microsoft.com/azuredocs/aci-helloworld", "latest")
       .WithHttpEndpoint(targetPort: 80)
       .WithReference(backend.GetEndpoint("http"));

builder.Build().Run();

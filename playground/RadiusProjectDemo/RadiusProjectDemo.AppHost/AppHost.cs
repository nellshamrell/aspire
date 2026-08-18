// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// WithContainerImage is experimental while the Radius publisher cannot build and push project
// images itself (https://github.com/microsoft/aspire/issues/16844).
#pragma warning disable ASPIRERADIUS057

var builder = DistributedApplication.CreateBuilder(args);

builder.AddRadiusEnvironment("radius")
       .WithNamespace("radius-project");

// A project is the resource an Aspire developer actually ships, but the Radius publisher does not
// build or push project images yet. Without the WithContainerImage line below, `aspire publish`
// fails with a remediation message instead of deploying a workload that would land in
// ImagePullBackOff. The README walks through that failure first, on purpose.
//
// The tag is explicit rather than `latest` because Kubernetes treats `latest` as an `Always` pull
// policy and the Radius container schema has no imagePullPolicy to override it. An explicit tag lets
// a KinD-loaded local image be used without contacting a registry.
//
// The reference is read from configuration so the same AppHost works against a local KinD cluster
// (the default, using an image loaded with `kind load docker-image`) and against a remote cluster
// that must pull from a registry:
//
//     aspire publish -- --ApiImage myregistry.azurecr.io/radiusdemo/apiservice:1.0
var apiImage = builder.Configuration["ApiImage"] ?? "radiusdemo/apiservice:1.0";

builder.AddProject<Projects.RadiusProjectDemo_ApiService>("apiservice")
       .WithContainerImage(apiImage);

builder.Build().Run();

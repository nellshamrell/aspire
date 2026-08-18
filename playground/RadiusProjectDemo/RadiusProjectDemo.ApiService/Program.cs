// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// The response reports the pod's hostname so the walkthrough can tell that the reply came from the
// Radius-managed workload in the cluster rather than from a locally running process.
app.MapGet("/", () => Results.Text($"Hello from {Environment.MachineName}"));

app.MapGet("/health", () => Results.Ok("healthy"));

app.Run();

// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep;

var builder = WebApplication.CreateBuilder(args);

// M0 is intentionally local-only. LAN binding waits for the pairing and transport
// design tracked in issue #6.
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5273));

var app = builder.Build();

app.MapGet("/api/health", () => new HealthResponse("ok", "Barkeep"))
    .WithName("GetHealth");

app.Run();

public partial class Program;

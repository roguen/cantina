// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json.Serialization;
using Cantina.Barkeep;
using Cantina.Barkeep.Yarg;
using Cantina.YargSession;

var builder = WebApplication.CreateBuilder(args);

// M0 is intentionally local-only. LAN binding waits for the pairing and transport
// design tracked in issue #6.
builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5273));

// Enums cross the wire as names, not ordinals: the iPad renders words, and the
// live-state contract's vocabulary (docs/live-state.md) is textual by design.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<YargSessionOptions>(
    builder.Configuration.GetSection(YargSessionOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<YargSessionTracker>();
builder.Services.AddHostedService<YargUdpListener>();
builder.Services.AddHostedService<CurrentSongPoller>();

var app = builder.Build();

app.MapGet("/api/health", () => new HealthResponse("ok", "Barkeep"))
    .WithName("GetHealth");

app.MapGet("/api/live", (YargSessionTracker tracker, TimeProvider clock) =>
        tracker.Snapshot(clock.GetUtcNow()))
    .WithName("GetLiveState");

app.Run();

public partial class Program;

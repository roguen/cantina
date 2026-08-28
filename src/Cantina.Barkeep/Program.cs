// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json.Serialization;
using Cantina.Barkeep;
using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

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
builder.Services.Configure<SetlistOptions>(
    builder.Configuration.GetSection(SetlistOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<YargSessionTracker>();
builder.Services.AddSingleton(provider => SetlistJournal.Open(
    provider.GetRequiredService<IOptions<SetlistOptions>>().Value.ResolveDataDirectory(),
    provider.GetRequiredService<TimeProvider>()));
builder.Services.AddHostedService<YargUdpListener>();
builder.Services.AddHostedService<CurrentSongPoller>();
builder.Services.Configure<YargCueOptions>(
    builder.Configuration.GetSection(YargCueOptions.SectionName));

// The cue pipeline needs synthetic input, which exists only on the target platform. On
// anything else the endpoint reports the named condition rather than pretending.
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IYargActuator, Win32YargActuator>();
    builder.Services.AddSingleton<YargCueService>();
    builder.Services.AddHostedService<CueConfirmationPoller>();
}

var app = builder.Build();

app.MapGet("/api/health", () => new HealthResponse("ok", "Barkeep"))
    .WithName("GetHealth");

app.MapGet("/api/live", (YargSessionTracker tracker, TimeProvider clock) =>
        tracker.Snapshot(clock.GetUtcNow()))
    .WithName("GetLiveState");

app.MapGet("/api/setlist", (SetlistJournal journal) => new SetlistView(
        journal.State,
        journal.RecoveredAmbiguous,
        journal.QuarantinedFiles))
    .WithName("GetSetlist");

// The mutation surface of D-023: the intent is journaled and flushed before this request
// is answered, and a duplicate command id is answered from the journal without
// re-applying. Replays return 200 with the recorded outcome rather than an error,
// because convergence is the point of idempotency.
app.MapPost("/api/setlist/commands", (SetlistIntent intent, SetlistJournal journal, TimeProvider clock) =>
    {
        if (string.IsNullOrWhiteSpace(intent.CommandId))
        {
            return Results.BadRequest(new CommandRejected("commandId is required"));
        }

        var applied = journal.Append(intent, clock, out var outcome);
        return Results.Ok(new CommandReceipt(intent.CommandId, outcome, Replayed: !applied));
    })
    .WithName("PostSetlistCommand");

// The cue surface: gate, actuate, verify by outcome (D-017, D-024). Pending resolution
// arrives by observation, so POST answers with the current status and GET follows it.
app.MapPost("/api/cue", (CueRequest request, IServiceProvider services) =>
    {
        if (string.IsNullOrWhiteSpace(request.CommandId) || request.Entry is null)
        {
            return Results.BadRequest(new CommandRejected("commandId and entry are required"));
        }

        var service = services.GetService<YargCueService>();

        if (service is null)
        {
            return Results.Ok(new CueStatus(
                request.CommandId, "refused", "cueing requires the Windows theater host",
                request.Entry, Loaded: null));
        }

        var query = string.IsNullOrWhiteSpace(request.Query) ? request.Entry.Title : request.Query;
        return Results.Ok(service.Cue(request with { Query = query }));
    })
    .WithName("PostCue");

app.MapGet("/api/cue/current", (IServiceProvider services) =>
    {
        var service = services.GetService<YargCueService>();
        return service?.Current is { } status ? Results.Ok(status) : Results.NoContent();
    })
    .WithName("GetCurrentCue");

app.Run();

public partial class Program;

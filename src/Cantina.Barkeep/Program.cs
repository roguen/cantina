// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cantina.Barkeep;
using Cantina.Barkeep.Access;
using Cantina.Barkeep.Library;
using Cantina.Barkeep.Network;
using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

// The web root is pinned to the binary's own directory, not the working directory.
// ASP.NET Core's default content root is `Directory.GetCurrentDirectory()`, so a Barkeep
// launched from a shortcut, a scheduled task, or any shell that happens to be somewhere
// else would look for its client bundle in that somewhere else and serve nothing. Measured
// on this host before it was believed: a published Barkeep started from the repository
// root answered 404 for its own front page.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
});

// Where Barkeep listens is decided once, before anything is built, so the certificate's
// subject names, the accepted Host headers, the accepted browser origins, and the firewall
// rule printed for the operator are all derived from the same answer (D-026). The default
// is still loopback: leaving it takes a deliberate configuration change.
var network = new NetworkOptions();
builder.Configuration.GetSection(NetworkOptions.SectionName).Bind(network);
var setlistPaths = new SetlistOptions();
builder.Configuration.GetSection(SetlistOptions.SectionName).Bind(setlistPaths);

var endpoints = TheaterEndpoints.Resolve(
    network,
    Environment.MachineName,
    builder.Environment.IsDevelopment());

var credentialDirectory = string.IsNullOrWhiteSpace(network.DataDirectory)
    ? setlistPaths.ResolveDataDirectory()
    : network.DataDirectory;

TheaterCertificates? certificates = null;

if (endpoints.Mode == BarkeepBinding.Lan)
{
    if (endpoints.LanAddress is null)
    {
        // Silently falling back to loopback would look like a working server that nobody
        // can reach; silently binding every interface would publish the theater to the
        // host's other networks. Neither is a choice this process gets to make.
        throw new InvalidOperationException(
            "Network:Mode is Lan but no routed IPv4 interface was found. Set Network:Address explicitly.");
    }

    certificates = TheaterCertificateAuthority.Ensure(
        credentialDirectory,
        endpoints.HostNames,
        endpoints.CertificateAddresses,
        network.LeafCertificateDays,
        TimeProvider.System.GetUtcNow());
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(endpoints.Port);

    if (certificates is not null && endpoints.LanAddress is not null)
    {
        // Plain HTTP on the LAN carries the onboarding surface and a redirect, nothing
        // else; the control surface is on the TLS port alone.
        options.Listen(endpoints.LanAddress, endpoints.Port);
        options.Listen(endpoints.LanAddress, endpoints.SecurePort,
            listener => listener.UseHttps(certificates.Server));
    }
});

// Host filtering is the DNS-rebinding defense: a name that resolves to this host but is
// not one Barkeep answers to is rejected before any endpoint sees it.
builder.Configuration["AllowedHosts"] = string.Join(';', endpoints.AllowedHosts);

// Enums cross the wire as names, not ordinals: the iPad renders words, and the
// live-state contract's vocabulary (docs/live-state.md) is textual by design.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.Configure<YargSessionOptions>(
    builder.Configuration.GetSection(YargSessionOptions.SectionName));
builder.Services.Configure<SetlistOptions>(
    builder.Configuration.GetSection(SetlistOptions.SectionName));
builder.Services.Configure<LibraryOptions>(
    builder.Configuration.GetSection(LibraryOptions.SectionName));
builder.Services.AddSingleton<SongIndex>();
builder.Services.AddHostedService<LibraryService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<YargSessionTracker>();
builder.Services.AddSingleton(provider => SetlistJournal.Open(
    provider.GetRequiredService<IOptions<SetlistOptions>>().Value.ResolveDataDirectory(),
    provider.GetRequiredService<TimeProvider>()));
builder.Services.AddHostedService<YargUdpListener>();
builder.Services.AddHostedService<CurrentSongPoller>();
builder.Services.Configure<YargCueOptions>(
    builder.Configuration.GetSection(YargCueOptions.SectionName));

builder.Services.AddSingleton(endpoints);
builder.Services.AddSingleton(DeviceRegistry.Open(credentialDirectory));
builder.Services.AddSingleton<PairingWindow>();
builder.Services.AddSingleton<LiveTicketStore>();

if (certificates is not null)
{
    builder.Services.AddSingleton(certificates);
}

// Rate limits exist for the two shapes of abuse this surface has: guessing a pairing code,
// and hammering a command. Both are partitioned so one caller cannot spend another's
// budget, and both answer 429 rather than failing open.
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    limiter.AddPolicy("pairing", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5) }));

    limiter.AddPolicy("commands", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Items[CantinaAccessMiddleware.DeviceItemKey] as string
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

// The cue pipeline needs synthetic input, which exists only on the target platform. On
// anything else the endpoint reports the named condition rather than pretending.
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IYargActuator, Win32YargActuator>();
    builder.Services.AddSingleton<YargCueService>();
    builder.Services.AddHostedService<CueConfirmationPoller>();
}

var app = builder.Build();

app.UseWebSockets();

// Every request is decided here, before any endpoint runs: origin, transport, and
// credential. See CantinaAccessMiddleware for the order and why it is that order.
app.UseMiddleware<CantinaAccessMiddleware>();
app.UseRateLimiter();

// A LAN binding announces itself once, in full, so the operator can see what was chosen
// rather than infer it — including the firewall rule, which Barkeep prints and never runs.
if (certificates is not null && endpoints.LanAddress is not null)
{
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Cantina.Barkeep.Network");
    var address = endpoints.LanAddress.ToString();
    AccessLog.LanBinding(log, address, endpoints.SecurePort);
    AccessLog.Onboarding(log, address, endpoints.Port);
    AccessLog.AuthorityFingerprint(log, certificates.AuthorityFingerprint);
    var firewall = endpoints.FirewallCommand(Environment.ProcessPath ?? "Cantina.Barkeep.exe");
    AccessLog.FirewallRule(log, firewall);

    var registry = app.Services.GetRequiredService<DeviceRegistry>();

    if (!registry.AnyPaired)
    {
        var opened = app.Services.GetRequiredService<PairingWindow>()
            .Open(TimeProvider.System.GetUtcNow(), TimeSpan.FromMinutes(10));
        AccessLog.NoDevicePaired(log);
        AccessLog.PairingCode(log, opened.Code, opened.ExpiresAt);
    }
}

// Barkeep serves the iPad its own client, so the theater PC is the only place the app
// comes from: no app store, no CDN, and nothing to install but a home-screen shortcut.
// The bundle is public — an unpaired iPad has to load it to have somewhere to type its
// pairing code — while every /api and /ws path behind it is not (D-026).
// Guarded on the bundle actually being there: during development the client is on the
// Vite dev server, and a server that throws at startup because a directory is missing is
// worse than one that says the app is not published here.
var bundled = !string.IsNullOrEmpty(app.Environment.WebRootPath) && Directory.Exists(app.Environment.WebRootPath);

if (bundled)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapGet("/api/health", () => new HealthResponse("ok", "Barkeep"))
    .WithName("GetHealth");

app.MapGet("/api/live", (YargSessionTracker tracker, TimeProvider clock) =>
        tracker.Snapshot(clock.GetUtcNow()))
    .WithName("GetLiveState");

// The push feed of the same projection, decimated and change-driven (docs/live-state.md).
app.Map("/ws/live", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var json = context.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>()
            .Value.SerializerOptions;
        await LiveStateSocket.RunAsync(
            socket,
            context.RequestServices.GetRequiredService<YargSessionTracker>(),
            context.RequestServices.GetRequiredService<TimeProvider>(),
            json,
            context.RequestAborted);
    });

// The library surface (D-025): plain-substring search over what the filesystem states,
// ranked title > artist > album > charter, each result carrying the folder path the cue
// pipeline joins on and the YARG hash once observation has taught it.
app.MapGet("/api/songs", (string? query, int? limit, SongIndex index) =>
        new SongSearchResponse(
            index.Search(query ?? string.Empty, Math.Clamp(limit ?? 50, 1, 200)),
            index.Count,
            index.LastScan))
    .WithName("SearchSongs");

app.MapPost("/api/library/rescan", (
        SongIndex index,
        Microsoft.Extensions.Options.IOptions<LibraryOptions> library,
        Microsoft.Extensions.Options.IOptions<YargSessionOptions> yarg,
        TimeProvider clock) =>
        index.Scan(library.Value.ResolveDirectories(yarg.Value.ResolveYargDirectory()), clock))
    .RequireRateLimiting("commands")
    .WithName("RescanLibrary");

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
    .RequireRateLimiting("commands")
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
    .RequireRateLimiting("commands")
    .WithName("PostCue");

app.MapGet("/api/cue/current", (IServiceProvider services) =>
    {
        var service = services.GetService<YargCueService>();
        return service?.Current is { } status ? Results.Ok(status) : Results.NoContent();
    })
    .WithName("GetCurrentCue");

// ── The access surface (D-026) ──────────────────────────────────────────────────────────
//
// Onboarding is readable by anything on the LAN and grants nothing: it names the service,
// the address to trust, and the fingerprint to compare. The pairing code is deliberately
// not here — it is shown at the theater PC, which is what makes physical presence the
// trust anchor.

app.MapGet("/api/onboarding", (
        TheaterEndpoints theater,
        DeviceRegistry registry,
        IServiceProvider services) =>
    {
        var issued = services.GetService<TheaterCertificates>();

        return new OnboardingDescription(
            "Barkeep",
            Environment.MachineName,
            theater.LanAddress is null
                ? $"http://localhost:{theater.Port}"
                : $"https://{theater.LanAddress}:{theater.SecurePort}",
            theater.HostNames,
            $"/{TheaterCertificateAuthority.AuthorityPublicFileName}",
            issued?.AuthorityFingerprint ?? string.Empty,
            registry.AnyPaired);
    })
    .WithName("GetOnboarding");

app.MapGet($"/{TheaterCertificateAuthority.AuthorityPublicFileName}", (IServiceProvider services) =>
    {
        var issued = services.GetService<TheaterCertificates>();

        return issued is null
            ? Results.NotFound()
            // iPadOS offers to install a downloaded profile only for this content type;
            // served as anything else it renders as gibberish and the trust step dead-ends.
            : Results.File(
                issued.AuthorityFilePath,
                "application/x-x509-ca-cert",
                TheaterCertificateAuthority.AuthorityPublicFileName);
    })
    .WithName("GetTheaterAuthority");

app.MapGet("/onboarding", (TheaterEndpoints theater, IServiceProvider services) =>
        Results.Content(
            OnboardingPage.Render(theater, services.GetService<TheaterCertificates>()),
            "text/html; charset=utf-8"))
    .WithName("GetOnboardingPage");

// Opening a pairing window is a loopback-only act, because standing at the theater PC is
// the credential. The code is returned here and logged there; it never crosses the LAN.
app.MapPost("/api/pairing/window", (HttpContext context, PairingWindow window, TimeProvider clock, ILoggerFactory logs) =>
    {
        if (!IsLocal(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var opened = window.Open(clock.GetUtcNow(), TimeSpan.FromMinutes(10));
        var pairingLog = logs.CreateLogger("Cantina.Barkeep.Access");
        AccessLog.PairingCode(pairingLog, opened.Code, opened.ExpiresAt);

        return Results.Ok(opened);
    })
    .WithName("OpenPairingWindow");

app.MapGet("/api/pairing/window", (HttpContext context, PairingWindow window, TimeProvider clock) =>
    {
        if (!IsLocal(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var current = window.Current(clock.GetUtcNow());
        return current is null ? Results.NoContent() : Results.Ok(current);
    })
    .WithName("GetPairingWindow");

app.MapDelete("/api/pairing/window", (HttpContext context, PairingWindow window) =>
    {
        if (!IsLocal(context))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        window.Close();
        return Results.NoContent();
    })
    .WithName("ClosePairingWindow");

// The one unauthenticated mutation on the LAN, and the only place a token is ever in
// plaintext. Five wrong codes close the window and the operator has to walk back to the PC.
app.MapPost("/api/pair", (
        PairingClaim claim,
        PairingWindow window,
        DeviceRegistry registry,
        TimeProvider clock) =>
    {
        var now = clock.GetUtcNow();
        var result = window.Redeem(claim.Code, now);

        if (result != PairingResult.Accepted)
        {
            return Results.Json(
                new PairingRefused(result),
                statusCode: result == PairingResult.TooManyAttempts
                    ? StatusCodes.Status429TooManyRequests
                    : StatusCodes.Status403Forbidden);
        }

        return Results.Ok(registry.Grant(claim.Label ?? "iPad", now));
    })
    .RequireRateLimiting("pairing")
    .WithName("PairDevice");

app.MapGet("/api/devices", (DeviceRegistry registry) => registry.Devices)
    .WithName("GetDevices");

// Revocation is the recovery path for a lost or replaced iPad, and it is immediate: the
// next request from that device fails the credential check, and its live socket dies with
// the next ticket it cannot get.
app.MapDelete("/api/devices/{deviceId}", (string deviceId, DeviceRegistry registry) =>
        registry.Revoke(deviceId) ? Results.NoContent() : Results.NotFound())
    .WithName("RevokeDevice");

// A browser cannot put a credential on a WebSocket, so a paired device spends its token
// here for a ticket good for one connection and thirty seconds.
app.MapPost("/api/live/ticket", (HttpContext context, LiveTicketStore tickets, TimeProvider clock) =>
    {
        var deviceId = context.Items[CantinaAccessMiddleware.DeviceItemKey] as string ?? "local";
        var (ticket, expiresAt) = tickets.Issue(deviceId, clock.GetUtcNow());
        return new LiveTicket(ticket, expiresAt);
    })
    .RequireRateLimiting("commands")
    .WithName("IssueLiveTicket");

// A single-page client owns its own routing, so an unknown path is the app, not a 404.
// API and socket paths are excluded: a mistyped endpoint must fail as an endpoint rather
// than silently return HTML that the caller will try to parse as JSON.
app.MapFallback(context =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/ws/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    var index = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");

    if (!bundled || !File.Exists(index))
    {
        // No bundle published. Say so rather than serving a blank page: during
        // development the client is on the Vite dev server, not here.
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }

    return context.Response.SendFileAsync(index);
});

app.Run();

static bool IsLocal(HttpContext context)
{
    var remote = context.Connection.RemoteIpAddress;
    return remote is null || IPAddress.IsLoopback(remote);
}

public partial class Program;

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Cantina.Barkeep;
using Cantina.Barkeep.Access;
using Cantina.Barkeep.Acquisition;
using Cantina.Barkeep.Library;
using Cantina.Barkeep.Network;
using Cantina.Barkeep.Providers;
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

    // A supplied certificate wins, and when one is configured no theater authority is
    // created at all - not even as a spare. Two certificates on disk, one of them unused,
    // is a private key nobody is watching (D-029).
    certificates = string.IsNullOrWhiteSpace(network.CertificatePath)
        ? TheaterCertificateAuthority.Ensure(
            credentialDirectory,
            endpoints.HostNames,
            endpoints.CertificateAddresses,
            network.LeafCertificateDays,
            TimeProvider.System.GetUtcNow())
        : TheaterCertificateAuthority.LoadSupplied(
            network.CertificatePath, network.CertificatePassword, network.CertificateKeyPath);
}

// The certificate is held behind a source that can swap it, because Kestrel reads its
// certificate once at startup and a renewal delivered afterwards would otherwise change
// nothing until someone restarted Barkeep - which is to say, until the old one expired
// (D-029).
var certificateSource = certificates is null
    ? null
    : new TheaterCertificateSource(
        certificates,
        network,
        LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Cantina.Barkeep.Network"));

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenLocalhost(endpoints.Port);

    if (certificateSource is not null && endpoints.LanAddress is not null)
    {
        // Plain HTTP on the LAN carries the onboarding surface and a redirect, nothing
        // else; the control surface is on the TLS port alone.
        options.Listen(endpoints.LanAddress, endpoints.Port);
        // The selection callback runs per connection and reads whatever the source holds
        // now, so a renewal takes effect on the next handshake rather than the next restart.
        // The certificate context carries the intermediates with the leaf: a client that
        // does not already hold Let's Encrypt's intermediate cannot build a path to the
        // root, and serving the leaf alone works on whichever machine you tested on.
        options.Listen(endpoints.LanAddress, endpoints.SecurePort, listener =>
            listener.UseHttps(
                (_, _, state, _) => ValueTask.FromResult(((TheaterCertificateSource)state!).ServerOptions()),
                certificateSource));
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
builder.Services.AddSingleton(provider => FavoritesStore.Open(
    provider.GetRequiredService<IOptions<SetlistOptions>>().Value.ResolveDataDirectory()));
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
builder.Services.Configure<DebugOptions>(
    builder.Configuration.GetSection(DebugOptions.SectionName));
builder.Services.Configure<AdvanceOptions>(
    builder.Configuration.GetSection(AdvanceOptions.SectionName));
builder.Services.Configure<EncoreOptions>(
    builder.Configuration.GetSection(EncoreOptions.SectionName));

// The Chorus Encore integration (D-032). One named client, self-identifying: the
// provider is donation-funded and publishes no API terms, so the User-Agent is how its
// operator can see Cantina, reach out, or block it by name. The generous timeout is for
// chart downloads, which are large; searches bound themselves with a shorter token.
builder.Services.AddHttpClient(EncoreClient.HttpClientName, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Cantina/1.0 (+https://github.com/roguen/cantina)");
    client.Timeout = TimeSpan.FromMinutes(15);
});
builder.Services.AddSingleton<EncoreClient>();
builder.Services.AddSingleton<EncoreDownloadCoordinator>();

builder.Services.Configure<NetworkOptions>(
    builder.Configuration.GetSection(NetworkOptions.SectionName));
builder.Services.AddSingleton(endpoints);
builder.Services.AddSingleton(DeviceRegistry.Open(credentialDirectory));
builder.Services.AddSingleton<PairingWindow>();
builder.Services.Configure<PairingEmailOptions>(
    builder.Configuration.GetSection(PairingEmailOptions.SectionName));
builder.Services.AddSingleton<IPairingMailTransport, SmtpPairingMailTransport>();
builder.Services.AddSingleton<PairingEmailService>();
builder.Services.AddSingleton<LiveTicketStore>();

if (certificateSource is not null)
{
    builder.Services.AddSingleton(certificateSource);
    builder.Services.AddHostedService<CertificateRenewalWatcher>();
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
    builder.Services.AddSingleton<ActuationGate>();
    builder.Services.AddSingleton<YargCueService>();
    builder.Services.AddSingleton<PlayerStandInService>();
    builder.Services.AddSingleton<ScoreAdvanceService>();
    builder.Services.AddHostedService<CueConfirmationPoller>();
    builder.Services.AddHostedService<ScoreAdvancePoller>();

    // Acquisition: the Geomitron Bridge filesystem handoff (D-007, D-030). Off unless a
    // watch directory is named, and Windows-only because the refresh drives YARG's menus.
    builder.Services.Configure<AcquisitionOptions>(
        builder.Configuration.GetSection(AcquisitionOptions.SectionName));

    if (!string.IsNullOrWhiteSpace(
        builder.Configuration[$"{AcquisitionOptions.SectionName}:WatchDirectory"]))
    {
        builder.Services.AddSingleton<ISongArrivalPort, FileArrivalPort>();
        builder.Services.AddSingleton<ISongIndexPort, LibraryIndexPort>();
        builder.Services.AddSingleton<IYargSessionPort, TrackerSessionPort>();
        builder.Services.AddSingleton<ISetlistPort, JournalSetlistPort>();
        builder.Services.AddSingleton(provider => AcquisitionJournal.Open(
            provider.GetRequiredService<IOptions<SetlistOptions>>().Value.ResolveDataDirectory()));
        builder.Services.AddSingleton<IImportPlayNextJournal>(provider =>
            provider.GetRequiredService<AcquisitionJournal>());
        builder.Services.AddSingleton<ImportPlayNextCoordinator>(provider =>
            new ImportPlayNextCoordinator(
                provider.GetRequiredService<ISongArrivalPort>(),
                provider.GetRequiredService<ISongIndexPort>(),
                provider.GetRequiredService<IYargSessionPort>(),
                provider.GetRequiredService<ISetlistPort>(),
                provider.GetRequiredService<IImportPlayNextJournal>(),
                provider.GetRequiredService<TimeProvider>(),
                new ImportPlayNextOptions
                {
                    // The real handoff is a cross-volume move that can take a while to
                    // stabilize; the harness default of 3 probes fits fakes, not disks.
                    MaximumStabilizationAttempts = 40,
                }));
        builder.Services.AddSingleton<AcquisitionWatcher>();
        builder.Services.AddHostedService(provider =>
            provider.GetRequiredService<AcquisitionWatcher>());
    }
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
    if (certificates.AuthorityFingerprint is { } fingerprint)
    {
        AccessLog.AuthorityFingerprint(log, fingerprint);
    }
    else
    {
        AccessLog.SuppliedCertificate(log, certificates.Server.Subject);
    }

    var daysLeft = certificates.DaysUntilExpiry(TimeProvider.System.GetUtcNow());

    if (daysLeft <= network.CertificateWarnDays)
    {
        AccessLog.CertificateExpiring(log, daysLeft, certificates.NotAfter);
    }
    else
    {
        AccessLog.CertificateValid(log, daysLeft, certificates.NotAfter);
    }
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

app.MapGet("/api/health", (IServiceProvider services, TimeProvider clock) =>
    {
        var issued = services.GetService<TheaterCertificateSource>()?.Current;

        if (issued is null)
        {
            // Loopback-only serves no TLS, so there is no certificate to report on. Null
            // rather than a fabricated "ok": absent and healthy are different facts.
            return new HealthResponse("ok", "Barkeep", null);
        }

        var days = issued.DaysUntilExpiry(clock.GetUtcNow());
        var warnDays = services.GetRequiredService<IOptions<NetworkOptions>>().Value.CertificateWarnDays;

        return new HealthResponse("ok", "Barkeep", new CertificateHealth(
            issued.NeedsDeviceTrust ? "theater-authority" : "supplied",
            issued.NeedsDeviceTrust,
            issued.NotAfter,
            days,
            days < 0 ? "expired" : days <= warnDays ? "expiring" : "ok"));
    })
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

// Starred songs: the filter that narrows the library to what the house plays.
app.MapGet("/api/favorites", (FavoritesStore favorites) => Results.Ok(favorites.All))
    .WithName("GetFavorites");

app.MapPost("/api/favorites", (FavoriteRequest request, FavoritesStore favorites) =>
    {
        if (string.IsNullOrWhiteSpace(request.Location))
        {
            return Results.BadRequest(new CommandRejected("location is required"));
        }

        favorites.Set(request.Location, request.Favored);
        return Results.Ok(favorites.All);
    })
    .RequireRateLimiting("commands")
    .WithName("SetFavorite");

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

// What arrived through the Geomitron Bridge handoff and what became of each item — the
// honest acquisition progress the roadmap promises the iPad. Empty when acquisition is
// not configured, which is itself information.
app.MapGet("/api/acquisition/recent", (IServiceProvider services) =>
    {
        var watcher = services.GetService<AcquisitionWatcher>();
        return watcher is null
            ? Results.Ok(Array.Empty<AcquisitionRecord>())
            : Results.Ok(watcher.Recent);
    })
    .WithName("GetRecentAcquisitions");

app.MapGet("/api/cue/current", (IServiceProvider services) =>
    {
        var service = services.GetService<YargCueService>();
        return service?.Current is { } status ? Results.Ok(status) : Results.NoContent();
    })
    .WithName("GetCurrentCue");

// ── The score-screen advance (#39) ──────────────────────────────────────────────────────
//
// Armed from the iPad, off at startup. While armed, a score screen with a next setlist
// entry gets a players-first grace period, then one CONTINUE, then the ordinary cue
// pipeline for the next song — same gates, same journal, same verify-by-outcome.

app.MapGet("/api/advance", (IServiceProvider services) =>
    {
        var advance = services.GetService<ScoreAdvanceService>();
        return advance is null
            ? Results.Ok(new AdvanceStatus(false, "Unavailable", "auto-advance requires the Windows theater host", DateTimeOffset.MinValue))
            : Results.Ok(advance.Status);
    })
    .WithName("GetAdvanceStatus");

app.MapPost("/api/advance", (AdvanceArmRequest request, IServiceProvider services) =>
    {
        var advance = services.GetService<ScoreAdvanceService>();
        return advance is null
            ? Results.Ok(new AdvanceStatus(false, "Unavailable", "auto-advance requires the Windows theater host", DateTimeOffset.MinValue))
            : Results.Ok(advance.SetEnabled(request.Enabled));
    })
    .RequireRateLimiting("commands")
    .WithName("SetAdvanceStatus");

// ── The debug surface ───────────────────────────────────────────────────────────────────
//
// Bench testing without players: stand in for their ready confirms at instrument setup so
// a cue can be driven to gameplay from the iPad alone. Config-gated (Debug:Enabled) and
// invisible when off — both endpoints answer 404, and the client never draws the section.
// Behind the same bearer auth as every other /api route.

app.MapGet("/api/debug", (IOptions<DebugOptions> debug) =>
        debug.Value.Enabled
            ? Results.Ok(new DebugView(true, debug.Value.PlayerConfirmations))
            : Results.NotFound())
    .WithName("GetDebugView");

app.MapPost("/api/debug/players", (IServiceProvider services, IOptions<DebugOptions> debug) =>
    {
        if (!debug.Value.Enabled)
        {
            return Results.NotFound();
        }

        var standIn = services.GetService<PlayerStandInService>();

        return standIn is null
            ? Results.Ok(new StandInStatus("refused", "standing in for players requires the Windows theater host"))
            : Results.Ok(standIn.Confirm());
    })
    .RequireRateLimiting("commands")
    .WithName("PostPlayerStandIn");

// ── The chart-provider surface (D-032) ──────────────────────────────────────────────────
//
// Search and download against Chorus Encore, the same two endpoints its own desktop
// client speaks. Off means invisible (404), and every refusal downstream is named. The
// download hands the file to the D-030 acquisition pipeline — there is no second import
// path, so everything after delivery is the proven one.

app.MapGet("/api/provider", (IOptions<EncoreOptions> encore) =>
        encore.Value.Enabled ? Results.Ok(new ProviderView(true)) : Results.NotFound())
    .WithName("GetProviderView");

app.MapGet("/api/provider/search", async (string? q, EncoreClient encore, SongIndex index, IOptions<EncoreOptions> providerOptions) =>
    {
        if (!providerOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new CommandRejected("q is required"));
        }

        using var bound = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            var result = await encore.SearchAsync(q.Trim(), bound.Token);

            // Say "already in your library" on the result, not after a failed download
            // (operator feedback, 2026-08-30). Same title and artist is the honest
            // heuristic the operator themselves would use; a different charter's take
            // on the same song still shows as present, and the download stays allowed.
            var marked = result.Charts.Select(chart =>
                index.Search(chart.Name, 25).Any(song =>
                    string.Equals(song.Title, chart.Name, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(song.Artist, chart.Artist, StringComparison.OrdinalIgnoreCase))
                    ? chart with { InLibrary = true }
                    : chart).ToList();

            return Results.Ok(result with { Charts = marked });
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            return Results.Ok(new EncoreSearchResult(0, [],
                $"the search could not reach Chorus Encore: {error.Message}"));
        }
    })
    .RequireRateLimiting("commands")
    .WithName("GetProviderSearch");

app.MapPost("/api/provider/download", (EncoreChart chart, EncoreDownloadCoordinator downloads, IOptions<EncoreOptions> providerOptions) =>
    {
        if (!providerOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        // The md5 becomes a URL segment and a file name; nothing but a literal chart
        // hash may pass.
        if (chart.Md5 is not { Length: 32 } || !chart.Md5.All(char.IsAsciiHexDigitLower))
        {
            return Results.BadRequest(new CommandRejected("md5 must be 32 lowercase hex characters"));
        }

        if (string.IsNullOrWhiteSpace(chart.Name))
        {
            return Results.BadRequest(new CommandRejected("name is required"));
        }

        return Results.Ok(downloads.Request(chart));
    })
    .RequireRateLimiting("commands")
    .WithName("PostProviderDownload");

app.MapGet("/api/provider/downloads", (EncoreDownloadCoordinator downloads, IOptions<EncoreOptions> providerOptions) =>
        providerOptions.Value.Enabled ? Results.Ok(downloads.Recent) : Results.NotFound())
    .WithName("GetProviderDownloads");

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
        var issued = services.GetService<TheaterCertificateSource>()?.Current;

        // The preferred name, not the address, when the binding has one: a real name is what
        // the certificate is issued for and what the operator typed into DNS (D-029).
        var host = theater.HostNames.FirstOrDefault(name =>
                       name.Contains('.', StringComparison.Ordinal) &&
                       !name.EndsWith(".local", StringComparison.Ordinal))
                   ?? theater.LanAddress?.ToString();

        var secure = host is null
            ? $"http://localhost:{theater.Port}"
            : theater.SecurePort == 443 ? $"https://{host}" : $"https://{host}:{theater.SecurePort}";

        return new OnboardingDescription(
            "Barkeep",
            Environment.MachineName,
            secure,
            theater.HostNames,
            $"/{TheaterCertificateAuthority.AuthorityPublicFileName}",
            issued?.AuthorityFingerprint ?? string.Empty,
            issued?.NeedsDeviceTrust ?? false,
            registry.AnyPaired);
    })
    .WithName("GetOnboarding");

app.MapGet($"/{TheaterCertificateAuthority.AuthorityPublicFileName}", (IServiceProvider services) =>
    {
        var issued = services.GetService<TheaterCertificateSource>()?.Current;

        return issued?.AuthorityFilePath is null
            ? Results.NotFound()
            // iPadOS offers to install a downloaded profile only for this content type;
            // served as anything else it renders as gibberish and the trust step dead-ends.
            : Results.File(
                issued.AuthorityFilePath!,
                "application/x-x509-ca-cert",
                TheaterCertificateAuthority.AuthorityPublicFileName);
    })
    .WithName("GetTheaterAuthority");

app.MapGet("/onboarding", (TheaterEndpoints theater, IServiceProvider services) =>
        Results.Content(
            OnboardingPage.Render(theater, services.GetService<TheaterCertificateSource>()?.Current),
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

// Emailed pairing codes (D-033). This is the one pre-auth mutation besides /api/pair
// itself, and it widens the trust anchor deliberately: "can read the operator's inbox"
// stands in for "is standing at the theater PC". The compensating controls: the
// destination is operator configuration and never client input, the ceiling is small,
// the requester is named in the message, and the console still prints every code.
app.MapGet("/api/pairing/email", (IOptions<PairingEmailOptions> email) =>
        email.Value.Enabled ? Results.Ok(new PairingEmailView(true)) : Results.NotFound())
    .WithName("GetPairingEmailView");

app.MapPost("/api/pairing/email", async (
        HttpContext context,
        PairingEmailRequest request,
        PairingEmailService email,
        IOptions<PairingEmailOptions> emailOptions,
        PairingWindow window,
        TimeProvider clock,
        ILoggerFactory logs) =>
    {
        if (!emailOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        var requester = context.Connection.RemoteIpAddress?.ToString() ?? "unknown address";
        var status = await email.RequestAsync(requester, request.Email, context.RequestAborted);

        if (status.State == "sent" && window.Current(clock.GetUtcNow()) is { } state)
        {
            // The console record stays authoritative: every live code is printed there.
            var pairingLog = logs.CreateLogger("Cantina.Barkeep.Access");
            AccessLog.PairingCode(pairingLog, state.Code, state.ExpiresAt);
        }

        return Results.Ok(status);
    })
    .RequireRateLimiting("pairing")
    .WithName("RequestPairingEmail");

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

// The device's own registered name - "iPad Mini" on the iPad Mini - so the masthead
// can say who this screen is, from the server's registry rather than a local guess.
app.MapGet("/api/device", (HttpContext context, DeviceRegistry registry) =>
    {
        var deviceId = context.Items[CantinaAccessMiddleware.DeviceItemKey] as string;
        var device = registry.Devices.FirstOrDefault(paired => paired.DeviceId == deviceId);
        return Results.Ok(new DeviceView(device?.Label ?? "this screen"));
    })
    .WithName("GetOwnDevice");

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

    // The content type has to be set explicitly. SendFileAsync does not infer one, and
    // Barkeep sends X-Content-Type-Options: nosniff, so a browser is both unable to guess
    // and forbidden from trying: Safari offered to *download* the home page rather than
    // render it. curl reported 200 and said nothing, which is why this survived every
    // automated check and failed on the first real device.
    context.Response.ContentType = "text/html; charset=utf-8";
    return context.Response.SendFileAsync(index);
});

app.Run();

static bool IsLocal(HttpContext context)
{
    var remote = context.Connection.RemoteIpAddress;
    return remote is null || IPAddress.IsLoopback(remote);
}

public partial class Program;

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Cantina.SelfTest;

/// <summary>
/// What the iPad actually waits for, measured on the theater PC against a running Barkeep.
///
/// Three numbers are measured and one is derived, and the difference is stated rather than
/// blurred:
///
/// <list type="bullet">
/// <item><b>Delivered-state age</b> — how old the YARG state in a frame is by the time the
/// client has it. This is what "the banner is current" means in milliseconds.</item>
/// <item><b>Command round trip</b> — a setlist command, including D-023's write-ahead flush
/// to disk, because the intent is on the platter before the request is answered.</item>
/// <item><b>Search</b> — a query across the real index, not a fixture.</item>
/// <item><b>Change latency</b> — <i>derived, not measured</i>. The socket polls the tracker
/// every 250 ms and pushes on change, so a scene change reaches the client within that poll
/// plus the delivered-state age. Measuring it directly needs a scene change, which needs
/// input, which this suite does not send.</item>
/// </list>
///
/// The ceilings below are regression tripwires, not targets. They are set far enough above
/// the design values that crossing one means something changed, and every measured number
/// is printed whether it passes or not.
/// </summary>
internal static class LatencySuite
{
    private const string Name = "latency";

    // The socket's poll interval (LiveStateSocket) is 250 ms; anything past 400 ms means
    // the decimation is not behaving as designed rather than merely being slow.
    private static readonly TimeSpan DeliveredAgeCeiling = TimeSpan.FromMilliseconds(400);

    // A journal append is a flush to local storage. A quarter second is generous by orders
    // of magnitude and would still catch a fsync-per-byte regression.
    private static readonly TimeSpan CommandCeiling = TimeSpan.FromMilliseconds(250);

    // In-memory substring search over a few hundred songs (D-025).
    private static readonly TimeSpan SearchCeiling = TimeSpan.FromMilliseconds(100);

    private static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(25);

    public static async Task<SuiteResult> RunAsync(Transcript transcript, string origin)
    {
        using var client = new HttpClient { BaseAddress = new Uri(origin) };

        try
        {
            using var health = await client.GetAsync("/api/health").ConfigureAwait(false);
            health.EnsureSuccessStatusCode();
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        {
            transcript.Log("INCONCLUSIVE", $"{Name}: barkeep-not-running at {origin}");
            return new SuiteResult(Name, Verdict.Inconclusive, "Barkeep was not answering, so there was nothing to time.");
        }

        var cases = new List<bool>();
        var inconclusive = false;

        // 1. Search, over whatever this host's library actually holds.
        var searches = new List<double>();

        // The queries repeat deliberately: the first request through any path pays for JIT
        // and the connection, which is a real cost to the first tap after a cold start and
        // a misleading one for every tap after it. Both are reported, separately.
        foreach (var query in new[]
        {
            "the", "a", "metallica", "zzzz-no-such-song", "un", "unforgiven", "trivium",
            "e", "guitar", "hero", "of", "black", "one", "master", "sad", "but", "true",
        })
        {
            var clock = Stopwatch.StartNew();
            using var response = await client.GetAsync($"/api/songs?query={query}&limit=50").ConfigureAwait(false);
            _ = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            searches.Add(clock.Elapsed.TotalMilliseconds);
        }

        cases.Add(Report(transcript, "search", searches, SearchCeiling));

        // 2. The command path, flush included. Each command carries its own id so nothing
        //    is replayed from the journal and every timing is a real append.
        //
        //    This probe runs into Barkeep's own rate limit if it is not careful, and the
        //    first version did: 20 adds plus 20 removes is 40 of the 60-per-minute
        //    `commands` budget, so a second run inside the same minute was refused with 429
        //    — which the loop ignored. Two things went wrong at once and both are guarded
        //    now. The cleanup silently failed, leaving 20 probe entries in the setlist. And
        //    worse, **a 429 returns fast**, so rate-limited requests would have made these
        //    latency numbers look better than the truth. A measurement that gets quicker as
        //    it is refused is a measurement that has to check what it is timing.
        var before = await SetlistCountAsync(client).ConfigureAwait(false);
        var commands = new List<double>();
        var refused = 0;

        for (var index = 0; index < 20; index++)
        {
            var body = $$$"""{"commandId":"latency-{{{index}}}-{{{Environment.ProcessId}}}","kind":"Add","entry":{"hash":"latency","title":"Latency probe","artist":"Cantina.SelfTest"}}""";
            var clock = Stopwatch.StartNew();
            using var response = await client
                .PostAsync("/api/setlist/commands", new StringContent(body, Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);
            _ = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                refused++;
                continue;
            }

            commands.Add(clock.Elapsed.TotalMilliseconds);
        }

        // Undo what the probe added. Remove targets a hash and takes the first match, so
        // twenty of these clear the twenty the probe added and nothing else — never Clear,
        // which would take the operator's setlist with it.
        for (var index = 0; index < 20; index++)
        {
            var body = $$$"""{"commandId":"latency-undo-{{{index}}}-{{{Environment.ProcessId}}}","kind":"Remove","hash":"latency"}""";
            using var undo = await client
                .PostAsync("/api/setlist/commands", new StringContent(body, Encoding.UTF8, "application/json"))
                .ConfigureAwait(false);
            _ = await undo.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!undo.IsSuccessStatusCode)
            {
                refused++;
            }
        }

        var after = await SetlistCountAsync(client).ConfigureAwait(false);

        if (refused > 0)
        {
            // Named, not shrugged at: the numbers from this run cannot be trusted and the
            // setlist may not be as it was found.
            cases.Add(Case(transcript, "command-round-trip", false,
                $"{refused} of 40 commands were refused, almost certainly Barkeep's own 60/minute "
                + "rate limit; the timings are invalid. Wait a minute between runs."));
        }
        else
        {
            cases.Add(Report(transcript, "command-round-trip", commands, CommandCeiling));
        }

        // Verified by outcome rather than by having sent the right requests (D-015's rule
        // applies to this harness too).
        cases.Add(Case(transcript, "probe-leaves-the-setlist-as-it-found-it", after == before,
            $"entries {before} -> {after}"));

        // 3. The observation path, which needs YARG to actually be broadcasting.
        var live = await client.GetFromJsonAsync<JsonElement>("/api/live").ConfigureAwait(false);
        var freshness = live.TryGetProperty("freshness", out var value) ? value.GetString() : null;

        if (freshness != "Live")
        {
            transcript.Log("INCONCLUSIVE", $"{Name}: yarg-not-observable (freshness={freshness}); the delivered-state age needs a live wire");
            inconclusive = true;
        }
        else
        {
            var ages = await DeliveredAgesAsync(transcript, origin).ConfigureAwait(false);

            if (ages.Count == 0)
            {
                transcript.Log("INCONCLUSIVE", $"{Name}: no-frames-received in {ObservationWindow.TotalSeconds:0} s");
                inconclusive = true;
            }
            else
            {
                cases.Add(Report(transcript, "delivered-state-age", ages, DeliveredAgeCeiling));
                transcript.Case(Name, "change-latency-derived", pass: true,
                    $"not measured: bounded by the socket's 250 ms poll plus the age above, "
                    + $"so ≤ {250 + Percentile(ages, 0.95):0} ms at p95. Measuring it directly needs a scene change.");
            }
        }

        var verdict = !cases.All(passed => passed) ? Verdict.Fail
            : inconclusive ? Verdict.Inconclusive
            : Verdict.Pass;

        transcript.Log("VERDICT", $"{Name}: {verdict} cases={cases.Count(passed => passed)}/{cases.Count}");

        return new SuiteResult(Name, verdict,
            "The iPad's waits, measured on this host: search, the journaled command round trip, and how stale delivered state is.");
    }

    private static async Task<List<double>> DeliveredAgesAsync(Transcript transcript, string origin)
    {
        var ages = new List<double>();
        var socketUri = new Uri(new Uri(origin.Replace("http://", "ws://", StringComparison.Ordinal)), "/ws/live");

        using var socket = new ClientWebSocket();
        using var window = new CancellationTokenSource(ObservationWindow);

        try
        {
            await socket.ConnectAsync(socketUri, window.Token).ConfigureAwait(false);
            var buffer = new byte[16 * 1024];

            while (!window.IsCancellationRequested)
            {
                var frame = await socket.ReceiveAsync(buffer, window.Token).ConfigureAwait(false);
                var arrived = DateTimeOffset.UtcNow;

                using var state = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, frame.Count));

                if (state.RootElement.TryGetProperty("receivedAt", out var receivedAt) &&
                    receivedAt.ValueKind != JsonValueKind.Null)
                {
                    ages.Add((arrived - receivedAt.GetDateTimeOffset()).TotalMilliseconds);
                }
            }
        }
        catch (Exception error) when (error is OperationCanceledException or WebSocketException)
        {
            // The window closing is the normal end of this measurement.
        }

        transcript.Log("SETUP", $"{Name}: frames={ages.Count} over {ObservationWindow.TotalSeconds:0} s "
            + "(the socket pushes on change plus a 5 s heartbeat, so a still game is quiet by design)");

        return ages;
    }

    /// <summary>
    /// Reports the first sample apart from the rest. The first request through a path pays
    /// for JIT and connection setup — a real cost to the first tap after a cold start, and
    /// a misleading one for every tap after it. The ceiling applies to steady state, and
    /// the first-request number is printed beside it rather than hidden in it.
    /// </summary>
    private static bool Report(Transcript transcript, string name, List<double> samples, TimeSpan ceiling)
    {
        var steady = samples.Count > 1 ? samples.Skip(1).ToList() : samples;
        var p95 = Percentile(steady, 0.95);
        var pass = p95 <= ceiling.TotalMilliseconds;

        transcript.Case(Name, name, pass,
            $"first={samples[0]:0.0} ms | steady n={steady.Count} p50={Percentile(steady, 0.50):0.0} ms "
            + $"p95={p95:0.0} ms max={steady.Max():0.0} ms (ceiling {ceiling.TotalMilliseconds:0} ms)");

        return pass;
    }

    private static async Task<int> SetlistCountAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/setlist").ConfigureAwait(false);
        using var view = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return view.RootElement.GetProperty("state").GetProperty("entries").GetArrayLength();
    }

    private static bool Case(Transcript transcript, string name, bool pass, string detail)
    {
        transcript.Case(Name, name, pass, detail);
        return pass;
    }

    private static double Percentile(List<double> samples, double fraction)
    {
        var ordered = samples.Order().ToList();
        var rank = (int)Math.Ceiling(fraction * ordered.Count) - 1;
        return ordered[Math.Clamp(rank, 0, ordered.Count - 1)];
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using Cantina.SelfTest;

// The target-PC acceptance harness. Run it on the theater PC:
//
//   dotnet run --project tools/Cantina.SelfTest -- run all
//
// Suites:
//   journal    D-023's crash matrix with REAL process kills - a child races journal
//              appends and the parent kills it hard, so torn writes are produced by
//              actual kills rather than simulated file shapes.
//   live       The Cantina.YargSession library against the real YARG broadcast.
//   readiness  The five D-024 readiness signals, read-only.
//   latency    What the iPad waits for: search, the journaled command round trip, and
//              how stale delivered state is when it arrives. Needs Barkeep running; the
//              observation half also needs YARG broadcasting.
//   lan        The D-026 transport against a Barkeep actually bound to the LAN: the
//              handshake, the chain to the theater authority, pairing, the live socket's
//              ticket, reconnection, and revocation. INCONCLUSIVE when Barkeep is
//              loopback-only, which is the default.
//
// Verdicts follow the house rule: PASS, FAIL, or INCONCLUSIVE with a named cause -
// a suite that cannot establish its preconditions says so rather than guessing.
// Exit codes: 0 all pass, 1 any fail, 2 no failures but something inconclusive.
//
// Only `run cue` sends input, and the transcript header says so per run. This assembly
// does link SendInput and SetForegroundWindow, because the cue suite needs them - so it
// cannot offer the assembly-level innocence that Cantina.Spikes.YargSetlist can, and it
// says the weaker true thing rather than the stronger false one. Every other suite reads.

if (args.Length >= 1 && args[0] == "journal-child")
{
    return JournalCrashChild.Run(args);
}

if (args.Length < 2 || args[0] != "run")
{
    Console.WriteLine("usage: Cantina.SelfTest run <all|journal|live|readiness|latency|lan|cue>");
    Console.WriteLine();
    Console.WriteLine("cue options (defaults are this theater library, measured in D-017/D-018):");
    Console.WriteLine("  --query <text>   search text to type       (default: unforgiven)");
    Console.WriteLine("  --hash <base64>  expected Hash.HashBytes   (default: The Unforgiven)");
    Console.WriteLine("  --title <text>   display title             (default: The Unforgiven)");
    Console.WriteLine();
    Console.WriteLine("The cue suite SENDS INPUT and starts a real song, then pauses it. It is not");
    Console.WriteLine("part of run all; run it deliberately.");
    return 2;
}

var transcript = new Transcript();
var which = args[1];
transcript.Banner(runSendsInput: which is "cue");

var results = new List<SuiteResult>();

if (which is "all" or "journal")
{
    results.Add(await JournalCrashSuite.RunAsync(transcript).ConfigureAwait(false));
}

if (which is "all" or "live")
{
    results.Add(await LiveObservationSuite.RunAsync(transcript).ConfigureAwait(false));
}

if (which is "all" or "readiness")
{
    results.Add(ReadinessSuite.Run(transcript));
}

if (which is "all" or "latency")
{
    results.Add(await LatencySuite.RunAsync(transcript, "http://localhost:5273").ConfigureAwait(false));
}

if (which is "all" or "lan")
{
    results.Add(await LanTransportSuite.RunAsync(transcript, "http://localhost:5273").ConfigureAwait(false));
}

if (which is "confirmdiag")
{
    // Reproduces the cue confirm loop in isolation against whatever is running now.
    var diagTracker = new Cantina.YargSession.YargSessionTracker();
    using var diagSocket = new System.Net.Sockets.Socket(
        System.Net.Sockets.AddressFamily.InterNetwork,
        System.Net.Sockets.SocketType.Dgram,
        System.Net.Sockets.ProtocolType.Udp);
    diagSocket.SetSocketOption(
        System.Net.Sockets.SocketOptionLevel.Socket,
        System.Net.Sockets.SocketOptionName.ReuseAddress, true);
    diagSocket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 36107));
    using var diagCts = new CancellationTokenSource();
    var diagBuffer = new byte[512];
    var diagPump = Task.Run(async () =>
    {
        var endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0);
        while (!diagCts.IsCancellationRequested)
        {
            var r = await diagSocket.ReceiveFromAsync(diagBuffer, System.Net.Sockets.SocketFlags.None, endpoint, diagCts.Token);
            diagTracker.OnDatagram(diagBuffer.AsSpan(0, r.ReceivedBytes), r.RemoteEndPoint.ToString() ?? "?", DateTimeOffset.UtcNow);
        }
    });
    var diagPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "YARC", "YARG", "release", "currentSong.json");
    for (var i = 0; i < 30; i++)
    {
        var content = File.Exists(diagPath) ? await File.ReadAllTextAsync(diagPath, diagCts.Token) : null;
        diagTracker.OnCurrentSong(content);
        var snap = diagTracker.Snapshot(DateTimeOffset.UtcNow);
        if (i % 10 == 0)
        {
            transcript.Log("DIAG",
                $"read={(content is null ? "absent" : content.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))} "
                + $"scene={snap.Scene} song={(snap.Song is { } ds ? ds.Hash : "null")}");
        }
        await Task.Delay(100);
    }
    await diagCts.CancelAsync();
    try { await diagPump; } catch (OperationCanceledException) { }
    return 0;
}

if (which is "cue")
{
    var query = ArgValue(args, "--query") ?? "unforgiven";
    var hash = ArgValue(args, "--hash") ?? "Is90eweGbwOBNrH8z1KcR+ncK1Y=";
    var title = ArgValue(args, "--title") ?? "The Unforgiven";
    results.Add(await CueSuite.RunAsync(transcript, query, hash, title).ConfigureAwait(false));
}

if (results.Count == 0)
{
    Console.WriteLine($"unknown suite: {which}");
    return 2;
}

transcript.Summary(results);

return results.Any(r => r.Verdict == Verdict.Fail) ? 1
    : results.Any(r => r.Verdict == Verdict.Inconclusive) ? 2
    : 0;

static string? ArgValue(string[] args, string name)
{
    var index = Array.IndexOf(args, name);
    return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
}

/// <summary>Child-process entry for the crash matrix; see <see cref="JournalCrashChild"/>.</summary>
internal static class JournalCrashChild
{
    public static int Run(string[] args)
    {
        var directory = args[Array.IndexOf(args, "--dir") + 1];
        var mode = args[Array.IndexOf(args, "--mode") + 1];

        using var journal = Cantina.Barkeep.Setlist.SetlistJournal.Open(directory, TimeProvider.System);

        if (mode == "spam")
        {
            // Append forever; the parent kills this process mid-flight. The kill, not a
            // simulation, produces whatever torn shape a real crash produces.
            Console.WriteLine("APPENDING");

            for (var i = 0; ; i++)
            {
                journal.Append(
                    Intent($"spam-{i:D6}", $"hash-{i:D6}"),
                    TimeProvider.System,
                    out _);
            }
        }

        if (mode == "burst")
        {
            // Append a known count, report it acknowledged, then die as hard as a process
            // can die without the kernel's help. Everything acknowledged must survive.
            for (var i = 0; i < 5; i++)
            {
                journal.Append(Intent($"burst-{i}", $"hash-{i}"), TimeProvider.System, out _);
            }

            Console.WriteLine("ACKED 5");
            Console.Out.Flush();
            Environment.FailFast("self-test: simulated crash after acknowledgement");
        }

        Console.WriteLine($"unknown child mode {mode}");
        return 2;
    }

    private static Cantina.Barkeep.Setlist.SetlistIntent Intent(string id, string hash) => new()
    {
        CommandId = id,
        Kind = Cantina.Barkeep.Setlist.SetlistIntentKind.Add,
        Entry = new Cantina.Barkeep.Setlist.SetlistEntry(hash, "self-test", "self-test"),
    };
}

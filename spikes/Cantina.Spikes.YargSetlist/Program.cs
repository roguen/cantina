// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Cantina.Spikes.YargObserve;
using Cantina.Spikes.YargSetlist;

// Does a YARG setlist advance to the next song on its own?
//
// This decides how much of M4 and M5 Cantina owns. If YARG auto-advances, Cantina is a
// nicer browser for 652 songs. If it stops on the score screen, Cantina supplies something
// the game lacks.
//
// The measurement is delicate for one reason: the datagram carries no input provenance.
// Score to Gameplay looks identical whether YARG advanced itself or a controller button
// dismissed the screen. So most of this program is not measuring the transition, it is
// establishing that nothing could have caused it. The harness never sends input - see
// Native.cs, which links no SendInput, keybd_event, mouse_event, or SetForegroundWindow.
//
// It reports INCONCLUSIVE readily and by named cause. That is the point: this project has
// twice recorded a confident wrong answer from a spike that inferred past its evidence.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Windows-only: reads Win32 input state.");
    return 2;
}

var port = 36107;
var scoreObservationSeconds = 180;   // 18x PlayAShowTimeout, which is 10.0 in settings.json
var minGameplaySeconds = 60;
var yargDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "AppData", "LocalLow", "YARC", "YARG", "release");

for (var i = 0; i < args.Length; i++)
{
    var has = i + 1 < args.Length;

    switch (args[i])
    {
        case "--port" when has && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var p):
            port = p;
            i++;
            break;
        case "--score-window" when has && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var s):
            scoreObservationSeconds = s;
            i++;
            break;
        case "--min-gameplay" when has && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var g):
            minGameplaySeconds = g;
            i++;
            break;
        case "--yarg-dir" when has:
            yargDirectory = args[++i];
            break;
        case "-h" or "--help":
            Console.WriteLine("cantina yarg-setlist - does a YARG setlist auto-advance?");
            Console.WriteLine();
            Console.WriteLine("Watch-only. Sends nothing. Arm a setlist with two or more DISTINCT songs,");
            Console.WriteLine("start it, then run this before the first song ends.");
            Console.WriteLine();
            Console.WriteLine("  --score-window <s>   seconds to watch the score screen (default 180)");
            Console.WriteLine("  --min-gameplay <s>   minimum gameplay before Score counts (default 60)");
            Console.WriteLine("  --port <n>           UDP port (default 36107)");
            return 2;
        default:
            Console.Error.WriteLine($"unrecognized argument: {args[i]}");
            return 2;
    }
}

var clock = Stopwatch.StartNew();
var transcript = new StringBuilder();

void Log(string tag, string message)
{
    var line = $"[T+{clock.Elapsed.TotalSeconds,9:0.000}] {tag,-9} {message}";
    Console.WriteLine(line);
    transcript.AppendLine(line);
}

Log("RUN", $"stopwatch_hires={Stopwatch.IsHighResolution} wall_start={DateTimeOffset.Now:O}");
Log("RUN", "attest_no_sendinput=true (links no SendInput/keybd_event/mouse_event/SetForegroundWindow)");
Log("RUN", $"self_pid={Environment.ProcessId} score_window_s={scoreObservationSeconds} min_gameplay_s={minGameplaySeconds}");

var yargProcesses = Process.GetProcessesByName("YARG");
Log("YARG", $"instances={yargProcesses.Length}");

if (yargProcesses.Length != 1)
{
    Log("VERDICT", $"result=INCONCLUSIVE cause={(yargProcesses.Length == 0 ? "YARG-GONE" : "MULTI-YARG")}");
    Log("VERDICT", $"claim=\"{yargProcesses.Length} YARG instances; a single-source oracle is impossible.\"");
    return 2;
}

var yarg = yargProcesses[0];
Log("YARG", $"pid={yarg.Id} start={yarg.StartTime:O} hwnd=0x{yarg.MainWindowHandle:X}");

var settingsPath = Path.Combine(yargDirectory, "settings.json");

if (File.Exists(settingsPath))
{
    var settings = File.ReadAllText(settingsPath);

    foreach (var key in new[] { "PlayAShowTimeout", "NoFail", "PauseOnFocusLoss", "PauseOnDeviceDisconnect", "AllowDuplicateSongs" })
    {
        var at = settings.IndexOf($"\"{key}\"", StringComparison.Ordinal);
        var value = at < 0
            ? $"{key}=absent"
            : settings.Substring(at, Math.Min(44, settings.Length - at)).ReplaceLineEndings(" ").Trim();
        Log("SETTINGS", value);
    }
}

if (Native.TryGetStuckKeys(out var stuckAtStart))
{
    Log("GATE", $"keys_all_up=false stuck={stuckAtStart}");
    Log("VERDICT", "result=INCONCLUSIVE cause=INPUT-DETECTED");
    Log("VERDICT", $"claim=\"Keys were already down at window open ({stuckAtStart}); a held key drives the UI on its own.\"");
    return 2;
}

Log("GATE", "keys_all_up=true");

// A controller is the normal way a score screen gets dismissed on this PC, and it updates
// neither GetLastInputInfo nor a keyboard hook. Without this baseline the most likely
// source of a false positive is invisible.
var padBaseline = new uint?[4];
var padsConnected = 0;

for (uint slot = 0; slot < 4; slot++)
{
    try
    {
        if (Native.XInputGetState(slot, out var state) == 0)
        {
            padBaseline[slot] = state.PacketNumber;
            padsConnected++;
            Log("DEVICE", $"xinput slot={slot} connected=true packet={state.PacketNumber}");
        }
    }
    catch (DllNotFoundException)
    {
        Log("DEVICE", "xinput unavailable (xinput1_4.dll missing); controller input cannot be ruled out");
        break;
    }
}

Log("DEVICE", $"xinput_connected={padsConnected}");

var lastInputBaseline = Native.LastInputTicks();
Log("GATE", $"last_input_ticks={lastInputBaseline}");

using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);   // D-013
socket.Bind(new IPEndPoint(IPAddress.Any, port));

using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.Cancel();
};

var senders = new HashSet<string>(StringComparer.Ordinal);
var accepted = 0L;
var rejected = 0L;
var maxGapMs = 0.0;
var lastArrival = clock.Elapsed;
YargScene? scene = null;
YargPlayState? playState = null;
var sceneSince = clock.Elapsed;
var latchedHash = string.Empty;
var latchedTitle = string.Empty;
var sceneLock = new object();

_ = Task.Run(async () =>
{
    var buffer = new byte[128];
    var remote = new IPEndPoint(IPAddress.Any, 0);

    while (!lifetime.IsCancellationRequested)
    {
        try
        {
            var result = await socket.ReceiveFromAsync(buffer, SocketFlags.None, remote, lifetime.Token).ConfigureAwait(false);
            var now = clock.Elapsed;

            lock (sceneLock)
            {
                var gap = (now - lastArrival).TotalMilliseconds;

                if (accepted > 0 && gap > maxGapMs)
                {
                    maxGapMs = gap;
                }

                lastArrival = now;
                senders.Add(result.RemoteEndPoint.ToString() ?? "?");

                if (YargDatagram.TryParse(buffer.AsSpan(0, result.ReceivedBytes), out var datagram, out _) && datagram is not null)
                {
                    accepted++;

                    if (scene != datagram.Scene || playState != datagram.PlayState)
                    {
                        Log("STATE", $"scene={datagram.Scene} play={datagram.PlayState} (was {scene?.ToString() ?? "-"}/{playState?.ToString() ?? "-"})");
                        scene = datagram.Scene;
                        playState = datagram.PlayState;
                        sceneSince = now;
                    }
                }
                else
                {
                    rejected++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (SocketException)
        {
            break;
        }
    }
}, lifetime.Token);

// currentSong.json. Zero length is a real, distinct value here - "no song loaded" is an
// empty file, not a missing one - and the file clears about 86 ms after the scene changes
// (D-010), so identity must be latched rather than read on demand at the boundary.
_ = Task.Run(async () =>
{
    var songPath = Path.Combine(yargDirectory, "currentSong.json");
    var sawZero = false;

    while (!lifetime.IsCancellationRequested)
    {
        try
        {
            if (File.Exists(songPath))
            {
                var text = File.ReadAllText(songPath);

                if (text.Length == 0)
                {
                    if (!sawZero)
                    {
                        sawZero = true;
                        Log("SONG", "currentSong.json is zero-length (no song loaded)");
                    }
                }
                else
                {
                    // "HashBytes", not "Hash". The shape is {"Hash":{"HashBytes":"..."}}, so
                    // searching for the outer key returns the inner key's NAME as the value
                    // and the hash never changes between songs - which would silently
                    // suppress the identity-change log line this measurement depends on.
                    var hash = Extract(text, "HashBytes") ?? "?";
                    var title = Extract(text, "Name") ?? Extract(text, "Title") ?? "?";

                    if (hash != latchedHash)
                    {
                        Log("SONG", $"loaded title=\"{title}\" hash={hash}");
                        latchedHash = hash;
                        latchedTitle = title;
                        sawZero = false;
                    }
                }
            }
        }
        catch (IOException)
        {
            // YARG is rewriting it; the next poll will see it.
        }

        await Task.Delay(25, lifetime.Token).ConfigureAwait(false);
    }
}, lifetime.Token);

static string? Extract(string json, string key)
{
    var at = json.IndexOf($"\"{key}\"", StringComparison.OrdinalIgnoreCase);

    if (at < 0)
    {
        return null;
    }

    var colon = json.IndexOf(':', at);

    if (colon < 0)
    {
        return null;
    }

    var quote = json.IndexOf('"', colon);

    if (quote < 0)
    {
        return null;
    }

    var end = json.IndexOf('"', quote + 1);
    return end < 0 ? null : json[(quote + 1)..end];
}

Log("WINDOW", "open - watching. This process sends nothing. Ctrl+C to stop.");

string? fault = null;
var gameplayStart = TimeSpan.Zero;
var scoreStart = TimeSpan.Zero;
var hashAtScore = string.Empty;
var sawGameplay = false;
var sawScore = false;
var pausedDuringSong = false;
var verdictResult = "INCONCLUSIVE";
var verdictCause = "NO-GAMEPLAY";
var verdictClaim = "The run ended before gameplay was ever observed.";

while (!lifetime.IsCancellationRequested)
{
    try
    {
        await Task.Delay(100, lifetime.Token).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        break;
    }

    var now = clock.Elapsed;

    if (Process.GetProcessesByName("YARG").Length != 1)
    {
        fault = "YARG-GONE";
        break;
    }

    _ = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var foregroundPid);

    if (foregroundPid != (uint)yarg.Id)
    {
        // PauseOnFocusLoss is true, so losing foreground pauses the game with no key behind
        // the transition (measured: D-024). That alone destroys the measurement; whether
        // focus regain also changes state does not matter here.
        Log("SENTINEL", $"foreground_pid={foregroundPid} expected={yarg.Id}");
        fault = "FOREGROUND-LOST";
        break;
    }

    if (Native.TryGetStuckKeys(out var stuck))
    {
        Log("SENTINEL", $"key down: {stuck}");
        fault = "INPUT-DETECTED";
        break;
    }

    if (Native.LastInputTicks() != lastInputBaseline)
    {
        Log("SENTINEL", "GetLastInputInfo advanced: something generated input");
        fault = "INPUT-DETECTED";
        break;
    }

    for (uint slot = 0; slot < 4; slot++)
    {
        if (padBaseline[slot] is not { } baseline)
        {
            continue;
        }

        try
        {
            if (Native.XInputGetState(slot, out var state) == 0 && state.PacketNumber != baseline)
            {
                Log("SENTINEL", $"xinput slot={slot} packet {baseline} -> {state.PacketNumber}: a controller moved");
                fault = "INPUT-DETECTED";
            }
        }
        catch (DllNotFoundException)
        {
            // Already reported at baseline.
        }
    }

    if (fault is not null)
    {
        break;
    }

    lock (sceneLock)
    {
        var age = (now - lastArrival).TotalMilliseconds;

        if (accepted > 0 && age > 2000)
        {
            Log("SENTINEL", $"no datagram for {age:0} ms");
            fault = "STREAM-DEAD";
        }
        else if (senders.Count > 1)
        {
            Log("SENTINEL", $"senders={string.Join(",", senders)}");
            fault = "MULTI-SOURCE";
        }
        else if (accepted == 0 && now.TotalSeconds > 15)
        {
            Log("SENTINEL", "no datagrams at all - is the Experimental UDP Data Stream on?");
            fault = "STREAM-DEAD";
        }
        else if (scene == YargScene.Gameplay && playState == YargPlayState.Playing)
        {
            if (!sawGameplay)
            {
                sawGameplay = true;
                gameplayStart = now;
                Log("PHASE", "gameplay started");
            }
        }
        else if (scene == YargScene.Gameplay && playState == YargPlayState.Paused && sawGameplay)
        {
            pausedDuringSong = true;
        }
        else if (scene == YargScene.Score && sawGameplay && !sawScore)
        {
            sawScore = true;
            scoreStart = now;
            hashAtScore = latchedHash;
            var dwell = (now - gameplayStart).TotalSeconds;
            Log("PHASE", $"score screen reached; gameplay dwell={dwell:0.0}s song=\"{latchedTitle}\" hash={hashAtScore}");

            if (pausedDuringSong)
            {
                fault = "PAUSED-DURING-SONG";
            }
            else if (dwell < minGameplaySeconds)
            {
                Log("SENTINEL", $"gameplay only {dwell:0.0}s (< {minGameplaySeconds}s): the song did not run to completion");
                fault = "ENDED-EARLY";
            }
        }
        else if (sawScore && scene == YargScene.Gameplay && playState == YargPlayState.Playing)
        {
            var dScore = (now - scoreStart).TotalSeconds;
            verdictResult = "ADVANCES-TO-GAMEPLAY";
            verdictCause = "-";
            verdictClaim = $"YARG left the score screen and started gameplay again after {dScore:0.0}s with no "
                + $"keyboard, mouse or controller input detected. song now=\"{latchedTitle}\" hash={latchedHash} "
                + $"(was {hashAtScore}). max_gap={maxGapMs:0}ms datagrams={accepted}";
            Log("PHASE", $"ADVANCED after {dScore:0.0}s on the score screen");
            break;
        }
        else if (sawScore && scene == YargScene.Menu && (now - sceneSince).TotalSeconds > 5)
        {
            var dScore = (now - scoreStart).TotalSeconds;
            verdictResult = "ADVANCES-TO-HUMAN-SCREEN";
            verdictCause = "-";
            verdictClaim = $"YARG left the score screen after {dScore:0.0}s into a Menu scene and stayed there over "
                + "5s, with no input detected. That is neither auto-advance nor a stuck score screen; a human "
                + "screen is waiting. Read the capture to see which one.";
            Log("PHASE", $"left score into Menu after {dScore:0.0}s");
            break;
        }

        if (sawScore && fault is null && (now - scoreStart).TotalSeconds >= scoreObservationSeconds)
        {
            verdictResult = "DOES-NOT-ADVANCE-WITHIN-N";
            verdictCause = "-";
            verdictClaim = $"No advance observed within {scoreObservationSeconds}s on the score screen, with "
                + $"{accepted} datagrams accepted throughout and max gap {maxGapMs:0}ms. Not 'never' - bounded "
                + "by this window.";
            break;
        }
    }

    if (fault is not null)
    {
        break;
    }
}

if (fault is not null)
{
    verdictResult = "INCONCLUSIVE";
    verdictCause = fault;
    verdictClaim = $"Run stopped by sentinel {fault} after {clock.Elapsed.TotalSeconds:0.0}s. "
        + $"stage_reached={(sawScore ? "score" : sawGameplay ? "gameplay" : "pre-gameplay")}";
}
else if (!sawGameplay)
{
    verdictCause = "NO-GAMEPLAY";
}
else if (!sawScore)
{
    verdictCause = "NO-SCORE";
    verdictClaim = "Gameplay was observed but the score screen never was.";
}

await lifetime.CancelAsync().ConfigureAwait(false);

Log("SUMMARY", $"datagrams accepted={accepted} rejected={rejected} senders={senders.Count} max_gap_ms={maxGapMs:0}");
Log("SUMMARY", $"gameplay_seen={sawGameplay} score_seen={sawScore} paused_during_song={pausedDuringSong}");
Log("VERDICT", $"result={verdictResult} cause={verdictCause}");
Log("VERDICT", $"claim=\"{verdictClaim}\"");

var captureDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "captures"));
Directory.CreateDirectory(captureDirectory);
var outPath = Path.Combine(captureDirectory, $"setlist-{DateTime.Now:yyyyMMdd-HHmmss}.transcript.txt");
await File.WriteAllTextAsync(outPath, transcript.ToString(), CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"transcript: {outPath}");

return verdictResult switch
{
    "ADVANCES-TO-GAMEPLAY" or "ADVANCES-TO-HUMAN-SCREEN" => 0,
    "DOES-NOT-ADVANCE-WITHIN-N" => 1,
    _ => 2,
};

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using Cantina.Spikes.YargInput;

// Spike for issue #3: does stock YARG accept synthetic keyboard input?
//
// This spike SENDS INPUT. That is the opposite safety boundary from
// Cantina.Spikes.YargObserve, which only ever listens, and is why it is a separate program.
//
// The oracle is YARG's own UDP datagram. A key that lands changes scene or play state; a
// key that does not land changes nothing. That distinction is what makes the run decisive
// rather than a matter of watching a projector and guessing.
//
// It never takes foreground. YARG's PauseOnFocusLoss setting is true, so a tool that stole
// focus would pause the game and then measure its own side effect. The operator keeps YARG
// focused; this process injects from the background, which is exactly how Barkeep will
// have to behave.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("This spike is Windows-only: it proves a Win32 input path.");
    return 2;
}

// Several keys by default. Each candidate otherwise costs the operator a full cycle of
// replaying a song and alt-tabbing, and the question is only ever "did any key land".
var keySpec = "enter,escape,space";
var port = 36107;
var waitSeconds = 8;
var timeoutSeconds = 3;
var holdMilliseconds = 60;
var settleMs = 750;
var dryRun = false;
string? selectQuery = null;
string? expectSubstring = null;
var onSongList = false;
var useVirtualKeys = false;
var typeOnly = false;
var focusYarg = false;
var yargDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "AppData",
    "LocalLow",
    "YARC",
    "YARG",
    "release");

for (var i = 0; i < args.Length; i++)
{
    var hasValue = i + 1 < args.Length;

    switch (args[i])
    {
        case "--key" or "--keys" when hasValue:
            keySpec = args[++i];
            break;
        case "--port" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var p):
            port = p;
            i++;
            break;
        case "--wait" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var w):
            waitSeconds = w;
            i++;
            break;
        case "--timeout" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var t):
            timeoutSeconds = t;
            i++;
            break;
        case "--hold" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var h):
            holdMilliseconds = h;
            i++;
            break;
        case "--settle" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var s2):
            settleMs = s2;
            i++;
            break;
        case "--no-settle":
            settleMs = 0;
            break;
        case "--select" when hasValue:
            selectQuery = args[++i];
            break;
        case "--expect" when hasValue:
            expectSubstring = args[++i];
            break;
        case "--on-song-list":
            onSongList = true;
            break;
        case "--vk":
            useVirtualKeys = true;
            break;
        case "--focus-yarg":
            focusYarg = true;
            break;
        case "--type-only":
            typeOnly = true;
            break;
        case "--yarg-dir" when hasValue:
            yargDirectory = args[++i];
            break;
        case "--dry-run":
            dryRun = true;
            break;
        case "-h" or "--help":
            PrintUsage();
            return 2;
        default:
            Console.Error.WriteLine($"unrecognized or incomplete argument: {args[i]}");
            PrintUsage();
            return 2;
    }
}

var candidates = new List<(string Name, ushort Scan, bool Extended)>();

foreach (var name in keySpec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
{
    if (!ScanCodes.TryResolve(name, out var resolvedScan, out var resolvedExtended))
    {
        Console.Error.WriteLine($"unknown key '{name}'. known: {ScanCodes.Known}");
        return 2;
    }

    candidates.Add((name, resolvedScan, resolvedExtended));
}

if (candidates.Count == 0)
{
    Console.Error.WriteLine("no keys to send");
    return 2;
}

using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.Cancel();
};

var yargProcesses = Process.GetProcessesByName("YARG");

if (yargProcesses.Length == 0)
{
    Console.WriteLine("YARG is not running. Start it, load a song, and try again.");
    return 1;
}

// More than one instance makes every part of this spike ambiguous: which window receives
// the key, and which game's state the oracle is reading. Both instances broadcast to the
// same port, so their datagrams interleave into a state that belongs to neither.
if (yargProcesses.Length > 1)
{
    Console.WriteLine($"REFUSING TO RUN: {yargProcesses.Length} YARG instances are running.");

    foreach (var p in yargProcesses.OrderBy(p => p.Id))
    {
        Console.WriteLine($"  pid {p.Id}, started {p.StartTime:yyyy-MM-dd HH:mm}");
    }

    Console.WriteLine();
    Console.WriteLine("They all broadcast to the same UDP port, so the oracle sees their states");
    Console.WriteLine("interleaved and no baseline is meaningful. Close all but one and re-run.");
    return 2;
}

var yarg = yargProcesses[0];

Console.WriteLine($"YARG pid {yarg.Id}, window handle 0x{yarg.MainWindowHandle:X}");
Console.WriteLine($"keys to try, in order: {string.Join(", ", candidates.Select(c => $"{c.Name} (0x{c.Scan:X2})"))}");
Console.WriteLine($"hold {holdMilliseconds}ms, stopping at the first key that changes state");
Console.WriteLine($"listening on udp {port} for the oracle");

using var reader = new YargStateReader(port);
var readerTask = Task.Run(() => reader.RunAsync(lifetime.Token), lifetime.Token);

Console.WriteLine("waiting for the first datagram...");

var deadline = Stopwatch.StartNew();
while (reader.Current is null && deadline.Elapsed < TimeSpan.FromSeconds(10))
{
    await Task.Delay(20, lifetime.Token).ConfigureAwait(false);
}

if (reader.Current is null)
{
    Console.WriteLine("no datagrams. Is the Experimental UDP Data Stream enabled?");
    return 1;
}

Console.WriteLine($"baseline {reader.Current.Value}");
Console.WriteLine();

if (focusYarg)
{
    // Unattended mode. The spike normally waits for a human to focus YARG, which makes it
    // unrunnable when nobody is at the machine. Focusing here is not the same as stealing
    // foreground mid-measurement: it happens before anything is sent, and it puts the game
    // in the state a player would have put it in.
    Console.WriteLine("--focus-yarg: bringing YARG to the foreground...");

    if (!NativeMethods.TryFocus(yarg.MainWindowHandle))
    {
        Console.WriteLine("FAILED to focus YARG. Windows refused, or another window holds a");
        Console.WriteLine("foreground lock. Nothing was sent, because the result would be ambiguous.");
        return 1;
    }

    Console.WriteLine("YARG is foreground. Settling before send...");
    await Task.Delay(1500, lifetime.Token).ConfigureAwait(false);
}
else
{
    Console.WriteLine($"FOCUS YARG NOW. Sending {candidates.Count} key(s) in {waitSeconds} seconds.");
    Console.WriteLine("Do not touch the keyboard after focusing; any real key press would confound the result.");

    for (var remaining = waitSeconds; remaining > 0; remaining--)
    {
        Console.WriteLine($"  {remaining}...");
        await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
    }
}

// Prove YARG actually had focus. Without this, a null result is ambiguous: it cannot be
// told apart from a key delivered to some other window.
var foreground = NativeMethods.GetForegroundWindow();
_ = NativeMethods.GetWindowThreadProcessId(foreground, out var foregroundPid);
var foregroundIsYarg = foregroundPid == (uint)yarg.Id;

Console.WriteLine();
Console.WriteLine($"foreground pid at send time: {foregroundPid} (YARG={foregroundIsYarg})");

if (!foregroundIsYarg)
{
    Console.WriteLine("YARG was NOT foreground. Result would be ambiguous, so nothing was sent.");
    return 1;
}

if (dryRun)
{
    Console.WriteLine("dry run: no input sent.");
    return 0;
}

// Selection mode (issue #4). Types a query into the song list, confirms, and reports which
// song YARG actually loaded. currentSong.json is the only surface that answers "which".
if (selectQuery is not null)
{
    var scene = reader.Current?.Scene;

    if (scene != YargScene.Menu)
    {
        Console.WriteLine($"REFUSING: scene is {scene}, expected Menu. Selection is only meaningful");
        Console.WriteLine("from the song list. Back out to it and re-run.");
        return 2;
    }

    // The scene byte is far too coarse to be a real precondition. YARG reports Menu for the
    // start menu, the song list, settings, and the instrument setup screen alike, so this
    // check cannot tell whether the song list is actually open. An earlier run passed it
    // while sitting on the start menu and produced a meaningless result.
    //
    // Rather than imply a verification that does not exist, require the operator to assert
    // it explicitly.
    if (!onSongList)
    {
        Console.WriteLine("REFUSING: cannot verify which menu screen is open.");
        Console.WriteLine();
        Console.WriteLine("The datagram reports scene=Menu for the start menu, the song list, settings,");
        Console.WriteLine("and instrument setup alike, so this spike cannot confirm the song list is up.");
        Console.WriteLine("Open the song list yourself and pass --on-song-list to assert it.");
        return 2;
    }

    Console.WriteLine(useVirtualKeys
        ? "injection shape: virtual key + scan code (what a real keyboard produces)"
        : "injection shape: scan code only (raw input); text may not reach a text field this way");

    // Any leftover filter text silently changes what the query matches. A capture on
    // 2026-08-03 showed exactly that: stale text survived, the typed query never arrived,
    // and Enter confirmed a song nobody asked for.
    Console.WriteLine("clearing any existing search text (40 backspaces)...");

    // Windows can refuse an injection outright, and that is a completely different failure
    // from YARG receiving the keys and ignoring them. Earlier runs discarded this return
    // value, which left "nothing appeared in the search box" unable to tell the two apart.
    uint backspaceAccepted = 0;

    for (var b = 0; b < 40; b++)
    {
        backspaceAccepted += NativeMethods.SendKeyPress(
            0x0E,
            extended: false,
            holdMilliseconds: 12,
            virtualKey: useVirtualKeys ? ScanCodes.VirtualKeyBackspace : (ushort)0);
        await Task.Delay(15, lifetime.Token).ConfigureAwait(false);
    }

    Console.WriteLine($"  Windows accepted {backspaceAccepted} of 80 backspace events");

    await Task.Delay(400, lifetime.Token).ConfigureAwait(false);

    Console.WriteLine($"typing query: \"{selectQuery}\"");

    uint typedAccepted = 0;

    foreach (var character in selectQuery)
    {
        if (!ScanCodes.TryResolveChar(character, out var charScan))
        {
            Console.WriteLine($"  cannot type '{character}' - not in the scan-code map. Aborting rather than");
            Console.WriteLine("  typing a different query than the one requested.");
            return 2;
        }

        ushort charVk = 0;

        if (useVirtualKeys && !ScanCodes.TryResolveCharVirtualKey(character, out charVk))
        {
            Console.WriteLine($"  cannot map '{character}' to a virtual key. Aborting.");
            return 2;
        }

        typedAccepted += NativeMethods.SendKeyPress(charScan, extended: false, holdMilliseconds: 25, virtualKey: charVk);
        await Task.Delay(35, lifetime.Token).ConfigureAwait(false);
    }

    var typedExpected = (uint)selectQuery.Length * 2;
    Console.WriteLine($"  Windows accepted {typedAccepted} of {typedExpected} character events");

    if (typedAccepted < typedExpected)
    {
        Console.WriteLine();
        Console.WriteLine("Windows REFUSED some injections. Whatever the search field shows, this run says");
        Console.WriteLine("nothing about whether YARG accepts typed text: the keys never left this process.");
        return 1;
    }

    // Give YARG's filter a moment before confirming.
    await Task.Delay(900, lifetime.Token).ConfigureAwait(false);

    if (typeOnly)
    {
        Console.WriteLine();
        Console.WriteLine("--type-only: Enter was NOT sent.");
        Console.WriteLine("Look at YARG's search field now. Does it read exactly the query above?");
        Console.WriteLine("That single observation decides whether text injection works at all,");
        Console.WriteLine("and it cannot be read from the datagram.");
        return 0;
    }

    Console.WriteLine("sending enter...");
    _ = NativeMethods.SendKeyPress(
        0x1C,
        extended: false,
        holdMilliseconds: holdMilliseconds,
        virtualKey: useVirtualKeys ? ScanCodes.VirtualKeyEnter : (ushort)0);

    var loadDeadline = Stopwatch.StartNew();
    CurrentSong? loaded = null;

    while (loadDeadline.Elapsed < TimeSpan.FromSeconds(15))
    {
        loaded = CurrentSong.TryRead(yargDirectory);

        if (loaded is not null)
        {
            break;
        }

        await Task.Delay(50, lifetime.Token).ConfigureAwait(false);
    }

    Console.WriteLine();

    if (loaded is null)
    {
        Console.WriteLine("NO SONG LOADED within 15s.");
        Console.WriteLine($"scene is now {reader.Current?.Scene.ToString() ?? "unknown"}.");
        Console.WriteLine("Enter may open a sub-menu rather than starting, or the query matched nothing.");
        return 1;
    }

    Console.WriteLine($"LOADED: {loaded.ShortLocation}");
    Console.WriteLine($"  hash: {loaded.ContentHash}");
    Console.WriteLine($"  after {loadDeadline.Elapsed.TotalMilliseconds:0} ms");

    if (expectSubstring is null)
    {
        Console.WriteLine();
        Console.WriteLine("No --expect given, so this run records what loaded without judging it.");
        return 0;
    }

    var matched = loaded.Location.Contains(expectSubstring, StringComparison.OrdinalIgnoreCase);
    Console.WriteLine();
    Console.WriteLine(matched
        ? $"MATCH: the loaded song contains \"{expectSubstring}\"."
        : $"MISMATCH: expected a path containing \"{expectSubstring}\".");

    return matched ? 0 : 1;
}

var landed = false;
var sentCount = 0;
var skippedCount = 0;

foreach (var candidate in candidates)
{
    Console.WriteLine();
    Console.WriteLine($"--- {candidate.Name} ---");

    // Settle before every key, not just the first. An earlier key in this same run may
    // have moved the game, and without a fresh settled baseline one key's effect could be
    // credited to the next. (An earlier revision claimed focusing YARG resumes a paused
    // game; measured on 2026-08-28, it does not — focus regain leaves the pause in place
    // and only the pause menu's RESUME entry resumes. D-024 records it.)
    var observed = new List<string>();
    var settled = await reader
        .WaitForStableAsync(TimeSpan.FromMilliseconds(settleMs), TimeSpan.FromSeconds(6), observed, lifetime.Token)
        .ConfigureAwait(false);

    if (settled is null)
    {
        skippedCount++;
        Console.WriteLine($"  SKIPPED: state never held still for {settleMs} ms, so no key was sent.");
        Console.WriteLine("  What it saw changing:");

        foreach (var line in observed)
        {
            Console.WriteLine($"    {line}");
        }

        if (observed.Count <= 1)
        {
            Console.WriteLine("    (nothing recorded - the datagram may have stopped arriving)");
        }

        Console.WriteLine($"  If this is normal churn, raise the tolerance with --settle <ms> or use --no-settle.");
        continue;
    }

    // Re-check focus each time: an earlier key could have opened something that took it.
    var currentForeground = NativeMethods.GetForegroundWindow();
    _ = NativeMethods.GetWindowThreadProcessId(currentForeground, out var currentPid);

    if (currentPid != (uint)yarg.Id)
    {
        Console.WriteLine($"  YARG lost foreground (now pid {currentPid}); stopping rather than reporting an ambiguous result");
        break;
    }

    var baseline = settled.Value;
    Console.WriteLine($"  baseline {baseline}");

    var sent = NativeMethods.SendKeyPress(candidate.Scan, candidate.Extended, holdMilliseconds);
    sentCount++;

    if (sent < 2)
    {
        Console.WriteLine($"  SendInput accepted only {sent} of 2 events - Windows refused the injection itself");
        Console.WriteLine("  That is an integrity-level problem, not a YARG behavior. Stopping.");
        return 1;
    }

    var change = await reader
        .WaitForChangeAsync(baseline, TimeSpan.FromSeconds(timeoutSeconds), lifetime.Token)
        .ConfigureAwait(false);

    if (change is null)
    {
        Console.WriteLine($"  no state change within {timeoutSeconds}s");
        continue;
    }

    var (state, elapsed) = change.Value;
    Console.WriteLine($"  STATE CHANGED after {elapsed.TotalMilliseconds:0} ms: {baseline} -> {state}");
    landed = true;
    break;
}

Console.WriteLine();

if (landed)
{
    Console.WriteLine("RESULT: synthetic keyboard input reaches stock YARG. SendInput is viable.");
    return 0;
}

if (sentCount == 0)
{
    Console.WriteLine($"RESULT: INCONCLUSIVE. No key was actually sent; all {skippedCount} were skipped.");
    Console.WriteLine("Nothing was injected, so this run says nothing whatsoever about whether YARG");
    Console.WriteLine("accepts synthetic input. Read the state churn listed above, then retry with a");
    Console.WriteLine("larger --settle value, or --no-settle to send regardless.");
    return 2;
}

Console.WriteLine($"RESULT: {sentCount} key(s) sent, none changed state.");

if (skippedCount > 0)
{
    Console.WriteLine($"PARTIAL: {skippedCount} key(s) were skipped and never sent, so this is weaker");
    Console.WriteLine("evidence than a clean run. Re-run so every candidate is actually delivered.");
    return 1;
}

Console.WriteLine("Windows accepted every injection and YARG held focus throughout.");
Console.WriteLine();
Console.WriteLine("This is NOT evidence that YARG ignored the keys. The oracle is the datagram, and");
Console.WriteLine("the datagram cannot see a move that stays inside one scene: `CurrentScene` reports");
Console.WriteLine("Menu for the start menu, the song list, settings, and instrument setup alike");
Console.WriteLine("(D-015). A key that navigates from one menu screen to another lands perfectly and");
Console.WriteLine("still reports no state change here. That exact case was observed on 2026-08-27:");
Console.WriteLine("Enter moved the start menu into the song list while this summary claimed the key");
Console.WriteLine("had been ignored.");
Console.WriteLine();
Console.WriteLine("Read the screen before concluding anything: spikes/observe-screen.ps1. Only a key");
Console.WriteLine("whose expected effect crosses a scene boundary can be judged from the datagram.");
return 1;

static void PrintUsage() =>
    Console.WriteLine("""
        cantina yarg-input - spike for issue #3

        Sends one synthetic key press to a focused YARG and uses YARG's own UDP datagram to
        decide whether it landed. Never takes foreground: YARG's PauseOnFocusLoss is true, so
        a tool that stole focus would pause the game and measure its own side effect.

          --key <name>     key to send (default escape)
          --port <n>       UDP oracle port (default 36107)
          --wait <n>       seconds to focus YARG before sending (default 8)
          --timeout <n>    seconds to wait for a state change (default 3)
          --hold <ms>      key-down duration (default 60)
          --dry-run        do everything except send
          -h, --help       this message

        Exit 0 if a state change was observed, 1 if not, 2 on bad usage.
        """);

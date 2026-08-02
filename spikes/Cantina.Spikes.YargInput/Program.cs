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

var key = "escape";
var port = 36107;
var waitSeconds = 8;
var timeoutSeconds = 3;
var holdMilliseconds = 60;
var dryRun = false;

for (var i = 0; i < args.Length; i++)
{
    var hasValue = i + 1 < args.Length;

    switch (args[i])
    {
        case "--key" when hasValue:
            key = args[++i];
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

if (!ScanCodes.TryResolve(key, out var scanCode, out var extended))
{
    Console.Error.WriteLine($"unknown key '{key}'. known: {ScanCodes.Known}");
    return 2;
}

using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    lifetime.Cancel();
};

var yarg = Process.GetProcessesByName("YARG").FirstOrDefault();

if (yarg is null)
{
    Console.WriteLine("YARG is not running. Start it, load a song, and try again.");
    return 1;
}

Console.WriteLine($"YARG pid {yarg.Id}, window handle 0x{yarg.MainWindowHandle:X}");
Console.WriteLine($"key '{key}' -> scan 0x{scanCode:X2}{(extended ? " extended" : string.Empty)}, hold {holdMilliseconds}ms");
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
Console.WriteLine($"FOCUS YARG NOW. Sending '{key}' in {waitSeconds} seconds.");
Console.WriteLine("Do not touch the keyboard after focusing; any real key press would confound the result.");

for (var remaining = waitSeconds; remaining > 0; remaining--)
{
    Console.WriteLine($"  {remaining}...");
    await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
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

// Focusing YARG resumes it, because PauseOnFocusLoss is true. Let that settle before taking
// the baseline, so a focus-induced transition cannot be misread as the injected key landing.
Console.WriteLine("waiting for state to settle after the focus change...");

var settled = await reader
    .WaitForStableAsync(TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(6), lifetime.Token)
    .ConfigureAwait(false);

if (settled is null)
{
    Console.WriteLine("state never held still for 750 ms, so a baseline would not be trustworthy.");
    Console.WriteLine("Nothing was sent. Let the game settle and run again.");
    return 1;
}

var baseline = settled.Value;
Console.WriteLine($"settled baseline: {baseline}");

if (dryRun)
{
    Console.WriteLine("dry run: no input sent.");
    return 0;
}

var sent = NativeMethods.SendKeyPress(scanCode, extended, holdMilliseconds);
var sendMoment = Stopwatch.StartNew();

Console.WriteLine($"SendInput accepted {sent} of 2 events");

if (sent < 2)
{
    Console.WriteLine("Windows refused the injection itself. That is a different failure from YARG ignoring it.");
    return 1;
}

var change = await reader
    .WaitForChangeAsync(baseline, TimeSpan.FromSeconds(timeoutSeconds), lifetime.Token)
    .ConfigureAwait(false);

Console.WriteLine();

if (change is null)
{
    Console.WriteLine($"NO STATE CHANGE within {timeoutSeconds}s. State still {baseline}.");
    Console.WriteLine("Either YARG ignored the key, or this key does nothing in the current screen.");
    Console.WriteLine("Try a different --key, or a screen where the key has an unambiguous effect.");
    return 1;
}

var (state, elapsed) = change.Value;

Console.WriteLine($"STATE CHANGED after {elapsed.TotalMilliseconds:0} ms: {baseline}  ->  {state}");
Console.WriteLine("Synthetic keyboard input reached stock YARG.");
Console.WriteLine($"(elapsed includes up to one datagram interval, about 11 ms at 90 Hz; send-to-observe was {sendMoment.Elapsed.TotalMilliseconds:0} ms)");

return 0;

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

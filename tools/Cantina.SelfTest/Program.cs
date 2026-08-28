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
//
// Verdicts follow the house rule: PASS, FAIL, or INCONCLUSIVE with a named cause -
// a suite that cannot establish its preconditions says so rather than guessing.
// Exit codes: 0 all pass, 1 any fail, 2 no failures but something inconclusive.
//
// This tool sends no input. Like the setlist harness, it links no SendInput,
// keybd_event, mouse_event, or SetForegroundWindow; the readiness suite only reads.

if (args.Length >= 1 && args[0] == "journal-child")
{
    return JournalCrashChild.Run(args);
}

if (args.Length < 2 || args[0] != "run")
{
    Console.WriteLine("usage: Cantina.SelfTest run <all|journal|live|readiness>");
    return 2;
}

var transcript = new Transcript();
transcript.Banner();

var results = new List<SuiteResult>();
var which = args[1];

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

if (results.Count == 0)
{
    Console.WriteLine($"unknown suite: {which}");
    return 2;
}

transcript.Summary(results);

return results.Any(r => r.Verdict == Verdict.Fail) ? 1
    : results.Any(r => r.Verdict == Verdict.Inconclusive) ? 2
    : 0;

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

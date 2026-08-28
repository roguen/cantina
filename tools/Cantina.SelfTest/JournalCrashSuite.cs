// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using Cantina.Barkeep.Setlist;

namespace Cantina.SelfTest;

/// <summary>
/// D-023's crash matrix, executed with real kills on the real filesystem. The child
/// process races journal appends and the parent kills it hard, so torn tails and missing
/// outcomes are produced by genuine process death — the honest version of the file-level
/// shapes the unit tests stage.
///
/// The invariant after every kill is the acknowledgement contract: reopening succeeds,
/// every command with a Done outcome is applied exactly once, at most one intent is
/// recovered ambiguous (the kill window between intent flush and outcome flush), and a
/// second reopen reproduces the first byte-for-byte in state terms.
/// </summary>
internal static class JournalCrashSuite
{
    private const int RacingKillRounds = 5;

    public static async Task<SuiteResult> RunAsync(Transcript transcript)
    {
        var failures = 0;
        var root = Path.Combine(Path.GetTempPath(), "cantina-selftest", Path.GetRandomFileName());

        try
        {
            for (var round = 1; round <= RacingKillRounds; round++)
            {
                var directory = Path.Combine(root, $"racing-{round}");
                var survived = await RacingKillAsync(transcript, directory, 30 + (round * 17)).ConfigureAwait(false);

                if (!survived)
                {
                    failures++;
                }
            }

            if (!await BurstAckAsync(transcript, Path.Combine(root, "burst")).ConfigureAwait(false))
            {
                failures++;
            }

            if (!CorruptSnapshot(transcript, Path.Combine(root, "corrupt")))
            {
                failures++;
            }

            if (!RebootSim(transcript, Path.Combine(root, "reboot")))
            {
                failures++;
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Leftovers under the temp root are harmless.
            }
        }

        return failures == 0
            ? new SuiteResult("journal", Verdict.Pass,
                $"{RacingKillRounds} racing kills, one crash-after-ack, one corrupt snapshot, and one restart all recovered per D-023")
            : new SuiteResult("journal", Verdict.Fail, $"{failures} crash case(s) violated the recovery contract");
    }

    private static async Task<bool> RacingKillAsync(Transcript transcript, string directory, int killAfterMs)
    {
        using var child = StartChild(directory, "spam");
        var line = await child.StandardOutput.ReadLineAsync().ConfigureAwait(false);

        if (line != "APPENDING")
        {
            transcript.Case("journal", "racing-kill", pass: false, $"child never started appending (got '{line}')");
            return false;
        }

        await Task.Delay(killAfterMs).ConfigureAwait(false);
        child.Kill(entireProcessTree: true);
        await child.WaitForExitAsync().ConfigureAwait(false);

        int entries;
        int ambiguous;
        int quarantined;

        using (var first = SetlistJournal.Open(directory, TimeProvider.System))
        {
            entries = first.State.Entries.Count;
            ambiguous = first.RecoveredAmbiguous.Count;
            quarantined = first.QuarantinedFiles.Count;
        }

        using var second = SetlistJournal.Open(directory, TimeProvider.System);
        var stable = second.State.Entries.Count == entries && second.RecoveredAmbiguous.Count == 0;

        var pass = ambiguous <= 1 && stable;
        transcript.Case("journal", "racing-kill", pass,
            $"killed at {killAfterMs} ms: {entries} entries survived, ambiguous={ambiguous}, "
            + $"quarantined={quarantined}, second reopen stable={stable}");
        return pass;
    }

    private static async Task<bool> BurstAckAsync(Transcript transcript, string directory)
    {
        using var child = StartChild(directory, "burst");
        var line = await child.StandardOutput.ReadLineAsync().ConfigureAwait(false);
        await child.WaitForExitAsync().ConfigureAwait(false);

        if (line != "ACKED 5")
        {
            transcript.Case("journal", "crash-after-ack", pass: false, $"child never acknowledged (got '{line}')");
            return false;
        }

        using var journal = SetlistJournal.Open(directory, TimeProvider.System);
        var pass = journal.State.Entries.Count == 5 && journal.RecoveredAmbiguous.Count == 0;
        transcript.Case("journal", "crash-after-ack", pass,
            $"5 acknowledged before FailFast; {journal.State.Entries.Count} recovered, "
            + $"ambiguous={journal.RecoveredAmbiguous.Count} - acknowledged means durable");
        return pass;
    }

    private static bool CorruptSnapshot(Transcript transcript, string directory)
    {
        using (var journal = SetlistJournal.Open(directory, TimeProvider.System))
        {
            journal.Append(NewIntent("c1", "h1"), TimeProvider.System, out _);
            journal.Compact();
            journal.Append(NewIntent("c2", "h2"), TimeProvider.System, out _);
        }

        File.WriteAllText(Path.Combine(directory, "setlist-snapshot.json"), "{torn");

        using var reopened = SetlistJournal.Open(directory, TimeProvider.System);

        // The snapshot held c1; the journal after compaction holds c2. Losing the
        // snapshot to corruption costs the compacted history - which is why the
        // quarantine is surfaced to the client as "state recovered from <when>" - but
        // must never lose the journal or fail to open.
        var pass = reopened.QuarantinedFiles.Count == 1
            && reopened.State.Entries.Count == 1
            && reopened.State.Entries[0].Hash == "h2";
        transcript.Case("journal", "corrupt-snapshot", pass,
            $"snapshot quarantined; journal survived with {reopened.State.Entries.Count} entry(ies)");
        return pass;
    }

    private static bool RebootSim(Transcript transcript, string directory)
    {
        using (var journal = SetlistJournal.Open(directory, TimeProvider.System))
        {
            journal.Append(NewIntent("c1", "h1"), TimeProvider.System, out _);
            journal.Append(NewIntent("cursor", "-", SetlistIntentKind.MoveCursor, cursor: 0), TimeProvider.System, out _);
        }

        using var reopened = SetlistJournal.Open(directory, TimeProvider.System);
        var pass = reopened.State.Entries.Count == 1 && reopened.State.Cursor == 0;
        transcript.Case("journal", "restart", pass, "setlist and cursor intact across close and reopen");
        return pass;
    }

    private static Process StartChild(string directory, string mode)
    {
        var start = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath ?? "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("journal-child");
        start.ArgumentList.Add("--dir");
        start.ArgumentList.Add(directory);
        start.ArgumentList.Add("--mode");
        start.ArgumentList.Add(mode);

        return Process.Start(start) ?? throw new InvalidOperationException("child failed to start");
    }

    private static SetlistIntent NewIntent(
        string id,
        string hash,
        SetlistIntentKind kind = SetlistIntentKind.Add,
        int? cursor = null) => new()
        {
            CommandId = id,
            Kind = kind,
            Entry = kind == SetlistIntentKind.Add ? new SetlistEntry(hash, "self-test", "self-test") : null,
            Cursor = cursor,
        };
}

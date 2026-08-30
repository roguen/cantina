// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Acquisition;
using Cantina.Barkeep.Library;
using Cantina.Barkeep.Setlist;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The deterministic halves of the Geomitron Bridge handoff (D-030): the index treating a
/// real-layout .sng as a song, the arrival probe's containment and stability, the durable
/// import journal, and the play-next slot. The live halves — a real download, YARG's Scan
/// Songs, the cue — belong to the target-PC acceptance run.
/// </summary>
public sealed class AcquisitionTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    private static string WriteSng(string directory, string fileName, string title, string artist)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, SngDocumentTests.Build([("name", title), ("artist", artist)]));
        return path;
    }

    private static FileArrivalPort Port(string root, long maxBytes = long.MaxValue) =>
        new(Options.Create(new AcquisitionOptions
        {
            WatchDirectory = root,
            MaximumSngBytes = maxBytes,
            StabilityProbeMilliseconds = 1,
        }));

    // ── The index treats an .sng as a song ────────────────────────────────────────────

    [Fact]
    public void TheScanIndexesAnSngAlongsideFolders()
    {
        var root = TempDirectory();
        var sng = WriteSng(root, "Foo Fighters - Everlong (Hoph2o).sng", "Everlong", "Foo Fighters");

        var index = new SongIndex();
        var report = index.Scan([root], TimeProvider.System);

        Assert.Equal(1, report.Indexed);
        Assert.Empty(report.Skipped);

        var song = Assert.Single(index.Search("everlong"));
        Assert.Equal(sng, song.Location);
        Assert.Equal("Foo Fighters", song.Artist);
    }

    [Fact]
    public void AMalformedSngIsSkippedWithItsReason()
    {
        var root = TempDirectory();
        File.WriteAllBytes(Path.Combine(root, "broken.sng"), "SNGPKG???"u8.ToArray());

        var index = new SongIndex();
        var report = index.Scan([root], TimeProvider.System);

        Assert.Equal(0, report.Indexed);
        var skip = Assert.Single(report.Skipped);
        Assert.Equal("sng-truncated-header", skip.Reason);
    }

    // ── Containment and stability ─────────────────────────────────────────────────────

    [Fact]
    public async Task AnArrivalOutsideTheWatchRootIsRefusedByName()
    {
        var root = TempDirectory();

        foreach (var hostile in new[]
        {
            @"..\escape.sng",
            @"C:\Windows\system32\evil.sng",
            @"nested\inner.sng",
        })
        {
            var probe = await Port(root).ProbeAsync(
                new SongArrivalCandidate("test", hostile, "f"), CancellationToken.None);

            Assert.Equal(SongArrivalProbeState.Rejected, probe.State);
            Assert.Equal("arrival-escapes-watch-root", probe.FailureCode);
        }
    }

    [Fact]
    public async Task OnlySngFilesAreArrivals()
    {
        var root = TempDirectory();
        File.WriteAllText(Path.Combine(root, "notes.txt"), "not a chart");

        var probe = await Port(root).ProbeAsync(
            new SongArrivalCandidate("test", "notes.txt", "f"), CancellationToken.None);

        Assert.Equal(SongArrivalProbeState.Rejected, probe.State);
        Assert.Equal("arrival-not-sng", probe.FailureCode);
    }

    [Fact]
    public async Task AGrowingFileStabilizesBeforeItIsReady()
    {
        var root = TempDirectory();
        var path = WriteSng(root, "arriving.sng", "Song", "Artist");
        var port = Port(root);
        var candidate = new SongArrivalCandidate("test", "arriving.sng", "f");

        // First sight: no baseline yet.
        Assert.Equal(SongArrivalProbeState.Stabilizing,
            (await port.ProbeAsync(candidate, CancellationToken.None)).State);

        // It grew between probes — still being written.
        File.AppendAllText(path, "more bytes");
        Assert.Equal(SongArrivalProbeState.Stabilizing,
            (await port.ProbeAsync(candidate, CancellationToken.None)).State);

        // Held still: ready.
        Assert.Equal(SongArrivalProbeState.Ready,
            (await port.ProbeAsync(candidate, CancellationToken.None)).State);
    }

    [Fact]
    public async Task AnOversizedArrivalIsRefusedNotImported()
    {
        var root = TempDirectory();
        WriteSng(root, "big.sng", "Song", "Artist");

        var probe = await Port(root, maxBytes: 4).ProbeAsync(
            new SongArrivalCandidate("test", "big.sng", "f"), CancellationToken.None);

        Assert.Equal(SongArrivalProbeState.Rejected, probe.State);
        Assert.Equal("arrival-oversized", probe.FailureCode);
    }

    // ── The durable import journal ────────────────────────────────────────────────────

    [Fact]
    public async Task ACompletedImportNeverRunsAgainEvenAcrossRestart()
    {
        var directory = TempDirectory();
        var candidate = new SongArrivalCandidate("test", "a.sng", "fp-1");

        using (var journal = AcquisitionJournal.Open(directory))
        {
            var claim = await journal.ClaimAsync("k1", candidate, CancellationToken.None);
            Assert.Equal(ImportPlayNextClaimState.Acquired, claim.State);
            await journal.FinalizeAsync("k1", candidate, claim.LeaseId!,
                new ImportPlayNextTerminalReceipt(ImportPlayNextOutcome.Completed, null),
                CancellationToken.None);
        }

        using var reopened = AcquisitionJournal.Open(directory);
        var replay = await reopened.ClaimAsync("k1", candidate, CancellationToken.None);

        Assert.Equal(ImportPlayNextClaimState.Terminal, replay.State);
        Assert.Equal(ImportPlayNextOutcome.Completed, replay.TerminalReceipt!.Outcome);
    }

    [Fact]
    public async Task ACrashedImportIsClaimableAgainAfterRestart()
    {
        // A lease with no receipt is a crash mid-import. Every pipeline step is
        // idempotent, so re-running converges — the journal must not wedge the file.
        var directory = TempDirectory();
        var candidate = new SongArrivalCandidate("test", "a.sng", "fp-1");

        using (var journal = AcquisitionJournal.Open(directory))
        {
            var claim = await journal.ClaimAsync("k1", candidate, CancellationToken.None);
            Assert.Equal(ImportPlayNextClaimState.Acquired, claim.State);
            // No finalize: the process dies here.
        }

        using var reopened = AcquisitionJournal.Open(directory);
        var again = await reopened.ClaimAsync("k1", candidate, CancellationToken.None);

        Assert.Equal(ImportPlayNextClaimState.Acquired, again.State);
    }

    [Fact]
    public async Task TheSameKeyWithADifferentFingerprintConflicts()
    {
        var directory = TempDirectory();
        using var journal = AcquisitionJournal.Open(directory);

        await journal.ClaimAsync("k1",
            new SongArrivalCandidate("test", "a.sng", "fp-1"), CancellationToken.None);
        var conflict = await journal.ClaimAsync("k1",
            new SongArrivalCandidate("test", "a.sng", "fp-2"), CancellationToken.None);

        Assert.Equal(ImportPlayNextClaimState.Conflict, conflict.State);
    }

    [Fact]
    public async Task OnlyFailuresCanBeForgottenForRetry()
    {
        var directory = TempDirectory();
        using var journal = AcquisitionJournal.Open(directory);
        var candidate = new SongArrivalCandidate("test", "a.sng", "fp-1");

        var failed = await journal.ClaimAsync("failed", candidate, CancellationToken.None);
        await journal.FinalizeAsync("failed", candidate, failed.LeaseId!,
            new ImportPlayNextTerminalReceipt(ImportPlayNextOutcome.Failed, "refresh-unsafe"),
            CancellationToken.None);

        var done = await journal.ClaimAsync("done", candidate, CancellationToken.None);
        await journal.FinalizeAsync("done", candidate, done.LeaseId!,
            new ImportPlayNextTerminalReceipt(ImportPlayNextOutcome.Completed, null),
            CancellationToken.None);

        var ambiguous = await journal.ClaimAsync("ambiguous", candidate, CancellationToken.None);
        await journal.FinalizeAsync("ambiguous", candidate, ambiguous.LeaseId!,
            new ImportPlayNextTerminalReceipt(ImportPlayNextOutcome.Ambiguous, "cue-outcome-unknown"),
            CancellationToken.None);

        // Failure retries; completion must not repeat; ambiguity needs eyes, not retries.
        Assert.True(journal.ForgetFailure("failed"));
        Assert.False(journal.ForgetFailure("done"));
        Assert.False(journal.ForgetFailure("ambiguous"));

        Assert.Equal(ImportPlayNextClaimState.Acquired,
            (await journal.ClaimAsync("failed", candidate, CancellationToken.None)).State);
    }

    // ── The play-next slot ────────────────────────────────────────────────────────────

    [Fact]
    public void InsertNextLandsAfterTheCursorAndLeavesItAlone()
    {
        var state = SetlistState.Empty
            .Apply(new SetlistIntent { CommandId = "1", Kind = SetlistIntentKind.Add, Entry = new("h1", "First", "A") })
            .Apply(new SetlistIntent { CommandId = "2", Kind = SetlistIntentKind.Add, Entry = new("h2", "Second", "A") })
            .Apply(new SetlistIntent { CommandId = "3", Kind = SetlistIntentKind.Add, Entry = new("h3", "Third", "A") });

        var inserted = state.Apply(new SetlistIntent
        {
            CommandId = "4",
            Kind = SetlistIntentKind.InsertNext,
            Entry = new("h-new", "Arrival", "B"),
        });

        // Cursor at 0: the current song stays current, the arrival plays next.
        Assert.Equal(["First", "Arrival", "Second", "Third"], inserted.Entries.Select(e => e.Title));
        Assert.Equal(0, inserted.Cursor);
    }

    [Fact]
    public void InsertNextIntoAnEmptySetlistBecomesTheOnlyEntry()
    {
        var state = SetlistState.Empty.Apply(new SetlistIntent
        {
            CommandId = "1",
            Kind = SetlistIntentKind.InsertNext,
            Entry = new("h", "Arrival", "B"),
        });

        Assert.Equal("Arrival", Assert.Single(state.Entries).Title);
        Assert.Equal(0, state.Cursor);
    }

    [Fact]
    public void InsertNextThroughTheJournalReplaysWithoutDuplicating()
    {
        var directory = TempDirectory();
        using var journal = SetlistJournal.Open(directory, TimeProvider.System);
        var intent = new SetlistIntent
        {
            CommandId = "insert-next-sng:abc",
            Kind = SetlistIntentKind.InsertNext,
            Entry = new("", "Everlong", "Foo Fighters", @"C:\songs\everlong.sng"),
        };

        Assert.True(journal.Append(intent, TimeProvider.System, out var first));
        Assert.Equal(SetlistOutcome.Done, first);

        // The retry after a crash re-sends the same command id and must converge.
        Assert.False(journal.Append(intent, TimeProvider.System, out var replayed));
        Assert.Equal(SetlistOutcome.Done, replayed);
        Assert.Single(journal.State.Entries);
    }
}

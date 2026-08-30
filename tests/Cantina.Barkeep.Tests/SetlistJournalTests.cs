// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;

namespace Cantina.Barkeep.Tests;

public sealed class SetlistJournalTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A straggling handle on Windows; the temp root is periodically cleaned.
        }
    }

    private static SetlistIntent Add(string id, string hash, string title = "t", string artist = "a") =>
        new() { CommandId = id, Kind = SetlistIntentKind.Add, Entry = new SetlistEntry(hash, title, artist) };

    [Fact]
    public void AppliesAndSurvivesReopen()
    {
        using (var journal = SetlistJournal.Open(_directory, TimeProvider.System))
        {
            Assert.True(journal.Append(Add("c1", "h1"), TimeProvider.System, out var outcome));
            Assert.Equal(SetlistOutcome.Done, outcome);
            Assert.True(journal.Append(Add("c2", "h2"), TimeProvider.System, out _));
        }

        using var reopened = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.Equal(2, reopened.State.Entries.Count);
        Assert.Equal("h1", reopened.State.Entries[0].Hash);
        Assert.Empty(reopened.RecoveredAmbiguous);
    }

    [Fact]
    public void ADuplicateCommandIdReplaysWithoutReapplying()
    {
        using var journal = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.True(journal.Append(Add("c1", "h1"), TimeProvider.System, out _));
        Assert.False(journal.Append(Add("c1", "h1"), TimeProvider.System, out var outcome));
        Assert.Equal(SetlistOutcome.Done, outcome);
        Assert.Single(journal.State.Entries);
    }

    [Fact]
    public void IdempotencySurvivesReopenAndCompaction()
    {
        using (var journal = SetlistJournal.Open(_directory, TimeProvider.System))
        {
            journal.Append(Add("c1", "h1"), TimeProvider.System, out _);
            journal.Compact();
        }

        using var reopened = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.False(reopened.Append(Add("c1", "h1"), TimeProvider.System, out var outcome));
        Assert.Equal(SetlistOutcome.Done, outcome);
        Assert.Single(reopened.State.Entries);
    }

    [Fact]
    public void AnIntentWithoutAnOutcomeRecoversAsAmbiguousAndIsNeverReExecuted()
    {
        // D-023 crash case 2, staged at the file level: the intent line reached disk, the
        // outcome never did. Recovery must surface ambiguous, not re-apply, and a second
        // recovery must replay identically because the ambiguous outcome is itself
        // journaled.
        Directory.CreateDirectory(_directory);
        var journalPath = Path.Combine(_directory, "setlist-journal.jsonl");
        File.WriteAllText(
            journalPath,
            """{"kind":0,"commandId":"c9","intent":{"commandId":"c9","kind":"Add","entry":{"hash":"h9","title":"t","artist":"a"}},"at":"2026-08-28T12:00:00+00:00"}""" + "\n");

        using (var journal = SetlistJournal.Open(_directory, TimeProvider.System))
        {
            Assert.Empty(journal.State.Entries);
            var ambiguous = Assert.Single(journal.RecoveredAmbiguous);
            Assert.Equal("c9", ambiguous.CommandId);

            Assert.False(journal.Append(Add("c9", "h9"), TimeProvider.System, out var outcome));
            Assert.Equal(SetlistOutcome.Ambiguous, outcome);
        }

        using var second = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.Empty(second.State.Entries);
        Assert.Empty(second.RecoveredAmbiguous);
    }

    [Fact]
    public void ATornTailLineIsQuarantinedAndEverythingBeforeItSurvives()
    {
        // D-023 crash case 1: killed mid-append. The torn line is the file-level shape of
        // that kill.
        using (var journal = SetlistJournal.Open(_directory, TimeProvider.System))
        {
            journal.Append(Add("c1", "h1"), TimeProvider.System, out _);
        }

        var journalPath = Path.Combine(_directory, "setlist-journal.jsonl");
        File.AppendAllText(journalPath, """{"kind":0,"commandId":"torn","inte""");

        using var reopened = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.Single(reopened.State.Entries);
        Assert.NotEmpty(reopened.QuarantinedFiles);
    }

    [Fact]
    public void ACorruptSnapshotFallsBackAndIsQuarantined()
    {
        // D-023 crash case 3.
        using (var journal = SetlistJournal.Open(_directory, TimeProvider.System))
        {
            journal.Append(Add("c1", "h1"), TimeProvider.System, out _);
        }

        File.WriteAllText(Path.Combine(_directory, "setlist-snapshot.json"), "{not json");

        using var reopened = SetlistJournal.Open(_directory, TimeProvider.System);
        Assert.Single(reopened.State.Entries);
        Assert.Contains(reopened.QuarantinedFiles, f => f.Contains("snapshot", StringComparison.Ordinal));
    }

    [Fact]
    public void RemoveBeforeTheCursorShiftsItWithTheSongs()
    {
        var state = SetlistState.Empty
            .Apply(Add("a", "h1"))
            .Apply(Add("b", "h2"))
            .Apply(Add("c", "h3"))
            .Apply(new SetlistIntent { CommandId = "m", Kind = SetlistIntentKind.MoveCursor, Cursor = 2 })
            .Apply(new SetlistIntent { CommandId = "r", Kind = SetlistIntentKind.Remove, Hash = "h1" });

        Assert.Equal(2, state.Entries.Count);
        Assert.Equal(1, state.Cursor);
        Assert.Equal("h3", state.Entries[state.Cursor].Hash);
    }

    [Fact]
    public void RemoveByIndexTakesTheEntryOnlyWhenTheLocationStillMatches()
    {
        // Hash-targeted removal cannot tell apart entries whose hash is unlearned (all
        // ""), so the iPad removes by index with the location it believes is there. A
        // stale view must remove nothing rather than the wrong song.
        var state = SetlistState.Empty
            .Apply(new SetlistIntent { CommandId = "1", Kind = SetlistIntentKind.Add, Entry = new("", "First", "A", @"C:\songs\first") })
            .Apply(new SetlistIntent { CommandId = "2", Kind = SetlistIntentKind.Add, Entry = new("", "Second", "A", @"C:\songs\second") });

        var removed = state.Apply(new SetlistIntent
        {
            CommandId = "3",
            Kind = SetlistIntentKind.Remove,
            Cursor = 1,
            Location = @"C:\songs\second",
        });

        Assert.Equal(["First"], removed.Entries.Select(entry => entry.Title));

        var stale = state.Apply(new SetlistIntent
        {
            CommandId = "4",
            Kind = SetlistIntentKind.Remove,
            Cursor = 0,
            Location = @"C:\songs\second",
        });

        Assert.Equal(2, stale.Entries.Count);
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Library;
using Cantina.Barkeep.Setlist;

namespace Cantina.Barkeep.Acquisition;

/// <summary>
/// Puts the arrived song next in the setlist through the same journal every other
/// mutation uses (D-023): the intent is on disk before the coordinator hears "applied",
/// and a replayed idempotency key converges instead of inserting twice.
/// </summary>
public sealed class JournalSetlistPort(
    SetlistJournal journal,
    SongIndex index,
    TimeProvider clock) : ISetlistPort
{
    public ValueTask<SetlistInsertOutcome> InsertNextAsync(
        SongIdentity song,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var indexed = index.FindByLocation(song.Value);

        if (indexed is null)
        {
            // The coordinator indexed it two steps ago; gone now means something moved it.
            return ValueTask.FromResult(SetlistInsertOutcome.Rejected);
        }

        var entry = new SetlistEntry(
            indexed.LearnedHash ?? string.Empty,
            indexed.Title,
            indexed.Artist,
            indexed.Location);

        var applied = journal.Append(
            new SetlistIntent
            {
                CommandId = $"insert-next-{idempotencyKey}",
                Kind = SetlistIntentKind.InsertNext,
                Entry = entry,
            },
            clock,
            out var outcome);

        // A replayed command id whose recorded outcome is Ambiguous is the D-023 crash
        // window: the intent was journaled, the outcome was not, and recovery refused to
        // re-execute blindly. Here the world is checkable - either the song is in the
        // setlist or it is not - so this port converges instead of rejecting forever: if
        // absent, retry once under a recovery-suffixed id; if present, it already applied.
        if (!applied && outcome == SetlistOutcome.Ambiguous)
        {
            if (journal.State.Entries.Any(existing =>
                string.Equals(existing.Location, entry.Location, StringComparison.OrdinalIgnoreCase)))
            {
                return ValueTask.FromResult(SetlistInsertOutcome.AlreadyApplied);
            }

            applied = journal.Append(
                new SetlistIntent
                {
                    CommandId = $"insert-next-{idempotencyKey}-recovered",
                    Kind = SetlistIntentKind.InsertNext,
                    Entry = entry,
                },
                clock,
                out outcome);
        }

        return ValueTask.FromResult(outcome switch
        {
            SetlistOutcome.Done when applied => SetlistInsertOutcome.Applied,
            SetlistOutcome.Done => SetlistInsertOutcome.AlreadyApplied,
            _ => SetlistInsertOutcome.Rejected,
        });
    }
}

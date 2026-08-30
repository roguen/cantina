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

        var applied = journal.Append(
            new SetlistIntent
            {
                CommandId = $"insert-next-{idempotencyKey}",
                Kind = SetlistIntentKind.InsertNext,
                Entry = new SetlistEntry(
                    indexed.LearnedHash ?? string.Empty,
                    indexed.Title,
                    indexed.Artist,
                    indexed.Location),
            },
            clock,
            out var outcome);

        return ValueTask.FromResult(outcome switch
        {
            SetlistOutcome.Done when applied => SetlistInsertOutcome.Applied,
            SetlistOutcome.Done => SetlistInsertOutcome.AlreadyApplied,
            _ => SetlistInsertOutcome.Rejected,
        });
    }
}

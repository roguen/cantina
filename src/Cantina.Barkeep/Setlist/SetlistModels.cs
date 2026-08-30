// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace Cantina.Barkeep.Setlist;

/// <summary>
/// One entry in the setlist: the identity Cantina cues by and displays. Location is the
/// folder path (the durable join key, D-025); Hash is YARG's learned content hash and may
/// be empty until first observed. Older journal lines carry no location and deserialize
/// with null, which the cue matcher tolerates.
/// </summary>
public sealed record SetlistEntry(string Hash, string Title, string Artist, string? Location = null);

/// <summary>
/// The mutations the setlist accepts. Every intent carries the client-supplied command id
/// that makes it idempotent (D-023): a duplicate id is answered from the journal, never
/// re-applied.
/// </summary>
public sealed record SetlistIntent
{
    public required string CommandId { get; init; }

    public required SetlistIntentKind Kind { get; init; }

    /// <summary>The entry for Add; null otherwise.</summary>
    public SetlistEntry? Entry { get; init; }

    /// <summary>The target hash for Remove; null otherwise.</summary>
    public string? Hash { get; init; }

    /// <summary>The target cursor for MoveCursor; for Remove, the index to remove at.</summary>
    public int? Cursor { get; init; }

    /// <summary>
    /// For Remove: the location the entry at <see cref="Cursor"/> is expected to hold.
    /// Hash-targeted removal cannot tell apart entries whose hash is still unlearned
    /// (they all carry ""), so the iPad removes by index instead - and the expected
    /// location makes a stale view remove nothing rather than the wrong song.
    /// </summary>
    public string? Location { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<SetlistIntentKind>))]
public enum SetlistIntentKind
{
    Add = 0,
    Remove = 1,
    MoveCursor = 2,
    Clear = 3,

    /// <summary>
    /// Cue the entry in YARG. Unlike the pure mutations above, a cue acts on the world,
    /// so its outcome is recorded only after observation — the two-phase path
    /// (<c>AppendPending</c>/<c>Resolve</c>) rather than the immediate one. It changes no
    /// setlist state.
    /// </summary>
    Cue = 4,

    /// <summary>
    /// Insert the entry immediately after the cursor — the play-next slot the acquisition
    /// pipeline promises for a song that just arrived (docs/geomitron-bridge-integration.md).
    /// </summary>
    InsertNext = 5,
}

/// <summary>
/// Outcomes per D-023. <see cref="Ambiguous"/> is first-class: an intent whose outcome
/// was never observed is recovered as ambiguous and never blindly re-executed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SetlistOutcome>))]
public enum SetlistOutcome
{
    Done = 0,
    Failed = 1,
    Ambiguous = 2,
}

/// <summary>The setlist: ordered songs plus the cursor (project glossary).</summary>
public sealed record SetlistState
{
    public static readonly SetlistState Empty = new() { Entries = [], Cursor = 0 };

    public required IReadOnlyList<SetlistEntry> Entries { get; init; }

    /// <summary>
    /// Index of the active entry. Clamped to the list, and kept meaningful across
    /// removals: removing before the cursor shifts it back with the songs it points at.
    /// </summary>
    public required int Cursor { get; init; }

    /// <summary>
    /// Applies a done intent. Pure, total, and order-deterministic: replaying the journal
    /// always reproduces the same state. Unknown targets are no-ops rather than errors,
    /// because a replayed remove of an already-removed song must converge, not throw.
    /// </summary>
    public SetlistState Apply(SetlistIntent intent)
    {
        switch (intent.Kind)
        {
            case SetlistIntentKind.Add when intent.Entry is not null:
                {
                    var entries = new List<SetlistEntry>(Entries) { intent.Entry };
                    return this with { Entries = entries };
                }

            case SetlistIntentKind.Remove:
                {
                    // Index-targeted with an expected location (the iPad's shape), or
                    // hash-targeted (the original shape). Either way an unknown target
                    // is a no-op, because a replayed remove must converge, not throw.
                    int index;

                    if (intent.Cursor is { } at)
                    {
                        if (at < 0 || at >= Entries.Count
                            || (intent.Location is { Length: > 0 } expected
                                && !string.Equals(Entries[at].Location, expected, StringComparison.OrdinalIgnoreCase)))
                        {
                            return this;
                        }

                        index = at;
                    }
                    else if (intent.Hash is not null)
                    {
                        index = IndexOf(intent.Hash);

                        if (index < 0)
                        {
                            return this;
                        }
                    }
                    else
                    {
                        return this;
                    }

                    var entries = new List<SetlistEntry>(Entries);
                    entries.RemoveAt(index);
                    var cursor = index < Cursor ? Cursor - 1 : Cursor;
                    return new SetlistState { Entries = entries, Cursor = Clamp(cursor, entries.Count) };
                }

            case SetlistIntentKind.InsertNext when intent.Entry is not null:
                {
                    // After the cursor, or first into an empty list. The cursor does not
                    // move: the current song stays current, and the arrival plays next.
                    var entries = new List<SetlistEntry>(Entries);
                    var slot = entries.Count == 0 ? 0 : Math.Min(Cursor + 1, entries.Count);
                    entries.Insert(slot, intent.Entry);
                    return this with { Entries = entries };
                }

            case SetlistIntentKind.MoveCursor when intent.Cursor is { } target:
                return this with { Cursor = Clamp(target, Entries.Count) };

            case SetlistIntentKind.Clear:
                return Empty;

            default:
                return this;
        }
    }

    private int IndexOf(string hash)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Hash, hash, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static int Clamp(int cursor, int count) =>
        count == 0 ? 0 : Math.Clamp(cursor, 0, count - 1);
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json.Serialization;

namespace Cantina.Barkeep.Setlist;

/// <summary>One entry in the setlist: the identity Cantina cues by and displays.</summary>
public sealed record SetlistEntry(string Hash, string Title, string Artist);

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

    /// <summary>The target cursor for MoveCursor; null otherwise.</summary>
    public int? Cursor { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<SetlistIntentKind>))]
public enum SetlistIntentKind
{
    Add = 0,
    Remove = 1,
    MoveCursor = 2,
    Clear = 3,
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

            case SetlistIntentKind.Remove when intent.Hash is not null:
                {
                    var index = IndexOf(intent.Hash);

                    if (index < 0)
                    {
                        return this;
                    }

                    var entries = new List<SetlistEntry>(Entries);
                    entries.RemoveAt(index);
                    var cursor = index < Cursor ? Cursor - 1 : Cursor;
                    return new SetlistState { Entries = entries, Cursor = Clamp(cursor, entries.Count) };
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

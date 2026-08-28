// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;
using System.Text.Json;

namespace Cantina.Barkeep.Setlist;

/// <summary>
/// The write-ahead journal of D-023. Durability lives here and nowhere else, because
/// graceful shutdown does not exist on the target host (D-019): an intent is flushed to
/// disk before it is applied or acknowledged, and its outcome is appended when observed.
///
/// Storage is a JSON-lines journal plus a compacted snapshot, both atomically replaced.
/// A file that fails to parse is set aside as <c>*.corrupt-&lt;stamp&gt;</c> and the
/// previous good state carries on — corruption is quarantined and reported, never
/// silently truncated and never fatal. A torn tail line (the expected shape of a
/// mid-append kill) is quarantined the same way, keeping every complete line before it.
///
/// Single-writer by design: one Barkeep, one theater. Callers serialize through
/// <see cref="Append"/>'s lock.
/// </summary>
public sealed class SetlistJournal : IDisposable
{
    private const string JournalName = "setlist-journal.jsonl";
    private const string SnapshotName = "setlist-snapshot.json";
    private const int SnapshotVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly string _directory;
    private readonly Dictionary<string, SetlistOutcome> _outcomes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SetlistIntent> _pending = new(StringComparer.Ordinal);
    private readonly List<string> _quarantined = [];
    private FileStream _journal;
    private SetlistState _state = SetlistState.Empty;

    private SetlistJournal(string directory, FileStream journal)
    {
        _directory = directory;
        _journal = journal;
    }

    /// <summary>The state produced by replaying every done intent.</summary>
    public SetlistState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>
    /// Intents recovered without an outcome, now marked ambiguous (D-023). Surfaced so
    /// the client can confirm; never re-executed.
    /// </summary>
    public IReadOnlyList<SetlistIntent> RecoveredAmbiguous
    {
        get
        {
            lock (_gate)
            {
                return [.. _pending.Values];
            }
        }
    }

    /// <summary>Files set aside during recovery, for the honest "state recovered" report.</summary>
    public IReadOnlyList<string> QuarantinedFiles
    {
        get
        {
            lock (_gate)
            {
                return [.. _quarantined];
            }
        }
    }

    /// <summary>
    /// Opens the journal, replaying snapshot plus journal into current state. Intents
    /// found without outcomes become ambiguous: their outcome is appended now, so the
    /// recovery itself is durable and a second crash replays identically.
    /// </summary>
    public static SetlistJournal Open(string directory, TimeProvider clock)
    {
        Directory.CreateDirectory(directory);

        var instance = new SetlistJournal(directory, OpenAppend(Path.Combine(directory, JournalName)));
        instance.Recover(clock);
        return instance;
    }

    /// <summary>
    /// Appends and flushes the intent, then applies it. Returns false with the recorded
    /// outcome when the command id was already journaled — the idempotent replay path.
    /// The write reaches disk before this method returns (flush-to-disk), which is the
    /// entire durability contract: acknowledge-after-flush, never after process exit.
    /// </summary>
    public bool Append(SetlistIntent intent, TimeProvider clock, out SetlistOutcome outcome)
    {
        lock (_gate)
        {
            if (_outcomes.TryGetValue(intent.CommandId, out var existing))
            {
                outcome = existing;
                return false;
            }

            if (_pending.ContainsKey(intent.CommandId))
            {
                outcome = SetlistOutcome.Ambiguous;
                return false;
            }

            WriteLine(new JournalLine
            {
                Kind = LineKind.Intent,
                CommandId = intent.CommandId,
                Intent = intent,
                At = clock.GetUtcNow(),
            });

            // Setlist mutations are pure server state: applying cannot fail and touches
            // nothing external, so the outcome is recordable immediately. Commands that
            // act on YARG journal their outcome only after observation (D-015: a sent
            // keystroke is never evidence of success).
            _state = _state.Apply(intent);

            WriteLine(new JournalLine
            {
                Kind = LineKind.Outcome,
                CommandId = intent.CommandId,
                Outcome = SetlistOutcome.Done,
                At = clock.GetUtcNow(),
            });

            _outcomes[intent.CommandId] = SetlistOutcome.Done;
            outcome = SetlistOutcome.Done;
            return true;
        }
    }

    /// <summary>
    /// Compacts: snapshot the current state atomically, then start a fresh journal. The
    /// snapshot is written to a temp file, flushed, and renamed over the old one, so a
    /// kill at any instant leaves either the old pair or the new pair, never a mix that
    /// loses an acknowledged mutation.
    /// </summary>
    public void Compact()
    {
        lock (_gate)
        {
            var snapshotPath = Path.Combine(_directory, SnapshotName);
            var temp = snapshotPath + ".tmp";

            var payload = JsonSerializer.Serialize(
                new Snapshot
                {
                    Version = SnapshotVersion,
                    State = _state,
                    Outcomes = new Dictionary<string, SetlistOutcome>(_outcomes, StringComparer.Ordinal),
                },
                Json);

            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(payload);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temp, snapshotPath, overwrite: true);

            _journal.Dispose();
            _journal = new FileStream(
                Path.Combine(_directory, JournalName),
                FileMode.Create,
                FileAccess.Write,
                FileShare.Read);
        }
    }

    /// <summary>Deletes journal and snapshot: the explicit, user-visible reset of D-023.</summary>
    public static void Reset(string directory)
    {
        File.Delete(Path.Combine(directory, JournalName));
        File.Delete(Path.Combine(directory, SnapshotName));
    }

    public void Dispose() => _journal.Dispose();

    private void Recover(TimeProvider clock)
    {
        var snapshotPath = Path.Combine(_directory, SnapshotName);

        if (File.Exists(snapshotPath))
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(snapshotPath), Json);

                if (snapshot is null || snapshot.Version != SnapshotVersion)
                {
                    // An unknown version refuses to guess and offers the reset path
                    // (D-023). Quarantine and continue from the journal alone.
                    Quarantine(snapshotPath, clock);
                }
                else
                {
                    _state = snapshot.State;

                    foreach (var pair in snapshot.Outcomes)
                    {
                        _outcomes[pair.Key] = pair.Value;
                    }
                }
            }
            catch (JsonException)
            {
                Quarantine(snapshotPath, clock);
            }
        }

        var journalPath = Path.Combine(_directory, JournalName);
        var tornTail = false;

        foreach (var line in ReadLines(journalPath))
        {
            JournalLine? record;

            try
            {
                record = JsonSerializer.Deserialize<JournalLine>(line, Json);
            }
            catch (JsonException)
            {
                // A torn final line is the expected shape of a mid-append kill: keep
                // everything before it, quarantine the file for inspection, and carry on.
                tornTail = true;
                break;
            }

            if (record is null)
            {
                tornTail = true;
                break;
            }

            switch (record.Kind)
            {
                case LineKind.Intent when record.Intent is not null:
                    _pending[record.CommandId] = record.Intent;
                    break;

                case LineKind.Outcome when record.Outcome is { } recorded:
                    if (_pending.Remove(record.CommandId, out var pendingIntent) && recorded == SetlistOutcome.Done)
                    {
                        _state = _state.Apply(pendingIntent);
                    }

                    _outcomes[record.CommandId] = recorded;
                    break;

                default:
                    break;
            }
        }

        if (tornTail)
        {
            _journal.Dispose();
            Quarantine(journalPath, clock);
            _journal = OpenAppend(journalPath);

            // Re-journal the surviving picture so the quarantined file is not load-bearing.
            Compact();
        }

        // D-023's recovery rule: an intent without an outcome is ambiguous, durably, so a
        // second crash replays identically and nothing is ever blindly re-executed.
        foreach (var pending in _pending)
        {
            WriteLine(new JournalLine
            {
                Kind = LineKind.Outcome,
                CommandId = pending.Key,
                Outcome = SetlistOutcome.Ambiguous,
                At = clock.GetUtcNow(),
            });

            _outcomes[pending.Key] = SetlistOutcome.Ambiguous;
        }
    }

    private void WriteLine(JournalLine line)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(line, Json) + "\n");
        _journal.Write(bytes);
        _journal.Flush(flushToDisk: true);
    }

    private void Quarantine(string path, TimeProvider clock)
    {
        var target = $"{path}.corrupt-{clock.GetUtcNow():yyyyMMdd-HHmmss-fff}";
        File.Move(path, target, overwrite: true);
        _quarantined.Add(target);
    }

    private static FileStream OpenAppend(string path) =>
        new(path, FileMode.Append, FileAccess.Write, FileShare.Read);

    private static IEnumerable<string> ReadLines(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        using var reader = new StreamReader(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0)
            {
                yield return line;
            }
        }
    }

    private enum LineKind
    {
        Intent = 0,
        Outcome = 1,
    }

    private sealed record JournalLine
    {
        public required LineKind Kind { get; init; }

        public required string CommandId { get; init; }

        public SetlistIntent? Intent { get; init; }

        public SetlistOutcome? Outcome { get; init; }

        public required DateTimeOffset At { get; init; }
    }

    private sealed record Snapshot
    {
        public required int Version { get; init; }

        public required SetlistState State { get; init; }

        public required Dictionary<string, SetlistOutcome> Outcomes { get; init; }
    }
}

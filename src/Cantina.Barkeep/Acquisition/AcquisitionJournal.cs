// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text;
using System.Text.Json;

namespace Cantina.Barkeep.Acquisition;

/// <summary>
/// The durable memory of what has been imported — D-023's write-ahead discipline applied
/// to acquisition. A lease line is flushed before any work starts; a receipt line is
/// flushed when the outcome is known; and on restart a receipt answers instantly while a
/// lease with no receipt is treated as **claimable again**, because every step of the
/// pipeline is idempotent: stabilizing re-probes, indexing re-scans, the setlist insert
/// replays by key, and the cue only fires against an idle game. Re-running a crashed
/// import is therefore convergence, not repetition — the one thing that must never repeat
/// is a *completed* one, and the receipt is what prevents that.
/// </summary>
public sealed class AcquisitionJournal : IImportPlayNextJournal, IDisposable
{
    private const string FileName = "acquisition-journal.jsonl";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly object _gate = new();
    private readonly FileStream _stream;
    private readonly Dictionary<string, string> _fingerprints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImportPlayNextTerminalReceipt> _receipts = new(StringComparer.Ordinal);
    private readonly HashSet<string> _activeLeases = new(StringComparer.Ordinal);

    private AcquisitionJournal(FileStream stream) => _stream = stream;

    public static AcquisitionJournal Open(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);

        // A kill mid-append leaves a torn tail with no newline; without this, the next
        // line written would concatenate onto the fragment and BOTH records would fail
        // to parse on the following replay. One newline makes the torn line the only
        // casualty, which is what D-023's torn-tail tolerance intends.
        if (stream.Length > 0)
        {
            using var tail = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            tail.Seek(-1, SeekOrigin.End);

            if (tail.ReadByte() != '\n')
            {
                stream.Write("\n"u8);
                stream.Flush(flushToDisk: true);
            }
        }

        var journal = new AcquisitionJournal(stream);
        journal.Replay(path);
        return journal;
    }

    public ValueTask<ImportPlayNextClaim> ClaimAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_fingerprints.TryGetValue(idempotencyKey, out var known) &&
                !string.Equals(known, candidate.Fingerprint, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(new ImportPlayNextClaim(ImportPlayNextClaimState.Conflict));
            }

            if (_receipts.TryGetValue(idempotencyKey, out var receipt))
            {
                return ValueTask.FromResult(new ImportPlayNextClaim(
                    ImportPlayNextClaimState.Terminal,
                    TerminalReceipt: receipt));
            }

            // Active in this process: someone is already importing it right now.
            if (!_activeLeases.Add(idempotencyKey))
            {
                return ValueTask.FromResult(new ImportPlayNextClaim(ImportPlayNextClaimState.InProgress));
            }

            var leaseId = Guid.NewGuid().ToString("N");

            try
            {
                Write(new JournalLine("lease", idempotencyKey, candidate.Fingerprint, leaseId, null, null));
            }
            catch
            {
                // The lease line never reached disk, so nothing may believe it exists in
                // memory either - otherwise every retry answers InProgress until restart.
                _activeLeases.Remove(idempotencyKey);
                throw;
            }

            _fingerprints[idempotencyKey] = candidate.Fingerprint;

            return ValueTask.FromResult(new ImportPlayNextClaim(
                ImportPlayNextClaimState.Acquired,
                leaseId));
        }
    }

    public ValueTask FinalizeAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        string leaseId,
        ImportPlayNextTerminalReceipt receipt,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            try
            {
                Write(new JournalLine(
                    "receipt",
                    idempotencyKey,
                    candidate.Fingerprint,
                    leaseId,
                    receipt.Outcome.ToString(),
                    receipt.FailureCode));
                _receipts[idempotencyKey] = receipt;
            }
            finally
            {
                // Released even when the write throws: the receipt is not claimed to
                // exist (the caller sees the exception), but wedging the key as
                // InProgress until restart would make every retry silently no-op. A
                // re-claim re-runs idempotent work, which is the designed recovery.
                _activeLeases.Remove(idempotencyKey);
            }
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A terminal <em>failure</em> may be retried deliberately — a later sweep finding the
    /// same fingerprint calls this to clear the receipt so the import can run again.
    /// Completed and ambiguous receipts stay: completion must not repeat, and ambiguity
    /// needs eyes, not retries.
    /// </summary>
    public bool ForgetFailure(string idempotencyKey)
    {
        lock (_gate)
        {
            if (_receipts.TryGetValue(idempotencyKey, out var receipt) &&
                receipt.Outcome is ImportPlayNextOutcome.Failed or ImportPlayNextOutcome.Canceled)
            {
                Write(new JournalLine("forget", idempotencyKey, null, null, null, null));
                _receipts.Remove(idempotencyKey);
                return true;
            }

            return false;
        }
    }

    private void Replay(string path)
    {
        using var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var text = new StreamReader(reader, Encoding.UTF8);

        while (text.ReadLine() is { } line)
        {
            if (line.Length == 0)
            {
                continue;
            }

            JournalLine? entry;

            try
            {
                entry = JsonSerializer.Deserialize<JournalLine>(line, Json);
            }
            catch (JsonException)
            {
                // A torn tail is the expected shape of a mid-append kill (D-023); every
                // complete line before it still counts.
                continue;
            }

            if (entry is null)
            {
                continue;
            }

            switch (entry.Kind)
            {
                case "lease" when entry.Fingerprint is not null:
                    _fingerprints[entry.Key] = entry.Fingerprint;
                    break;

                case "receipt" when entry.Outcome is not null:
                    // TryParse, because this file outlives binaries: a receipt written by
                    // a newer Barkeep must not prevent an older one from starting. An
                    // unrecognized outcome is skipped - the key becomes claimable, and
                    // re-running idempotent work beats refusing to boot.
                    if (Enum.TryParse<ImportPlayNextOutcome>(entry.Outcome, out var outcome))
                    {
                        _receipts[entry.Key] = new ImportPlayNextTerminalReceipt(outcome, entry.FailureCode);
                    }

                    break;

                case "forget":
                    _receipts.Remove(entry.Key);
                    break;
            }
        }
    }

    private void Write(JournalLine line)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(line, Json) + "\n");
        _stream.Write(bytes);
        _stream.Flush(flushToDisk: true);
    }

    public void Dispose() => _stream.Dispose();

    private sealed record JournalLine(
        string Kind,
        string Key,
        string? Fingerprint,
        string? LeaseId,
        string? Outcome,
        string? FailureCode);
}

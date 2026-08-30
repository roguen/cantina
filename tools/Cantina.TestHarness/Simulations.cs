// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Acquisition;

namespace Cantina.TestHarness;

internal sealed class HarnessCallLog
{
    private readonly Lock _gate = new();
    private readonly List<string> _calls = [];

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _calls.Count;
            }
        }
    }

    public void Add(string call)
    {
        lock (_gate)
        {
            _calls.Add(call);
        }
    }

    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return _calls.ToArray();
        }
    }
}

internal sealed class ManualHarnessTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration) => _utcNow += duration;
}

internal sealed class ScriptedSongArrivalPort(
    IEnumerable<SongArrivalProbeResult> probes,
    HarnessCallLog calls) : ISongArrivalPort
{
    private readonly Queue<SongArrivalProbeResult> _probes = new(probes);

    public ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = _probes.Dequeue();
        calls.Add($"arrival.probe:{HarnessNames.Probe(result.State)}");
        return ValueTask.FromResult(result);
    }
}

internal sealed class CancelingSongArrivalPort(
    HarnessCallLog calls,
    CancellationTokenSource cancellation) : ISongArrivalPort
{
    public ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        calls.Add("arrival.probe:cancel");
        cancellation.Cancel();
        cancellationToken.ThrowIfCancellationRequested();
        throw new InvalidOperationException("The cancellation token was not canceled.");
    }
}

internal sealed class ThrowingSongArrivalPort(HarnessCallLog calls) : ISongArrivalPort
{
    public ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add("arrival.probe:throw");
        throw new HarnessAdapterException();
    }
}

internal sealed class CoordinatedSongArrivalPort(HarnessCallLog calls) : ISongArrivalPort
{
    private readonly TaskCompletionSource _firstEntry =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _unexpectedEntry =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseFirst =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _entryCount;

    public Task FirstEntry => _firstEntry.Task;

    public Task UnexpectedEntry => _unexpectedEntry.Task;

    public async ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        var entry = Interlocked.Increment(ref _entryCount);
        if (entry == 1)
        {
            calls.Add("arrival.probe:blocked");
            _firstEntry.TrySetResult();
            await _releaseFirst.Task.WaitAsync(cancellationToken);
            return SongArrivalProbeResult.Ready();
        }

        calls.Add("arrival.probe:unexpected-duplicate");
        _unexpectedEntry.TrySetResult();
        return SongArrivalProbeResult.Rejected("unexpected-duplicate-port-entry");
    }

    public void ReleaseFirst() => _releaseFirst.TrySetResult();
}

internal sealed class ScriptedSongIndexPort(
    SongIndexResult result,
    HarnessCallLog calls) : ISongIndexPort
{
    public ValueTask<SongIndexResult> ValidateAndIndexAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add($"song.validate-index:{candidate.RelativePath}");
        return ValueTask.FromResult(result);
    }
}

internal enum ScriptedCommandFault
{
    None,
    CancelAfterDispatch,
    ThrowAfterDispatch,
}

internal sealed class ScriptedYargSessionPort : IYargSessionPort
{
    private readonly Queue<YargSessionSnapshot> _snapshots;
    private readonly ExternalCommandOutcome _refreshOutcome;
    private readonly bool _songBecomesVisible;
    private readonly ExternalCommandOutcome _cueOutcome;
    private readonly HarnessCallLog _calls;
    private readonly ScriptedCommandFault _refreshFault;
    private readonly ScriptedCommandFault _cueFault;

    public ScriptedYargSessionPort(
        IEnumerable<YargSessionSnapshot> snapshots,
        ExternalCommandOutcome refreshOutcome,
        bool songBecomesVisible,
        ExternalCommandOutcome cueOutcome,
        HarnessCallLog calls,
        ScriptedCommandFault refreshFault = ScriptedCommandFault.None,
        ScriptedCommandFault cueFault = ScriptedCommandFault.None)
    {
        _snapshots = new Queue<YargSessionSnapshot>(snapshots);
        _refreshOutcome = refreshOutcome;
        _songBecomesVisible = songBecomesVisible;
        _cueOutcome = cueOutcome;
        _calls = calls;
        _refreshFault = refreshFault;
        _cueFault = cueFault;
    }

    public ValueTask<YargSessionSnapshot> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = _snapshots.Dequeue();
        _calls.Add($"yarg.observe:{HarnessNames.Activity(snapshot.Activity)}");
        return ValueTask.FromResult(snapshot);
    }

    public ValueTask<ExternalCommandOutcome> RequestLibraryRefreshAsync(
        SongIdentity song,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"yarg.refresh:{song.Value}");
        return ValueTask.FromResult(Dispatch(_refreshFault, _refreshOutcome, cancellationToken));
    }

    public ValueTask<bool> WaitForSongVisibleAsync(
        SongIdentity song,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"yarg.resolve:{song.Value}");
        return ValueTask.FromResult(_songBecomesVisible);
    }

    public ValueTask<ExternalCommandOutcome> CueAsync(
        SongIdentity song,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _calls.Add($"yarg.cue:{song.Value}");
        return ValueTask.FromResult(Dispatch(_cueFault, _cueOutcome, cancellationToken));
    }

    private static ExternalCommandOutcome Dispatch(
        ScriptedCommandFault fault,
        ExternalCommandOutcome outcome,
        CancellationToken cancellationToken) => fault switch
        {
            ScriptedCommandFault.None => outcome,
            ScriptedCommandFault.CancelAfterDispatch =>
                throw new OperationCanceledException(cancellationToken),
            ScriptedCommandFault.ThrowAfterDispatch => throw new HarnessAdapterException(),
            _ => throw new ArgumentOutOfRangeException(nameof(fault), fault, null),
        };
}

internal sealed class HarnessAdapterException : Exception;

internal sealed class InMemorySetlistPort(HarnessCallLog calls) : ISetlistPort
{
    private readonly HashSet<string> _appliedKeys = new(StringComparer.Ordinal);

    public ValueTask<SetlistInsertOutcome> InsertNextAsync(
        SongIdentity song,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        calls.Add($"setlist.insert-next:{song.Value}");
        var outcome = _appliedKeys.Add(idempotencyKey)
            ? SetlistInsertOutcome.Applied
            : SetlistInsertOutcome.AlreadyApplied;
        return ValueTask.FromResult(outcome);
    }
}

// This journal proves atomicity only among callers sharing this in-memory instance. It
// makes no durability claim across process or machine restarts.
internal sealed class InMemoryImportPlayNextJournal : IImportPlayNextJournal
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, JournalEntry> _entries = new(StringComparer.Ordinal);
    private readonly TaskCompletionSource _firstClaimAcquired =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextLease;

    public Task FirstClaimAcquired => _firstClaimAcquired.Task;

    public ValueTask<ImportPlayNextClaim> ClaimAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_entries.TryGetValue(idempotencyKey, out var entry))
            {
                var leaseId = $"lease-{++_nextLease:D4}";
                _entries.Add(idempotencyKey, new JournalEntry(candidate, leaseId));
                _firstClaimAcquired.TrySetResult();
                return ValueTask.FromResult(
                    new ImportPlayNextClaim(
                        ImportPlayNextClaimState.Acquired,
                        leaseId));
            }

            if (entry.Candidate != candidate)
            {
                return ValueTask.FromResult(
                    new ImportPlayNextClaim(ImportPlayNextClaimState.Conflict));
            }

            return entry.TerminalReceipt is null
                ? ValueTask.FromResult(
                    new ImportPlayNextClaim(ImportPlayNextClaimState.InProgress))
                : ValueTask.FromResult(
                    new ImportPlayNextClaim(
                        ImportPlayNextClaimState.Terminal,
                        TerminalReceipt: entry.TerminalReceipt));
        }
    }

    public ValueTask FinalizeAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        string leaseId,
        ImportPlayNextTerminalReceipt receipt,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (!_entries.TryGetValue(idempotencyKey, out var entry) ||
                entry.Candidate != candidate ||
                !string.Equals(entry.LeaseId, leaseId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Only the owner of an acquired command lease can finalize it.");
            }

            if (entry.TerminalReceipt is not null && entry.TerminalReceipt != receipt)
            {
                throw new InvalidOperationException(
                    "A command lease cannot be finalized with two different results.");
            }

            entry.TerminalReceipt = receipt;
        }

        return ValueTask.CompletedTask;
    }

    private sealed class JournalEntry(
        SongArrivalCandidate candidate,
        string leaseId)
    {
        public SongArrivalCandidate Candidate { get; } = candidate;

        public string LeaseId { get; } = leaseId;

        public ImportPlayNextTerminalReceipt? TerminalReceipt { get; set; }
    }
}

internal static class HarnessNames
{
    public static string State(ImportPlayNextState state) => state switch
    {
        ImportPlayNextState.Detected => "detected",
        ImportPlayNextState.Stabilizing => "stabilizing",
        ImportPlayNextState.Validating => "validating",
        ImportPlayNextState.Indexed => "indexed",
        ImportPlayNextState.RefreshPending => "refresh-pending",
        ImportPlayNextState.YargVisible => "yarg-visible",
        ImportPlayNextState.Queued => "queued",
        ImportPlayNextState.Cued => "cued",
        ImportPlayNextState.RefreshAmbiguous => "refresh-ambiguous",
        ImportPlayNextState.CueAmbiguous => "cue-ambiguous",
        ImportPlayNextState.Canceled => "canceled",
        ImportPlayNextState.Failed => "failed",
        ImportPlayNextState.InProgress => "in-progress",
        ImportPlayNextState.TerminalReplay => "terminal-replay",
        ImportPlayNextState.IdempotencyConflict => "idempotency-conflict",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static string Outcome(ImportPlayNextOutcome outcome) => outcome switch
    {
        ImportPlayNextOutcome.Completed => "completed",
        ImportPlayNextOutcome.Failed => "failed",
        ImportPlayNextOutcome.Ambiguous => "ambiguous",
        ImportPlayNextOutcome.Canceled => "canceled",
        ImportPlayNextOutcome.InProgress => "in-progress",
        ImportPlayNextOutcome.Conflict => "conflict",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    public static string Probe(SongArrivalProbeState state) => state switch
    {
        SongArrivalProbeState.Stabilizing => "stabilizing",
        SongArrivalProbeState.Ready => "ready",
        SongArrivalProbeState.Rejected => "rejected",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static string Activity(YargActivity activity) => activity switch
    {
        YargActivity.Unknown => "unknown",
        YargActivity.Idle => "idle",
        YargActivity.Active => "active",
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };
}

// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Acquisition;

public sealed class ImportPlayNextCoordinator
{
    private readonly ISongArrivalPort _arrival;
    private readonly ISongIndexPort _index;
    private readonly IYargSessionPort _yarg;
    private readonly ISetlistPort _setlist;
    private readonly IImportPlayNextJournal _journal;
    private readonly TimeProvider _timeProvider;
    private readonly ImportPlayNextOptions _options;

    public ImportPlayNextCoordinator(
        ISongArrivalPort arrival,
        ISongIndexPort index,
        IYargSessionPort yarg,
        ISetlistPort setlist,
        IImportPlayNextJournal journal,
        TimeProvider timeProvider,
        ImportPlayNextOptions? options = null)
    {
        _arrival = arrival;
        _index = index;
        _yarg = yarg;
        _setlist = setlist;
        _journal = journal;
        _timeProvider = timeProvider;
        _options = options ?? new ImportPlayNextOptions();

        ArgumentOutOfRangeException.ThrowIfLessThan(
            _options.MaximumStabilizationAttempts,
            1);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            _options.MaximumSafeStateObservations,
            1);

        if (_options.SessionFreshnessWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The session freshness window cannot be negative.");
        }

        if (_options.TerminalPersistenceTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The terminal-persistence timeout must be positive.");
        }
    }

    public async ValueTask<ImportPlayNextResult> RunAsync(
        ImportPlayNextRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);
        ArgumentNullException.ThrowIfNull(request.Candidate);

        var events = new List<ImportPlayNextEvent>();

        void Record(ImportPlayNextState state, string? detail = null) =>
            events.Add(new ImportPlayNextEvent(events.Count + 1, state, detail));

        var claim = await _journal.ClaimAsync(
            request.IdempotencyKey,
            request.Candidate,
            cancellationToken);
        switch (claim.State)
        {
            case ImportPlayNextClaimState.InProgress:
                Record(ImportPlayNextState.InProgress);
                return new ImportPlayNextResult(
                    ImportPlayNextOutcome.InProgress,
                    events.AsReadOnly());

            case ImportPlayNextClaimState.Terminal:
                var receipt = claim.TerminalReceipt ??
                    throw new InvalidOperationException("A terminal claim requires a receipt.");
                Record(ImportPlayNextState.TerminalReplay);
                return new ImportPlayNextResult(
                    receipt.Outcome,
                    events.AsReadOnly(),
                    receipt.FailureCode,
                    true);

            case ImportPlayNextClaimState.Conflict:
                Record(ImportPlayNextState.IdempotencyConflict, "idempotency-key-reused");
                return new ImportPlayNextResult(
                    ImportPlayNextOutcome.Conflict,
                    events.AsReadOnly(),
                    "idempotency-key-reused");

            case ImportPlayNextClaimState.Acquired:
                break;

            default:
                throw new InvalidOperationException("Unknown import-play-next claim state.");
        }

        var leaseId = claim.LeaseId ??
            throw new InvalidOperationException("An acquired claim requires a lease ID.");
        var uncertainDispatch = ExternalDispatch.None;
        var isFinalizing = false;

        async ValueTask<ImportPlayNextResult> FinishAsync(
            ImportPlayNextOutcome outcome,
            string? failureCode = null)
        {
            var result = new ImportPlayNextResult(
                outcome,
                events.AsReadOnly(),
                failureCode);
            isFinalizing = true;
            using var persistenceTimeout = new CancellationTokenSource(
                _options.TerminalPersistenceTimeout,
                _timeProvider);
            await _journal.FinalizeAsync(
                request.IdempotencyKey,
                request.Candidate,
                leaseId,
                new ImportPlayNextTerminalReceipt(outcome, failureCode),
                persistenceTimeout.Token);
            return result;
        }

        async ValueTask<ImportPlayNextResult> FailedAsync(string failureCode)
        {
            Record(ImportPlayNextState.Failed, failureCode);
            return await FinishAsync(ImportPlayNextOutcome.Failed, failureCode);
        }

        async ValueTask<ImportPlayNextResult> AmbiguousAsync(
            ImportPlayNextState state,
            string failureCode)
        {
            Record(state, failureCode);
            return await FinishAsync(ImportPlayNextOutcome.Ambiguous, failureCode);
        }

        try
        {
            Record(ImportPlayNextState.Detected);

            var isReady = false;
            for (var attempt = 0; attempt < _options.MaximumStabilizationAttempts; attempt++)
            {
                var probe = await _arrival.ProbeAsync(request.Candidate, cancellationToken);
                switch (probe.State)
                {
                    case SongArrivalProbeState.Stabilizing:
                        if (events[^1].State != ImportPlayNextState.Stabilizing)
                        {
                            Record(ImportPlayNextState.Stabilizing);
                        }

                        break;

                    case SongArrivalProbeState.Ready:
                        isReady = true;
                        break;

                    case SongArrivalProbeState.Rejected:
                        return await FailedAsync(probe.FailureCode ?? "arrival-rejected");

                    default:
                        throw new InvalidOperationException("Unknown song-arrival probe state.");
                }

                if (isReady)
                {
                    break;
                }
            }

            if (!isReady)
            {
                return await FailedAsync("stabilization-timeout");
            }

            Record(ImportPlayNextState.Validating);
            var indexResult = await _index.ValidateAndIndexAsync(
                request.Candidate,
                cancellationToken);
            if (!indexResult.IsAccepted || indexResult.Song is null)
            {
                return await FailedAsync(indexResult.FailureCode ?? "validation-rejected");
            }

            var song = indexResult.Song;
            Record(ImportPlayNextState.Indexed, song.Value);
            Record(ImportPlayNextState.RefreshPending);

            var hasSafeSnapshot = false;
            for (var attempt = 0; attempt < _options.MaximumSafeStateObservations; attempt++)
            {
                var snapshot = await _yarg.ObserveAsync(cancellationToken);
                if (IsFresh(snapshot) && snapshot.CanRefreshLibrary)
                {
                    hasSafeSnapshot = true;
                    break;
                }
            }

            if (!hasSafeSnapshot)
            {
                return await FailedAsync("refresh-unsafe");
            }

            uncertainDispatch = ExternalDispatch.Refresh;
            var refreshOutcome = await _yarg.RequestLibraryRefreshAsync(
                song,
                cancellationToken);
            uncertainDispatch = ExternalDispatch.None;
            if (refreshOutcome == ExternalCommandOutcome.Failed)
            {
                return await FailedAsync("refresh-failed");
            }

            if (refreshOutcome == ExternalCommandOutcome.Ambiguous)
            {
                return await AmbiguousAsync(
                    ImportPlayNextState.RefreshAmbiguous,
                    "refresh-outcome-unknown");
            }

            if (refreshOutcome != ExternalCommandOutcome.Succeeded)
            {
                throw new InvalidOperationException("Unknown library-refresh command outcome.");
            }

            if (!await _yarg.WaitForSongVisibleAsync(song, cancellationToken))
            {
                return await FailedAsync("song-not-visible");
            }

            Record(ImportPlayNextState.YargVisible, song.Value);

            var insertion = await _setlist.InsertNextAsync(
                song,
                request.IdempotencyKey,
                cancellationToken);
            if (insertion == SetlistInsertOutcome.Rejected)
            {
                return await FailedAsync("setlist-rejected");
            }

            if (insertion != SetlistInsertOutcome.Applied &&
                insertion != SetlistInsertOutcome.AlreadyApplied)
            {
                throw new InvalidOperationException("Unknown setlist-insertion outcome.");
            }

            Record(ImportPlayNextState.Queued, song.Value);

            var cueSnapshot = await _yarg.ObserveAsync(cancellationToken);
            if (cueSnapshot.Activity == YargActivity.Idle && IsFresh(cueSnapshot))
            {
                uncertainDispatch = ExternalDispatch.Cue;
                var cueOutcome = await _yarg.CueAsync(song, request.IdempotencyKey, cancellationToken);
                uncertainDispatch = ExternalDispatch.None;
                if (cueOutcome == ExternalCommandOutcome.Failed)
                {
                    return await FailedAsync("cue-failed");
                }

                if (cueOutcome == ExternalCommandOutcome.Ambiguous)
                {
                    return await AmbiguousAsync(
                        ImportPlayNextState.CueAmbiguous,
                        "cue-outcome-unknown");
                }

                if (cueOutcome != ExternalCommandOutcome.Succeeded)
                {
                    throw new InvalidOperationException("Unknown cue command outcome.");
                }

                Record(ImportPlayNextState.Cued, song.Value);
            }

            return await FinishAsync(ImportPlayNextOutcome.Completed);
        }
        catch (OperationCanceledException) when (!isFinalizing)
        {
            if (uncertainDispatch == ExternalDispatch.Refresh)
            {
                return await AmbiguousAsync(
                    ImportPlayNextState.RefreshAmbiguous,
                    "refresh-dispatch-canceled");
            }

            if (uncertainDispatch == ExternalDispatch.Cue)
            {
                return await AmbiguousAsync(
                    ImportPlayNextState.CueAmbiguous,
                    "cue-dispatch-canceled");
            }

            const string failureCode = "operation-canceled-before-dispatch";
            Record(ImportPlayNextState.Canceled, failureCode);
            return await FinishAsync(ImportPlayNextOutcome.Canceled, failureCode);
        }
        catch (Exception) when (!isFinalizing)
        {
            if (uncertainDispatch == ExternalDispatch.Refresh)
            {
                return await AmbiguousAsync(
                    ImportPlayNextState.RefreshAmbiguous,
                    "refresh-dispatch-threw");
            }

            if (uncertainDispatch == ExternalDispatch.Cue)
            {
                return await AmbiguousAsync(
                    ImportPlayNextState.CueAmbiguous,
                    "cue-dispatch-threw");
            }

            return await FailedAsync("adapter-error-before-dispatch");
        }
    }

    private bool IsFresh(YargSessionSnapshot snapshot)
    {
        var age = _timeProvider.GetUtcNow() - snapshot.ObservedAt;
        return age >= TimeSpan.Zero && age <= _options.SessionFreshnessWindow;
    }

    private enum ExternalDispatch
    {
        None,
        Refresh,
        Cue,
    }
}

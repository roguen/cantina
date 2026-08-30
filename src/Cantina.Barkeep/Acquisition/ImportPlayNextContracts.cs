// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Acquisition;

public interface ISongArrivalPort
{
    ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken);
}

public interface ISongIndexPort
{
    ValueTask<SongIndexResult> ValidateAndIndexAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken);
}

public interface IYargSessionPort
{
    /// <summary>
    /// Awaits one semantic observation. An adapter may wait for a snapshot newer than
    /// the previous call; the coordinator makes no real-time polling guarantee.
    /// </summary>
    ValueTask<YargSessionSnapshot> ObserveAsync(CancellationToken cancellationToken);

    ValueTask<ExternalCommandOutcome> RequestLibraryRefreshAsync(
        SongIdentity song,
        CancellationToken cancellationToken);

    ValueTask<bool> WaitForSongVisibleAsync(
        SongIdentity song,
        CancellationToken cancellationToken);

    ValueTask<ExternalCommandOutcome> CueAsync(
        SongIdentity song,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface ISetlistPort
{
    ValueTask<SetlistInsertOutcome> InsertNextAsync(
        SongIdentity song,
        string idempotencyKey,
        CancellationToken cancellationToken);
}

public interface IImportPlayNextJournal
{
    ValueTask<ImportPlayNextClaim> ClaimAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Atomically finalizes the lease. Implementations must either store the receipt
    /// for replay or fail without claiming that finalization succeeded. Failure does
    /// not release the lease for automatic retry; recovery is journal-specific.
    /// </summary>
    ValueTask FinalizeAsync(
        string idempotencyKey,
        SongArrivalCandidate candidate,
        string leaseId,
        ImportPlayNextTerminalReceipt receipt,
        CancellationToken cancellationToken);
}

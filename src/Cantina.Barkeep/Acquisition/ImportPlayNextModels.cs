// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Acquisition;

public sealed record SongArrivalCandidate(
    string Source,
    string RelativePath,
    string Fingerprint);

public sealed record SongIdentity(string Value);

public sealed record ImportPlayNextRequest(
    string IdempotencyKey,
    SongArrivalCandidate Candidate);

public enum SongArrivalProbeState
{
    Stabilizing,
    Ready,
    Rejected,
}

public sealed record SongArrivalProbeResult(
    SongArrivalProbeState State,
    string? FailureCode = null)
{
    public static SongArrivalProbeResult Stabilizing() =>
        new(SongArrivalProbeState.Stabilizing);

    public static SongArrivalProbeResult Ready() =>
        new(SongArrivalProbeState.Ready);

    public static SongArrivalProbeResult Rejected(string failureCode) =>
        new(SongArrivalProbeState.Rejected, failureCode);
}

public sealed record SongIndexResult(
    bool IsAccepted,
    SongIdentity? Song,
    string? FailureCode)
{
    public static SongIndexResult Accepted(SongIdentity song) =>
        new(true, song, null);

    public static SongIndexResult Rejected(string failureCode) =>
        new(false, null, failureCode);
}

public enum YargActivity
{
    Unknown,
    Idle,
    Active,
}

public sealed record YargSessionSnapshot(
    YargActivity Activity,
    DateTimeOffset ObservedAt,
    bool CanRefreshLibrary);

public enum ExternalCommandOutcome
{
    Succeeded,
    Failed,
    Ambiguous,
}

public enum SetlistInsertOutcome
{
    Applied,
    AlreadyApplied,
    Rejected,
}

public enum ImportPlayNextClaimState
{
    Acquired,
    InProgress,
    Terminal,
    Conflict,
}

public sealed record ImportPlayNextTerminalReceipt(
    ImportPlayNextOutcome Outcome,
    string? FailureCode);

public sealed record ImportPlayNextClaim(
    ImportPlayNextClaimState State,
    string? LeaseId = null,
    ImportPlayNextTerminalReceipt? TerminalReceipt = null);

public enum ImportPlayNextState
{
    Detected,
    Stabilizing,
    Validating,
    Indexed,
    RefreshPending,
    YargVisible,
    Queued,
    Cued,
    RefreshAmbiguous,
    CueAmbiguous,
    Canceled,
    Failed,
    InProgress,
    TerminalReplay,
    IdempotencyConflict,
}

public sealed record ImportPlayNextEvent(
    int Sequence,
    ImportPlayNextState State,
    string? Detail = null);

public enum ImportPlayNextOutcome
{
    Completed,
    Failed,
    Ambiguous,
    Canceled,
    InProgress,
    Conflict,
}

public sealed record ImportPlayNextResult(
    ImportPlayNextOutcome Outcome,
    IReadOnlyList<ImportPlayNextEvent> Events,
    string? FailureCode = null,
    bool IsReplay = false);

public sealed record ImportPlayNextOptions
{
    public int MaximumStabilizationAttempts { get; init; } = 3;

    public int MaximumSafeStateObservations { get; init; } = 3;

    public TimeSpan SessionFreshnessWindow { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan TerminalPersistenceTimeout { get; init; } = TimeSpan.FromSeconds(2);
}

// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.YargSession;

/// <summary>
/// Freshness tiers from <c>docs/live-state.md</c>. The datagram arrives at ~90.7 Hz, so
/// silence is meaningful quickly — but a healthy run has shown a 538 ms gap, so demotions
/// are debounced by the tracker rather than reported raw.
/// </summary>
public enum LiveFreshness
{
    /// <summary>Newest datagram younger than 500 ms.</summary>
    Live = 0,

    /// <summary>Younger than 5 s. The client shows the last state, visibly dimmed.</summary>
    Stale = 1,

    /// <summary>Older than 5 s, no datagram yet, or a socket fault.</summary>
    Dead = 2,
}

/// <summary>
/// Named faults per <c>docs/failure-behavior.md</c>. A fault is reported with its name,
/// never as an empty or frozen state.
/// </summary>
public enum SessionFault
{
    None = 0,

    /// <summary>No datagram has ever arrived on this listener.</summary>
    NoDatagrams = 1,

    /// <summary>The stream was live and stopped.</summary>
    StreamDead = 2,

    /// <summary>
    /// Datagrams from more than one endpoint. The stream is a LAN broadcast (D-020), so a
    /// second YARG anywhere reaches this listener; interleaving two games manufactured a
    /// withdrawn finding once, so this is surfaced, never resolved by picking a sender.
    /// </summary>
    MultipleSources = 3,

    /// <summary>
    /// The port could not be bound: another application holds the YARG data port without
    /// <c>SO_REUSEADDR</c> (D-013). Not retried — retrying cannot succeed and presents
    /// as a hang.
    /// </summary>
    PortConflict = 4,
}

/// <summary>How the current song identity became known (docs/live-state.md).</summary>
public enum SongSource
{
    Unknown = 0,

    /// <summary>Latched from <c>currentSong.json</c>.</summary>
    Observed = 1,

    /// <summary>Cued by Barkeep and confirmed by observation.</summary>
    CuedByBarkeep = 2,
}

/// <summary>Latched song identity. Carried through the score screen (D-010).</summary>
public sealed record LatchedSong(string Title, string Artist, string Hash);

/// <summary>
/// The live-state snapshot promised to the client, per <c>docs/live-state.md</c>.
/// Everything here is observation; nothing is inference. A field this type cannot fill
/// honestly is absent from it by design — there is no playback position and no score.
/// </summary>
public sealed record LiveState
{
    public required YargScene Scene { get; init; }

    public required YargPlayState PlayState { get; init; }

    /// <summary>Latched identity, or null when no song has been observed since the last menu dwell.</summary>
    public required LatchedSong? Song { get; init; }

    public required SongSource SongSource { get; init; }

    /// <summary>Arrival time of the newest accepted datagram, or null before the first.</summary>
    public required DateTimeOffset? ReceivedAt { get; init; }

    public required LiveFreshness Freshness { get; init; }

    public required SessionFault Fault { get; init; }

    /// <summary>Distinct sender endpoints observed, for the MultipleSources report.</summary>
    public required IReadOnlyList<string> Senders { get; init; }

    public required long DatagramsAccepted { get; init; }

    public required long DatagramsRejected { get; init; }
}

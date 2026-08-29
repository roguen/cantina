// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.YargSession;

/// <summary>
/// Turns raw observations — datagram bytes, <c>currentSong.json</c> content, socket
/// faults — into the <see cref="LiveState"/> promise of <c>docs/live-state.md</c>.
///
/// Deliberately free of I/O and of clocks: callers pass <c>now</c> with every
/// observation, so the deterministic harness can drive it without sleeping (D-008).
/// Thread-safe; the listener, the file poller, and snapshot readers run concurrently.
/// </summary>
public sealed class YargSessionTracker
{
    /// <summary>Raw tier boundary: younger than this is Live (docs/live-state.md).</summary>
    public static readonly TimeSpan LiveThreshold = TimeSpan.FromMilliseconds(500);

    /// <summary>Raw tier boundary: older than this is Dead.</summary>
    public static readonly TimeSpan DeadThreshold = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A demotion must hold this long before it is reported. A healthy run showed a
    /// 538 ms inter-datagram gap (D-018), and flapping Live→Stale on every scheduler
    /// hiccup would make the client flicker — the exact failure D-010's debounce rule
    /// exists to prevent. Promotions are immediate: a fresh datagram is proof.
    /// </summary>
    public static readonly TimeSpan DemotionDebounce = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Menu dwell after which the song latch clears. Matches the human-screen dwell in
    /// the advance-observation rule (docs/live-state.md): a menu held this long means
    /// the score screen is behind us and the latched identity has served its purpose.
    /// </summary>
    public static readonly TimeSpan MenuDwellToClearLatch = TimeSpan.FromSeconds(5);

    private readonly object _gate = new();
    private readonly HashSet<string> _senders = new(StringComparer.Ordinal);

    private YargScene _scene = YargScene.Unknown;
    private YargPlayState _playState = YargPlayState.NoSong;
    private DateTimeOffset? _lastAccepted;
    private DateTimeOffset? _menuSince;
    private LatchedSong? _song;
    private SongSource _songSource = SongSource.Unknown;
    private string _lastSongContentHash = string.Empty;
    private bool _portConflict;
    private long _accepted;
    private long _rejected;

    private LiveFreshness _reportedFreshness = LiveFreshness.Dead;
    private LiveFreshness _candidateFreshness = LiveFreshness.Dead;
    private DateTimeOffset? _candidateSince;

    /// <summary>Feeds one received datagram. Malformed payloads are counted, never guessed at.</summary>
    public void OnDatagram(ReadOnlySpan<byte> payload, string sender, DateTimeOffset now)
    {
        if (!YargDatagram.TryParse(payload, out var datagram, out _) || datagram is null)
        {
            lock (_gate)
            {
                _rejected++;
            }

            return;
        }

        lock (_gate)
        {
            _accepted++;
            _lastAccepted = now;
            _senders.Add(sender);

            _scene = datagram.Scene;
            _playState = datagram.PlayState;

            if (datagram.Scene == YargScene.Menu)
            {
                _menuSince ??= now;

                if (_song is not null && now - _menuSince >= MenuDwellToClearLatch)
                {
                    _song = null;
                    _songSource = SongSource.Unknown;

                    // The dedup hash resets WITH the latch. The self-test caught the
                    // asymmetry: currentSong.json populates during the load screen, up to
                    // ~2 s before gameplay datagrams begin, so a latch taken during a
                    // stale menu dwell is cleared here - and without this reset the
                    // unchanged file hash blocked every re-latch of the same song, which
                    // is exactly the replay-the-same-track case a theater hits nightly.
                    _lastSongContentHash = string.Empty;
                }
            }
            else
            {
                _menuSince = null;
            }
        }
    }

    /// <summary>
    /// Feeds one read of <c>currentSong.json</c>. Empty content is a real value — no song
    /// loaded — and deliberately does NOT clear the latch: the file clears ~86 ms after
    /// the scene changes, and identity must survive the score screen (D-010).
    /// </summary>
    public void OnCurrentSong(string? content)
    {
        if (!CurrentSongDocument.TryParse(content, out var document, out _) || document is null)
        {
            return;
        }

        lock (_gate)
        {
            if (document.Hash == _lastSongContentHash)
            {
                return;
            }

            _lastSongContentHash = document.Hash;
            _song = new LatchedSong(document.Title, document.Artist, document.Hash, document.Location);
            _songSource = SongSource.Observed;
        }
    }

    /// <summary>
    /// Records that the listener could not bind the port (D-013's named fault). The
    /// tracker carries the fault so the client sees "another application holds the YARG
    /// data port", never an unexplained dead stream.
    /// </summary>
    public void ReportPortConflict()
    {
        lock (_gate)
        {
            _portConflict = true;
        }
    }

    /// <summary>Computes the snapshot at <paramref name="now"/>.</summary>
    public LiveState Snapshot(DateTimeOffset now)
    {
        lock (_gate)
        {
            var raw = RawFreshness(now);
            var reported = DebouncedFreshness(raw, now);

            return new LiveState
            {
                Scene = _scene,
                PlayState = _playState,
                Song = _song,
                SongSource = _song is null ? SongSource.Unknown : _songSource,
                ReceivedAt = _lastAccepted,
                Freshness = reported,
                Fault = CurrentFault(reported),
                Senders = [.. _senders],
                DatagramsAccepted = _accepted,
                DatagramsRejected = _rejected,
            };
        }
    }

    private LiveFreshness RawFreshness(DateTimeOffset now)
    {
        if (_lastAccepted is not { } last)
        {
            return LiveFreshness.Dead;
        }

        var age = now - last;

        if (age < LiveThreshold)
        {
            return LiveFreshness.Live;
        }

        return age < DeadThreshold ? LiveFreshness.Stale : LiveFreshness.Dead;
    }

    private LiveFreshness DebouncedFreshness(LiveFreshness raw, DateTimeOffset now)
    {
        if (raw <= _reportedFreshness)
        {
            // Promotion, or no change: report immediately and clear any pending demotion.
            _reportedFreshness = raw;
            _candidateFreshness = raw;
            _candidateSince = null;
            return _reportedFreshness;
        }

        if (raw != _candidateFreshness)
        {
            _candidateFreshness = raw;
            _candidateSince = now;
            return _reportedFreshness;
        }

        if (_candidateSince is { } since && now - since >= DemotionDebounce)
        {
            _reportedFreshness = raw;
            _candidateSince = null;
        }

        return _reportedFreshness;
    }

    private SessionFault CurrentFault(LiveFreshness reported)
    {
        if (_portConflict)
        {
            return SessionFault.PortConflict;
        }

        if (_senders.Count > 1)
        {
            return SessionFault.MultipleSources;
        }

        if (reported == LiveFreshness.Dead)
        {
            return _lastAccepted is null ? SessionFault.NoDatagrams : SessionFault.StreamDead;
        }

        return SessionFault.None;
    }
}

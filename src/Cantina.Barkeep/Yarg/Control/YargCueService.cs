// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.YargSession;

namespace Cantina.Barkeep.Yarg.Control;

public sealed record CueRequest(string CommandId, SetlistEntry Entry, string Query);

/// <summary>Where a cue stands. The wording is the iPad's wording (docs/failure-behavior.md).</summary>
public sealed record CueStatus(
    string CommandId,
    string State,
    string Detail,
    SetlistEntry Requested,
    LatchedSong? Loaded);

/// <summary>
/// The cue pipeline: D-024's readiness gate in front of D-017's proven sequence, verified
/// by outcome and journaled so a crash mid-cue recovers as ambiguous rather than
/// re-sending keystrokes.
///
/// A cue cannot confirm synchronously. D-018 measured why: confirming a song opens
/// instrument setup, which belongs to the players (D-015), and <c>currentSong.json</c>
/// populates only when gameplay actually starts. So actuation ends in
/// <c>pending-players</c>, and <see cref="TryConfirm"/> resolves the cue when the tracker
/// observes gameplay — with the requested hash, or with somebody else's, which is
/// reported by name rather than smoothed over.
/// </summary>
public sealed class YargCueService(
    YargSessionTracker tracker,
    IYargActuator actuator,
    SetlistJournal journal,
    TimeProvider clock)
{
    private readonly object _gate = new();
    private CueStatus? _current;

    public CueStatus? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Runs the gate and, if every signal holds, the actuation sequence. Returns the
    /// resulting status; a refused cue names the failing signal and sends nothing.
    /// </summary>
    public CueStatus Cue(CueRequest request)
    {
        lock (_gate)
        {
            // One in-flight cue: a second one supersedes the first, which is resolved
            // ambiguous by name rather than silently forgotten.
            if (_current is { State: "pending-players" } previous)
            {
                journal.Resolve(previous.CommandId, SetlistOutcome.Ambiguous, clock);
                _current = null;
            }
        }

        var snapshot = tracker.Snapshot(clock.GetUtcNow());

        var refusal = Gate(snapshot);

        if (refusal is not null)
        {
            return Status(request, "refused", refusal, loaded: null);
        }

        // Journal before touching YARG (D-023): a crash after this point recovers the
        // command as ambiguous instead of re-sending keystrokes at an unknown screen.
        if (!journal.AppendPending(ToIntent(request), clock))
        {
            return Status(request, "replayed", "this command id was already journaled; not re-executed", loaded: null);
        }

        var actuation = Actuate(request);

        if (actuation is not null)
        {
            journal.Resolve(request.CommandId, SetlistOutcome.Failed, clock);
            return Status(request, "failed", actuation, loaded: null);
        }

        return Status(request, "pending-players",
            "the song list search was driven and confirmed; instrument setup belongs to the players (D-015). "
            + "The cue resolves when gameplay is observed.", loaded: null);
    }

    /// <summary>
    /// Called by the confirmation poller with each snapshot. Resolves a pending cue when
    /// gameplay is observed: the requested hash is Done; a different hash is Failed and
    /// names what actually loaded (D-015's honest failure, load-bearing since D-017
    /// proved fuzzy search can select something plausible and wrong).
    /// </summary>
    public void TryConfirm(LiveState snapshot)
    {
        lock (_gate)
        {
            if (_current is not { State: "pending-players" } pending)
            {
                return;
            }

            if (snapshot.Scene != YargScene.Gameplay || snapshot.Song is null)
            {
                return;
            }

            var matches =
                (pending.Requested.Location is { Length: > 0 } location
                    && string.Equals(location, snapshot.Song.Location, StringComparison.OrdinalIgnoreCase))
                || (pending.Requested.Hash.Length > 0 && snapshot.Song.Hash == pending.Requested.Hash);

            if (matches)
            {
                journal.Resolve(pending.CommandId, SetlistOutcome.Done, clock);
                _current = pending with
                {
                    State = "done",
                    Detail = "gameplay observed with the requested song",
                    Loaded = snapshot.Song,
                };
                return;
            }

            journal.Resolve(pending.CommandId, SetlistOutcome.Failed, clock);
            _current = pending with
            {
                State = "failed",
                Detail = $"a different song loaded: \"{snapshot.Song.Title}\" by {snapshot.Song.Artist}",
                Loaded = snapshot.Song,
            };
        }
    }

    private string? Gate(LiveState snapshot)
    {
        var processes = actuator.YargProcessCount();

        if (processes == 0)
        {
            return "YARG is not running";
        }

        if (processes > 1)
        {
            return $"{processes} YARG instances are running; the oracle is ambiguous";
        }

        // Deliverability is checked here rather than discovered afterwards, because every
        // way Windows blocks injected input is silent (D-014's UIPI case, a locked
        // workstation, a session boundary). A cue that cannot arrive must fail before it
        // is sent, not report a success nobody received.
        if (actuator.InputBlockedReason() is { } blocked)
        {
            return blocked;
        }

        if (snapshot.Fault == SessionFault.PortConflict)
        {
            return "another application is holding the YARG data port";
        }

        if (snapshot.Senders.Count > 1)
        {
            return "two YARG instances are broadcasting; refusing to guess which";
        }

        if (snapshot.Freshness != LiveFreshness.Live)
        {
            var age = snapshot.ReceivedAt is { } at
                ? $"last seen {(clock.GetUtcNow() - at).TotalSeconds:0} s ago"
                : "never seen";
            return $"YARG is not observable ({age})";
        }

        if (snapshot.PlayState == YargPlayState.Paused)
        {
            return "the game is paused; resume it on the pause menu";
        }

        if (snapshot.Scene != YargScene.Menu || snapshot.PlayState != YargPlayState.NoSong)
        {
            return $"a song is active (scene={snapshot.Scene}); a cue never interrupts it";
        }

        return null;
    }

    private string? Actuate(CueRequest request)
    {
        // Bringing YARG forward is the explicit first step of a cue, permitted by
        // docs/failure-behavior.md; it is the one place Barkeep touches focus.
        if (!actuator.TryFocusYarg())
        {
            return "could not bring YARG to the foreground";
        }

        var (isForeground, owner) = actuator.ForegroundState();

        if (!isForeground)
        {
            return $"another application has the screen: {owner}";
        }

        if (!actuator.ClickSearchBox())
        {
            return "the search-box click was refused by Windows";
        }

        if (!actuator.ClearSearch())
        {
            return "clearing the search field was refused by Windows";
        }

        // Type what the keyboard map can produce. Titles carry characters no scan code
        // makes ("(Bang Your Head) Metal Health" has two); refusing the whole cue for
        // them was the first bug the real iPad found. A lossy query is safe here because
        // the outcome is verified by reading back what actually loaded.
        var typed = actuator.TypeablePortion(request.Query);

        if (typed.Length == 0)
        {
            return $"no character of \"{request.Query}\" can be typed; nothing was sent";
        }

        if (!actuator.TypeQuery(typed))
        {
            return "typing the query was refused by Windows";
        }

        Thread.Sleep(900);   // let YARG's filter settle (D-017 measured cadence)

        if (!actuator.PressEnter())
        {
            return "the selection keypress was refused by Windows";
        }

        Thread.Sleep(300);

        if (!actuator.PressEnter())
        {
            return "the confirm keypress was refused by Windows";
        }

        return null;
    }

    private CueStatus Status(CueRequest request, string state, string detail, LatchedSong? loaded)
    {
        var status = new CueStatus(request.CommandId, state, detail, request.Entry, loaded);

        lock (_gate)
        {
            _current = status;
        }

        return status;
    }

    private static SetlistIntent ToIntent(CueRequest request) => new()
    {
        CommandId = request.CommandId,
        Kind = SetlistIntentKind.Cue,
        Entry = request.Entry,
    };
}

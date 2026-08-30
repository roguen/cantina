// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>How the advance loop paces itself. Milliseconds, all measured on the theater.</summary>
public sealed class AdvanceOptions
{
    public const string SectionName = "Advance";

    /// <summary>How long the score screen belongs to the players before Cantina acts.</summary>
    public int GraceMilliseconds { get; set; } = 6000;

    /// <summary>How long the menu must hold still after the score screen before cueing.</summary>
    public int MenuSettleMilliseconds { get; set; } = 1500;

    /// <summary>How long a pressed CONTINUE gets to change the scene before the episode
    /// is reported inconclusive.</summary>
    public int OutcomeBoundMilliseconds { get; set; } = 10000;

    /// <summary>Presses per score screen before giving up. Never a hammer.</summary>
    public int MaxAttemptsPerScore { get; set; } = 2;
}

/// <summary>Where the advance loop stands, for the iPad. The wording is the iPad's.</summary>
public sealed record AdvanceStatus(bool Enabled, string Phase, string Detail, DateTimeOffset UpdatedAt);

/// <summary>The one field the iPad sends to arm or stand down the loop.</summary>
public sealed record AdvanceArmRequest(bool Enabled);

/// <summary>
/// The score-screen advance (#39, decided 2026-08-30: Cantina presses CONTINUE as well
/// as the players). Armed explicitly from the iPad and off at startup — a show is armed
/// deliberately, not by default.
///
/// The loop: a score screen with a next setlist entry starts a grace period that belongs
/// to the players; if they do not dismiss it, Cantina presses CONTINUE once, bounded by
/// attempts, never a hammer. Either way the scene lands on a menu, and the next entry
/// goes through the same cue pipeline as a tap on the iPad — same gates, same journal,
/// same verify-by-outcome — with instrument setup still belonging to the players
/// (D-015). The cursor moves only after the cue confirms that the right song loaded.
///
/// What the wire cannot say (D-015, D-018): WHICH menu the score screen dismisses to.
/// The cue's own verification is the answer — if YARG landed somewhere the search click
/// cannot reach, no song loads and the episode fails by name instead of pretending.
/// </summary>
public sealed class ScoreAdvanceService(
    IYargActuator actuator,
    ActuationGate actuation,
    YargCueService cue,
    SetlistJournal journal,
    IOptions<AdvanceOptions> options,
    TimeProvider clock)
{
    private enum Phase
    {
        Idle,
        Grace,
        AwaitingMenu,
        Cueing,
    }

    private readonly object _gate = new();
    private bool _enabled;
    private Phase _phase = Phase.Idle;
    private DateTimeOffset _graceStartedAt;
    private DateTimeOffset _pressedAt;
    private DateTimeOffset _menuSince;
    private int _attempts;
    private int _episode;
    private string? _cueCommandId;
    private string _detail = "auto-advance is off";
    private DateTimeOffset _updatedAt = DateTimeOffset.MinValue;

    public AdvanceStatus Status
    {
        get
        {
            lock (_gate)
            {
                return new(_enabled, _phase.ToString(), _detail, _updatedAt);
            }
        }
    }

    public AdvanceStatus SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            _phase = Phase.Idle;
            _attempts = 0;
            Note(enabled
                ? "auto-advance is armed; the score screen's grace period belongs to the players"
                : "auto-advance is off");
            return new(_enabled, _phase.ToString(), _detail, _updatedAt);
        }
    }

    /// <summary>Called by the poller with each snapshot. Everything here is one tick of
    /// the state machine; nothing blocks.</summary>
    public void Observe(LiveState snapshot)
    {
        lock (_gate)
        {
            if (!_enabled)
            {
                return;
            }

            var now = clock.GetUtcNow();

            // Leaving the score screen resets the attempt budget for the next one.
            if (snapshot.Scene != YargScene.Score && _phase is Phase.Idle)
            {
                _attempts = 0;
            }

            switch (_phase)
            {
                case Phase.Idle:
                    ObserveIdle(snapshot, now);
                    break;
                case Phase.Grace:
                    ObserveGrace(snapshot, now);
                    break;
                case Phase.AwaitingMenu:
                    ObserveAwaitingMenu(snapshot, now);
                    break;
                case Phase.Cueing:
                    ObserveCueing(snapshot);
                    break;
                default:
                    break;
            }
        }
    }

    private void ObserveIdle(LiveState snapshot, DateTimeOffset now)
    {
        if (snapshot.Scene != YargScene.Score || snapshot.Freshness != LiveFreshness.Live)
        {
            return;
        }

        if (_attempts >= options.Value.MaxAttemptsPerScore)
        {
            return;
        }

        if (NextEntry() is not { } next)
        {
            Note("score screen observed, but the setlist has no next song; nothing to advance to");
            _attempts = options.Value.MaxAttemptsPerScore;
            return;
        }

        _phase = Phase.Grace;
        _graceStartedAt = now;
        Note($"score screen observed; the players have {options.Value.GraceMilliseconds / 1000} s "
            + $"before Cantina continues to \"{next.Title}\"");
    }

    private void ObserveGrace(LiveState snapshot, DateTimeOffset now)
    {
        if (snapshot.Scene == YargScene.Menu)
        {
            // The players dismissed it themselves; Cantina only cues what comes next.
            _phase = Phase.AwaitingMenu;
            _menuSince = now;
            _pressedAt = now;
            Note("the players dismissed the score screen; cueing the next song when the menu settles");
            return;
        }

        if (snapshot.Scene == YargScene.Gameplay)
        {
            // Somebody started a song without a cue. Not Cantina's episode any more.
            _phase = Phase.Idle;
            Note("gameplay began without a cue; standing down for this song");
            return;
        }

        if (now - _graceStartedAt < TimeSpan.FromMilliseconds(options.Value.GraceMilliseconds))
        {
            return;
        }

        using var held = actuation.Hold();

        if (Refusal(snapshot) is { } refused)
        {
            _phase = Phase.Idle;
            Note($"could not press CONTINUE: {refused}");
            return;
        }

        _attempts++;

        if (!actuator.PressEnter())
        {
            _phase = Phase.Idle;
            Note("could not press CONTINUE: the key was refused by Windows");
            return;
        }

        _phase = Phase.AwaitingMenu;
        _pressedAt = clock.GetUtcNow();
        _menuSince = DateTimeOffset.MinValue;
        Note("CONTINUE pressed; waiting for the menu");
    }

    private void ObserveAwaitingMenu(LiveState snapshot, DateTimeOffset now)
    {
        if (snapshot.Scene == YargScene.Menu && snapshot.PlayState == YargPlayState.NoSong)
        {
            if (_menuSince == DateTimeOffset.MinValue)
            {
                _menuSince = now;
                return;
            }

            if (now - _menuSince < TimeSpan.FromMilliseconds(options.Value.MenuSettleMilliseconds))
            {
                return;
            }

            if (NextEntry() is not { } next)
            {
                _phase = Phase.Idle;
                Note("the menu settled but the setlist no longer has a next song");
                return;
            }

            _episode++;
            _cueCommandId = $"advance-{_episode}-{clock.GetUtcNow():yyyyMMddHHmmss}";
            var status = cue.Cue(new CueRequest(_cueCommandId, next, next.Title));

            if (status.State is "pending-players" or "replayed")
            {
                _phase = Phase.Cueing;
                Note($"cueing \"{next.Title}\"; instrument setup belongs to the players");
            }
            else
            {
                _phase = Phase.Idle;
                Note($"the advance cue was {status.State}: {status.Detail}");
            }

            return;
        }

        if (snapshot.Scene == YargScene.Gameplay)
        {
            _phase = Phase.Idle;
            Note("gameplay began before Cantina cued anything; standing down for this song");
            return;
        }

        if (snapshot.Scene == YargScene.Score
            && now - _pressedAt > TimeSpan.FromMilliseconds(options.Value.OutcomeBoundMilliseconds))
        {
            _phase = Phase.Idle;
            Note($"the score screen did not react within {options.Value.OutcomeBoundMilliseconds / 1000} s; "
                + $"attempt {_attempts} of {options.Value.MaxAttemptsPerScore}");
        }
    }

    private void ObserveCueing(LiveState snapshot)
    {
        var current = cue.Current;

        if (current is null || current.CommandId != _cueCommandId)
        {
            _phase = Phase.Idle;
            Note("another cue superseded the advance; standing down");
            return;
        }

        switch (current.State)
        {
            case "done":
                var moved = journal.State.Cursor + 1;
                journal.Append(new SetlistIntent
                {
                    CommandId = $"advance-cursor-{_episode}",
                    Kind = SetlistIntentKind.MoveCursor,
                    Cursor = moved,
                }, clock, out _);
                _phase = Phase.Idle;
                Note($"advanced: \"{current.Requested.Title}\" is playing and the setlist cursor moved");
                break;

            case "failed":
                _phase = Phase.Idle;
                Note($"the advance cue failed: {current.Detail}");
                break;

            default:
                break;
        }
    }

    private SetlistEntry? NextEntry()
    {
        var state = journal.State;
        return state.Cursor + 1 < state.Entries.Count ? state.Entries[state.Cursor + 1] : null;
    }

    private string? Refusal(LiveState snapshot)
    {
        if (actuator.YargProcessCount() != 1)
        {
            return "exactly one YARG instance is required";
        }

        if (actuator.InputBlockedReason() is { } blocked)
        {
            return blocked;
        }

        if (snapshot.Freshness != LiveFreshness.Live)
        {
            return "YARG is not observable";
        }

        if (!actuator.TryFocusYarg())
        {
            return "could not bring YARG to the foreground";
        }

        var (isForeground, owner) = actuator.ForegroundState();

        return isForeground ? null : $"another application has the screen: {owner}";
    }

    private void Note(string detail)
    {
        _detail = detail;
        _updatedAt = clock.GetUtcNow();
    }
}

/// <summary>Feeds tracker snapshots to the advance loop, like the cue's own poller.</summary>
internal sealed class ScoreAdvancePoller(
    ScoreAdvanceService advance,
    YargSessionTracker tracker,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            advance.Observe(tracker.Snapshot(clock.GetUtcNow()));

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

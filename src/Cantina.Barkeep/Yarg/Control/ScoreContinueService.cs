// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.YargSession;

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// The score screen's one key, pressed from the iPad. The operator asked to run the
/// whole theater without instruments — everything except the gameplay itself — and the
/// score screen was the last place that required walking to the PC: quickplay's score
/// screen waits forever, and the cue gate rightly refuses while it is up (measured
/// 2026-08-30). The scene gate makes this safe where blind menu Enters are not: Score
/// is one of the three scenes the wire actually distinguishes, so this presses only
/// when the score screen is provably what is on screen.
/// </summary>
public sealed class ScoreContinueService(
    YargSessionTracker tracker,
    IYargActuator actuator,
    ActuationGate actuation,
    TimeProvider clock)
{
    public StandInStatus Continue()
    {
        if (actuator.YargProcessCount() != 1)
        {
            return new("refused", "exactly one YARG instance is required");
        }

        if (actuator.InputBlockedReason() is { } blocked)
        {
            return new("refused", blocked);
        }

        var snapshot = tracker.Snapshot(clock.GetUtcNow());

        if (snapshot.Freshness != LiveFreshness.Live)
        {
            return new("refused", "YARG is not observable");
        }

        if (snapshot.Scene != YargScene.Score)
        {
            return new("refused", $"the score screen is not up (scene={snapshot.Scene}); this button presses nothing blind");
        }

        using var held = actuation.Hold();

        if (!actuator.TryFocusYarg())
        {
            return new("refused", "could not bring YARG to the foreground");
        }

        var (isForeground, owner) = actuator.ForegroundState();

        if (!isForeground)
        {
            return new("refused", $"another application has the screen: {owner}");
        }

        if (!actuator.PressEnter())
        {
            return new("failed", "CONTINUE was refused by Windows");
        }

        return new("sent", "CONTINUE pressed; the stage decides what comes next");
    }
}

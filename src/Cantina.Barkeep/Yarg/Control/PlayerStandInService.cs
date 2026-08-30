// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>What became of a stand-in request. The wording is the iPad's wording.</summary>
public sealed record StandInStatus(string State, string Detail);

/// <summary>
/// Stands in for the players at instrument setup: the ready confirms that D-015 reserves
/// for humans, sent synthetically so a cued song can be kicked off from the iPad during
/// bench testing. This is the debug surface's whole reason to exist, and it is gated the
/// same way a cue is — it refuses by name unless a cue is actually pending the players,
/// the wire is live, and the input can arrive. The confirms themselves prove nothing
/// (a sent keystroke is never evidence); the cue resolves when the confirmation poller
/// observes gameplay, exactly as it does for real players.
/// </summary>
public sealed class PlayerStandInService(
    YargSessionTracker tracker,
    IYargActuator actuator,
    YargCueService cue,
    TimeProvider clock,
    IOptions<DebugOptions> options)
{
    public StandInStatus Confirm()
    {
        if (cue.Current is not { State: "pending-players" })
        {
            var state = cue.Current?.State ?? "none";
            return new("refused", $"no cue is waiting on the players (current cue: {state}); cue a song first");
        }

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
            return new("refused", "YARG is not observable; the confirms would land at an unknown screen");
        }

        // Instrument setup broadcasts Menu/NoSong — the wire cannot distinguish it from
        // any other menu screen. The pending-players cue above is what says this is the
        // right menu; a scene that already left Menu says it is not.
        if (snapshot.Scene != YargScene.Menu)
        {
            return new("refused", $"the scene is {snapshot.Scene}, not a menu; instrument setup is no longer on screen");
        }

        if (!actuator.TryFocusYarg())
        {
            return new("refused", "could not bring YARG to the foreground");
        }

        var (isForeground, owner) = actuator.ForegroundState();

        if (!isForeground)
        {
            return new("refused", $"another application has the screen: {owner}");
        }

        var pacing = options.Value;
        Thread.Sleep(pacing.ConfirmationLeadMilliseconds);

        for (var sent = 0; sent < pacing.PlayerConfirmations; sent++)
        {
            if (!actuator.PressEnter())
            {
                return new("failed", $"ready confirm {sent + 1} of {pacing.PlayerConfirmations} was refused by Windows");
            }

            Thread.Sleep(pacing.ConfirmationSettleMilliseconds);
        }

        return new("sent",
            $"{pacing.PlayerConfirmations} ready confirms sent standing in for the players; "
            + "the cue resolves when gameplay is observed");
    }
}

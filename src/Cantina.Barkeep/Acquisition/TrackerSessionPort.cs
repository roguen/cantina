// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Library;
using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Acquisition;

/// <summary>
/// The YARG half of the import pipeline, built from the same observed-not-assumed parts
/// as the cue pipeline.
///
/// The refresh drives the menu sequence measured on 2026-08-29: MORE OPTIONS opens a
/// popup whose third entry is SCAN SONGS, both reachable by pointer click, and a
/// completed scan moved the library count 652 → 448 — which also resolved D-025's open
/// 652-versus-447 discrepancy as a stale song cache. The sequence is open-loop, exactly
/// like the search-box click (D-017): no wire signal reports which menu screen is
/// showing or when a scan finishes, so the clicks are bounded by time and the *cue* is
/// what verifies the song actually became visible, by reading back what loaded.
/// <see cref="WaitForSongVisibleAsync"/> is therefore honest about being a settle, not
/// an observation.
/// </summary>
public sealed class TrackerSessionPort(
    YargSessionTracker tracker,
    IYargActuator actuator,
    YargCueService cue,
    SongIndex index,
    IOptions<AcquisitionOptions> options,
    TimeProvider clock) : IYargSessionPort
{
    public ValueTask<YargSessionSnapshot> ObserveAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var snapshot = tracker.Snapshot(now);

        // Idle means what the cue gate means by it: a live wire, the menu scene, no song.
        // Anything less is Active or Unknown, and the coordinator treats both as "do not
        // touch the game".
        var live = snapshot.Freshness == LiveFreshness.Live && snapshot.Fault == SessionFault.None;
        var idle = live &&
            snapshot.Scene == YargScene.Menu &&
            snapshot.PlayState == YargPlayState.NoSong &&
            snapshot.Senders.Count <= 1;

        var activity = !live ? YargActivity.Unknown : idle ? YargActivity.Idle : YargActivity.Active;

        return ValueTask.FromResult(new YargSessionSnapshot(
            activity,
            snapshot.ReceivedAt ?? now,
            CanRefreshLibrary: idle && actuator.YargProcessCount() == 1 && actuator.InputBlockedReason() is null));
    }

    public async ValueTask<ExternalCommandOutcome> RequestLibraryRefreshAsync(
        SongIdentity song,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // Focus first, verified by observation rather than by the request (D-014). The
        // gate already proved the scene is Menu, so focus cannot pause gameplay.
        if (!actuator.TryFocusYarg())
        {
            return ExternalCommandOutcome.Failed;
        }

        // Open-loop from here: two clicks whose targets cannot be confirmed before use.
        // A miss is contained rather than detected — the worst first click selects a song
        // row, the worst second click leaves the popup open — and the failure surfaces at
        // the cue read-back, the same honesty budget D-017 accepted for selection.
        if (!actuator.ClickAt(settings.MoreOptionsX, settings.MoreOptionsY))
        {
            return ExternalCommandOutcome.Failed;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(800), cancellationToken);

        if (!actuator.ClickAt(settings.ScanSongsX, settings.ScanSongsY))
        {
            // The popup may or may not be open; nothing can say. Ambiguous, by name.
            return ExternalCommandOutcome.Ambiguous;
        }

        // The scan has no completion signal anywhere Barkeep can see, so time is the only
        // bound. The wire staying Menu/NoSong throughout is the one cheap sanity check
        // available: a scene change here means something else happened.
        var deadline = clock.GetUtcNow().AddSeconds(settings.ScanSettleSeconds);

        while (clock.GetUtcNow() < deadline)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            var snapshot = tracker.Snapshot(clock.GetUtcNow());

            if (snapshot.Scene != YargScene.Menu)
            {
                return ExternalCommandOutcome.Ambiguous;
            }
        }

        return ExternalCommandOutcome.Succeeded;
    }

    public ValueTask<bool> WaitForSongVisibleAsync(SongIdentity song, CancellationToken cancellationToken)
    {
        // Deliberately a no-op, and named as such. YARG's library is not observable from
        // outside — the wire says nothing about it, the count is pixels on a screen, and
        // songcache.bin is a private surface Cantina stays off (rule 6). Visibility is
        // proven where it can be: the cue reads back currentSong.json and matches the
        // path, so a song that never became visible fails *there*, by name, instead of
        // being falsely confirmed here.
        return ValueTask.FromResult(true);
    }

    public ValueTask<ExternalCommandOutcome> CueAsync(SongIdentity song, CancellationToken cancellationToken)
    {
        // The typed query is the indexed title, not the filename: YARG's search is fuzzy
        // (D-017) and "Everlong" reaches the song where "Foo Fighters - Everlong (Hoph2o)"
        // might widen into anything. The verify-by-outcome match is on the location either
        // way, so a wrong hit fails by name rather than playing the wrong chart.
        var indexed = index.FindByLocation(song.Value);
        var title = indexed?.Title ?? Path.GetFileNameWithoutExtension(song.Value);

        var status = cue.Cue(new CueRequest(
            $"acquisition-{Guid.NewGuid():N}",
            new SetlistEntry(
                indexed?.LearnedHash ?? string.Empty,
                title,
                indexed?.Artist ?? string.Empty,
                song.Value),
            title));

        return ValueTask.FromResult(status.State switch
        {
            "refused" or "failed" => ExternalCommandOutcome.Failed,
            "pending-players" or "done" or "replayed" => ExternalCommandOutcome.Succeeded,
            _ => ExternalCommandOutcome.Ambiguous,
        });
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.YargSession;

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// Feeds tracker snapshots to the cue service so a pending cue resolves when gameplay is
/// observed. Polling at 250 ms is comfortably inside the latch's lifetime and adds no
/// I/O of its own — the tracker already holds the state.
/// </summary>
internal sealed class CueConfirmationPoller(
    YargCueService cueService,
    YargSessionTracker tracker,
    TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            cueService.TryConfirm(tracker.Snapshot(clock.GetUtcNow()));

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

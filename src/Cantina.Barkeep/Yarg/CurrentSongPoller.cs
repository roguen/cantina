// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg;

/// <summary>
/// Polls <c>currentSong.json</c> and feeds its content to the tracker, which owns the
/// latching. Polling, not file watching: the file clears ~86 ms after a scene change
/// (D-010), YARG rewrites it in place, and FileSystemWatcher coalesces and drops events
/// under exactly that kind of churn. A 25 ms poll is the cadence the spikes proved.
///
/// Read-only, and tolerant of the rewrite race: an <see cref="IOException"/> mid-rewrite
/// is expected and the next poll resolves it.
/// </summary>
internal sealed class CurrentSongPoller(
    YargSessionTracker tracker,
    IOptions<YargSessionOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            return;
        }

        var path = Path.Combine(settings.ResolveYargDirectory(), "currentSong.json");
        var interval = TimeSpan.FromMilliseconds(settings.CurrentSongPollMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (File.Exists(path))
                {
                    tracker.OnCurrentSong(await File.ReadAllTextAsync(path, stoppingToken).ConfigureAwait(false));
                }
            }
            catch (IOException)
            {
                // YARG is rewriting the file; the next poll reads the settled content.
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}

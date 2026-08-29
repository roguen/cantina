// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Yarg;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Library;

/// <summary>
/// Scans the index at startup and keeps learning: whenever the tracker holds a latched
/// song, its YARG hash is joined to the indexed folder by location (D-025). Observation
/// is the only place hashes come from, so this loop is the join.
/// </summary>
internal sealed partial class LibraryService(
    SongIndex index,
    YargSessionTracker tracker,
    IOptions<LibraryOptions> libraryOptions,
    IOptions<YargSessionOptions> yargOptions,
    TimeProvider clock,
    ILogger<LibraryService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directories = libraryOptions.Value.ResolveDirectories(yargOptions.Value.ResolveYargDirectory());
        var report = index.Scan(directories, clock);
        LogScan(logger, report.Indexed, report.Skipped.Count, report.DurationMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var song = tracker.Snapshot(clock.GetUtcNow()).Song;

            if (song is { Location.Length: > 0, Hash.Length: > 0 })
            {
                index.LearnHash(song.Location, song.Hash);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Library scan: {Indexed} songs indexed, {Skipped} skipped, in {DurationMs:0} ms.")]
    private static partial void LogScan(ILogger logger, int indexed, int skipped, double durationMs);
}

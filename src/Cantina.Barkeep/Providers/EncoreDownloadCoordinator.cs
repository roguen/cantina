// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Concurrent;
using Cantina.Barkeep.Acquisition;
using Cantina.Barkeep.Library;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Providers;

/// <summary>Where one requested download stands. The wording is the iPad's wording.</summary>
public sealed record ProviderDownload(
    string Md5,
    string Title,
    string Artist,
    string State,
    string Detail,
    DateTimeOffset StartedAt);

/// <summary>
/// Turns an approved download into an acquisition-watch-directory arrival, which is the
/// entire trick: everything after the file lands — settling, validation, indexing, YARG's
/// rescan, play-next — is the already-proven D-030 pipeline, and this class adds no
/// second import path. It stages outside the watch directory, validates the header
/// before publishing (a byte stream from the network proves nothing), and moves the file
/// in under its final name only when it is whole.
///
/// Downloads are one at a time and counted per rolling hour: the provider is a
/// donation-funded service whose stated top cost is bandwidth (D-032).
/// </summary>
public sealed class EncoreDownloadCoordinator(
    EncoreClient client,
    IOptions<EncoreOptions> options,
    IOptions<AcquisitionOptions> acquisition,
    TimeProvider clock,
    ILogger<EncoreDownloadCoordinator> log) : IDisposable
{
    public void Dispose() => _oneAtATime.Dispose();

    private readonly ConcurrentDictionary<string, ProviderDownload> _downloads = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<DateTimeOffset> _recentStarts = new();
    private readonly SemaphoreSlim _oneAtATime = new(1, 1);

    /// <summary>The recent picture, newest first, for the iPad's provider section.</summary>
    public IReadOnlyList<ProviderDownload> Recent =>
        [.. _downloads.Values.OrderByDescending(download => download.StartedAt).Take(10)];

    public ProviderDownload Request(EncoreChart chart)
    {
        var watchDirectory = acquisition.Value.WatchDirectory;

        if (watchDirectory.Length == 0)
        {
            return Track(chart, "refused",
                "no acquisition watch directory is configured; there is nowhere to deliver the chart");
        }

        var fileName = FileNameFor(chart);

        if (File.Exists(Path.Combine(watchDirectory, fileName)))
        {
            return Track(chart, "refused", $"\"{fileName}\" is already in the watch directory");
        }

        if (_downloads.TryGetValue(chart.Md5, out var existing) && existing.State is "downloading" or "delivered")
        {
            return existing;
        }

        var now = clock.GetUtcNow();

        while (_recentStarts.TryPeek(out var oldest) && now - oldest > TimeSpan.FromHours(1))
        {
            _recentStarts.TryDequeue(out _);
        }

        if (_recentStarts.Count >= options.Value.DownloadsPerHour)
        {
            return Track(chart, "refused",
                $"the polite ceiling of {options.Value.DownloadsPerHour} downloads per hour is reached; try again later");
        }

        _recentStarts.Enqueue(now);
        var started = Track(chart, "downloading", "downloading from Chorus Encore");

        // Fire-and-track: the request returns immediately and the iPad follows the state.
        _ = RunAsync(chart, fileName);

        return started;
    }

    private async Task RunAsync(EncoreChart chart, string fileName)
    {
        await _oneAtATime.WaitAsync().ConfigureAwait(false);

        var staging = options.Value.ResolveStagingDirectory();
        var stagedPath = Path.Combine(staging, chart.Md5 + ".sng.partial");

        try
        {
            Directory.CreateDirectory(staging);

            await client.DownloadAsync(chart.Md5, stagedPath, CancellationToken.None).ConfigureAwait(false);

            // A downloaded byte stream proves nothing. The same validation the arrival
            // pipeline trusts decides whether this is a chart before it is published.
            await using (var stream = File.OpenRead(stagedPath))
            {
                if (!SngDocument.TryRead(stream, out _, out var reason))
                {
                    Track(chart, "failed", $"the downloaded file is not a valid chart ({reason})");
                    return;
                }
            }

            // The move publishes the file to the watcher under its final name, whole.
            // From here the D-030 pipeline owns it: import, rescan, play-next, and the
            // arrivals feed the iPad already shows.
            File.Move(stagedPath, Path.Combine(acquisition.Value.WatchDirectory, fileName), overwrite: false);

            Track(chart, "delivered",
                "delivered to the acquisition pipeline; the arrivals feed reports the import");
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            ProviderLog.DownloadFailed(log, chart.Md5, error);
            Track(chart, "failed", $"the download failed: {error.Message}");
        }
        finally
        {
            try
            {
                if (File.Exists(stagedPath))
                {
                    File.Delete(stagedPath);
                }
            }
            catch (IOException)
            {
                // A stranded partial is re-stageable; the next attempt overwrites it.
            }

            _oneAtATime.Release();
        }
    }

    private ProviderDownload Track(EncoreChart chart, string state, string detail)
    {
        var record = new ProviderDownload(
            chart.Md5, chart.Name, chart.Artist, state, detail, clock.GetUtcNow());
        _downloads[chart.Md5] = record;
        return record;
    }

    /// <summary>
    /// "{Artist} - {Name} ({Charter}).sng", the same convention Geomitron Bridge uses, so
    /// downloads from either tool sit side by side. Characters the filesystem refuses are
    /// dropped rather than escaped — the name is a label; the identity is the md5.
    /// </summary>
    public static string FileNameFor(EncoreChart chart)
    {
        var stem = $"{chart.Artist} - {chart.Name}" + (chart.Charter is { Length: > 0 } ? $" ({chart.Charter})" : "");
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string([.. stem.Where(character => !invalid.Contains(character))]).Trim();

        if (cleaned.Length == 0)
        {
            cleaned = chart.Md5;
        }

        return cleaned + ".sng";
    }
}

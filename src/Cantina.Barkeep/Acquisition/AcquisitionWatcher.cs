// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Acquisition;

/// <summary>One import the watcher ran, for the honest-progress surface the iPad renders.</summary>
public sealed record AcquisitionRecord(
    string FileName,
    string IdempotencyKey,
    ImportPlayNextOutcome Outcome,
    string? FailureCode,
    IReadOnlyList<ImportPlayNextEvent> Events,
    DateTimeOffset FinishedAt);

/// <summary>
/// Watches the Geomitron Bridge handoff directory and runs the import pipeline for each
/// stable <c>.sng</c> arrival.
///
/// Watcher events are hints, never triggers (docs/geomitron-bridge-integration.md): every
/// hint funnels into one queue, a reconciliation sweep at startup and on a timer enqueues
/// whatever events were missed, and the coordinator's journal makes double-enqueueing
/// free. The identity of an arrival is its name, length, and write time — so a
/// re-download of the same file is a new import, and a re-notification of the same bytes
/// is a replay.
/// </summary>
public sealed partial class AcquisitionWatcher(
    ImportPlayNextCoordinator coordinator,
    AcquisitionJournal journal,
    IOptions<AcquisitionOptions> options,
    TimeProvider clock,
    ILogger<AcquisitionWatcher> log) : BackgroundService
{
    private readonly Channel<(string FileName, bool FromSweep)> _hints =
        Channel.CreateUnbounded<(string, bool)>();
    private readonly object _recentGate = new();
    private readonly List<AcquisitionRecord> _recent = [];

    public IReadOnlyList<AcquisitionRecord> Recent
    {
        get
        {
            lock (_recentGate)
            {
                return [.. _recent];
            }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var directory = options.Value.WatchDirectory;

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        // Configured but absent is loud AND recoverable: the operator named a directory,
        // so keep waiting for it - a NAS path mounting after boot, or Bridge creating its
        // library folder on first run, must not cost a whole session of acquisition.
        while (!Directory.Exists(directory))
        {
            AcquisitionLog.WatchDirectoryMissing(log, directory);

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        AcquisitionLog.Watching(log, directory);

        using var watcher = new FileSystemWatcher(directory, "*.sng")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        void Hint(string path) => _hints.Writer.TryWrite((Path.GetFileName(path), false));

        watcher.Created += (_, e) => Hint(e.FullPath);
        watcher.Changed += (_, e) => Hint(e.FullPath);
        watcher.Renamed += (_, e) => Hint(e.FullPath);

        // Startup reconciliation covers everything that arrived while Barkeep was down,
        // and the periodic sweep covers watcher events Windows dropped. The task is
        // observed: a sweep that dies takes the design's whole safety net with it, so its
        // death must be named, not discarded.
        var sweepInterval = TimeSpan.FromMinutes(Math.Max(1, options.Value.ReconcileMinutes));
        var sweep = SweepLoopAsync(directory, sweepInterval, stoppingToken);
        _ = sweep.ContinueWith(
            t => AcquisitionLog.SweepDied(log, t.Exception?.GetBaseException().Message ?? "unknown"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);

        await foreach (var (fileName, fromSweep) in _hints.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ImportAsync(directory, fileName, fromSweep, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // Broad on purpose: an unnamed escape here kills the consumer loop and
                // acquisition dies silently, which is the one outcome worse than any
                // single import failing.
                AcquisitionLog.ImportFaulted(log, fileName, error.Message);
            }
        }
    }

    private async Task SweepLoopAsync(string directory, TimeSpan interval, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(directory, "*.sng", SearchOption.TopDirectoryOnly))
                {
                    _hints.Writer.TryWrite((Path.GetFileName(path), true));
                }
            }
            catch (Exception error) when (
                error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                // Briefly unreadable - an ACL flap, antivirus holding the directory - is a
                // sweep delayed, not a sweep dead. UnauthorizedAccessException is NOT an
                // IOException, which is exactly how the first version would have died.
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ImportAsync(string directory, string fileName, bool fromSweep, CancellationToken stoppingToken)
    {
        var full = Path.Combine(directory, fileName);

        // The identity must come from the SETTLED file, not from whenever the hint was
        // dequeued. The first hint for a cross-volume Bridge handoff is the Created event
        // at the start of the copy; fingerprinting there mints a key over a partial
        // length and an in-flight mtime, the coordinator's own probe loop then patiently
        // waits out the real copy, and the whole import completes under the stale key -
        // after which the sweep computes the settled fingerprint, finds no receipt, and
        // runs the entire pipeline a second time: duplicate setlist entry, duplicate cue.
        // Found by adversarial review before it shipped, confirmed by three independent
        // lenses. So the watcher settles first, with the same two signals the arrival
        // probe uses, and the pipeline's probe becomes the safety net rather than the
        // thing that invalidates the key.
        var settled = await SettleIdentityAsync(full, stoppingToken);

        if (settled is not { } identity)
        {
            return;
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{fileName}|{identity.Length}|{identity.WriteTicks}")));
        var key = $"sng:{fingerprint}";

        // A failed import of the same unchanged file gets one fresh chance per SWEEP -
        // not per hint, or the burst of Changed events one copy raises would retry
        // back-to-back, re-driving the Scan Songs clicks each time. "Failed" usually
        // means the world was wrong (YARG mid-song, a lock held), not the file.
        // Completed and ambiguous receipts are never forgotten.
        if (fromSweep)
        {
            journal.ForgetFailure(key);
        }

        var result = await coordinator.RunAsync(
            new ImportPlayNextRequest(key, new SongArrivalCandidate("geomitron-bridge", fileName, fingerprint)),
            stoppingToken);

        // If the file moved on while the pipeline ran, this import spoke for bytes that
        // no longer exist; the settled file re-enters through the queue under its own key.
        var after = new FileInfo(full);

        if (after.Exists &&
            (after.Length != identity.Length || after.LastWriteTimeUtc.Ticks != identity.WriteTicks))
        {
            _hints.Writer.TryWrite((fileName, false));
        }

        if (result.Outcome == ImportPlayNextOutcome.InProgress)
        {
            return;
        }

        if (!result.IsReplay)
        {
            var outcomeName = result.Outcome.ToString();
            AcquisitionLog.ImportFinished(log, fileName, outcomeName, result.FailureCode);
        }

        lock (_recentGate)
        {
            // Replays of already-terminal imports are not re-recorded: the surface shows
            // what happened, not how often the sweep re-confirmed it.
            if (!result.IsReplay)
            {
                _recent.Insert(0, new AcquisitionRecord(
                    fileName,
                    key,
                    result.Outcome,
                    result.FailureCode,
                    result.Events,
                    clock.GetUtcNow()));

                if (_recent.Count > 50)
                {
                    _recent.RemoveAt(_recent.Count - 1);
                }
            }
        }
    }
}

public sealed partial class AcquisitionWatcher
{
    /// <summary>
    /// Waits until the file's length and write time hold still across one probe interval,
    /// and no writer holds it. Bounded by the same budget as the pipeline's own probe.
    /// Null when the file vanished or never settled.
    /// </summary>
    private async Task<(long Length, long WriteTicks)?> SettleIdentityAsync(
        string full,
        CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(50, options.Value.StabilityProbeMilliseconds));

        for (var attempt = 0; attempt < 40; attempt++)
        {
            var before = new FileInfo(full);

            if (!before.Exists)
            {
                return null;
            }

            var snapshot = (before.Length, before.LastWriteTimeUtc.Ticks);
            await Task.Delay(interval, stoppingToken);

            var after = new FileInfo(full);

            if (!after.Exists)
            {
                return null;
            }

            if ((after.Length, after.LastWriteTimeUtc.Ticks) != snapshot)
            {
                continue;
            }

            try
            {
                using var probe = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (IOException)
            {
                continue;
            }

            return snapshot;
        }

        return null;
    }
}

public static partial class AcquisitionLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Watching {Directory} for Geomitron Bridge arrivals.")]
    public static partial void Watching(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Acquisition:WatchDirectory names {Directory}, which does not exist. Acquisition is off until it does.")]
    public static partial void WatchDirectoryMissing(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Import of {FileName} finished: {Outcome} {FailureCode}.")]
    public static partial void ImportFinished(ILogger logger, string fileName, string outcome, string? failureCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Import of {FileName} faulted: {Reason}. The next sweep retries.")]
    public static partial void ImportFaulted(ILogger logger, string fileName, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "The reconciliation sweep died: {Reason}. Dropped watcher events are no longer recovered until restart.")]
    public static partial void SweepDied(ILogger logger, string reason);
}

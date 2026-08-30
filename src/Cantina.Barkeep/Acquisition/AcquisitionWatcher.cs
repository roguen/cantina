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
public sealed class AcquisitionWatcher(
    ImportPlayNextCoordinator coordinator,
    AcquisitionJournal journal,
    IOptions<AcquisitionOptions> options,
    TimeProvider clock,
    ILogger<AcquisitionWatcher> log) : BackgroundService
{
    private readonly Channel<string> _hints = Channel.CreateUnbounded<string>();
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

        if (!Directory.Exists(directory))
        {
            // Configured but absent is a loud condition, not a silent idle: the operator
            // named a directory and it is not there.
            AcquisitionLog.WatchDirectoryMissing(log, directory);
            return;
        }

        AcquisitionLog.Watching(log, directory);

        using var watcher = new FileSystemWatcher(directory, "*.sng")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        void Hint(string path) => _hints.Writer.TryWrite(Path.GetFileName(path));

        watcher.Created += (_, e) => Hint(e.FullPath);
        watcher.Changed += (_, e) => Hint(e.FullPath);
        watcher.Renamed += (_, e) => Hint(e.FullPath);

        // Startup reconciliation covers everything that arrived while Barkeep was down,
        // and the periodic sweep covers watcher events Windows dropped.
        var sweepInterval = TimeSpan.FromMinutes(Math.Max(1, options.Value.ReconcileMinutes));
        _ = SweepLoopAsync(directory, sweepInterval, stoppingToken);

        await foreach (var fileName in _hints.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ImportAsync(directory, fileName, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
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
                    _hints.Writer.TryWrite(Path.GetFileName(path));
                }
            }
            catch (IOException)
            {
                // The directory being briefly unreadable is a hint delayed, not a fault.
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

    private async Task ImportAsync(string directory, string fileName, CancellationToken stoppingToken)
    {
        var full = Path.Combine(directory, fileName);
        var info = new FileInfo(full);

        if (!info.Exists)
        {
            return;
        }

        // Name + length + write time: a re-download of the same title is a new import, a
        // duplicate notification of the same bytes replays from the journal.
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{fileName}|{info.Length}|{info.LastWriteTimeUtc.Ticks}")));
        var key = $"sng:{fingerprint}";

        // A failed import of the same unchanged file gets one fresh chance per sweep:
        // "failed" here usually means the world was wrong (YARG not observable, a lock
        // held), not the file. Completed and ambiguous receipts are never forgotten.
        journal.ForgetFailure(key);

        var result = await coordinator.RunAsync(
            new ImportPlayNextRequest(key, new SongArrivalCandidate("geomitron-bridge", fileName, fingerprint)),
            stoppingToken);

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
}

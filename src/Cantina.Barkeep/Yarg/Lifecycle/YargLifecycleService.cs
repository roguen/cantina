// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg.Lifecycle;

/// <summary>
/// YARG's process state, reported separately from the data stream (#23). "not-running" and a
/// stale datagram are different facts: the first means launch it, the second means look at
/// the game.
/// </summary>
public sealed record YargProcessStatus(
    string State,
    string Detail,
    bool LaunchConfigured,
    DateTimeOffset UpdatedAt);

/// <summary>The one field a restart request carries: the operator confirmed ending a song.</summary>
public sealed record YargRestartRequest(bool Confirm);

/// <summary>
/// Launches and restarts YARG from the iPad (#23, the owner's call on 2026-09-13; D-038).
///
/// A launch is not done when the process exists. It is done when the data stream is live,
/// the library has finished loading, and, for a fresh process, the Music Library is open.
/// Each of those is observed, not assumed. The launched window does not take the screen by
/// itself (measured), so the service focuses it through the same actuator a cue uses,
/// under the same actuation gate.
///
/// A restart never ends a song silently: during gameplay it refuses unless the request
/// carries the operator's confirmation. A polite close gets a bounded wait, then YARG is
/// terminated, and a YARG that survives even that is reported by name. The setlist and its
/// cursor belong to Barkeep and survive untouched; a cue still waiting on the players is
/// failed by name, because the screen it was waiting on no longer exists.
/// </summary>
public sealed class YargLifecycleService(
    IYargProcessHost host,
    IYargActuator actuator,
    ActuationGate actuation,
    YargSessionTracker tracker,
    YargCueService cue,
    IOptions<YargProcessOptions> options,
    TimeProvider clock)
{
    private readonly object _gate = new();
    private string? _operation;
    private string _detail = "";
    private DateTimeOffset _updatedAt;
    private bool _outcomeHadProcess;
    private Task _work = Task.CompletedTask;

    /// <summary>The background launch or restart, for tests and orderly shutdown.</summary>
    public Task Work
    {
        get
        {
            lock (_gate)
            {
                return _work;
            }
        }
    }

    public YargProcessStatus Status
    {
        get
        {
            lock (_gate)
            {
                return Describe();
            }
        }
    }

    public YargProcessStatus Launch()
    {
        lock (_gate)
        {
            if (_operation is not null)
            {
                return Describe();
            }

            if (Refusal() is { } refused)
            {
                return Refused(refused);
            }

            var running = host.Count();

            if (running > 0)
            {
                return running == 1
                    ? new("running", "YARG is already running", true, clock.GetUtcNow())
                    : Refused($"{running} YARG instances are running; refusing to add another");
            }

            Begin("starting", "launching YARG");
            _work = Task.Run(RunLaunchAsync);
            return Describe();
        }
    }

    public YargProcessStatus Restart(bool confirm)
    {
        lock (_gate)
        {
            if (_operation is not null)
            {
                return Refused($"YARG is already {_operation}; wait for it to finish");
            }

            if (Refusal() is { } refused)
            {
                return Refused(refused);
            }

            var running = host.Count();

            if (running == 0)
            {
                Begin("starting", "YARG was not running; launching it");
                _work = Task.Run(RunLaunchAsync);
                return Describe();
            }

            if (running > 1)
            {
                return Refused($"{running} YARG instances are running; refusing to guess which to restart");
            }

            var snapshot = tracker.Snapshot(clock.GetUtcNow());
            var songActive = snapshot.Scene is YargScene.Gameplay or YargScene.Practice
                || snapshot.PlayState != YargPlayState.NoSong;

            if (songActive && !confirm)
            {
                return Refused("a song is active; restarting ends it, so confirm to restart anyway");
            }

            Begin("stopping", "asking YARG to close");
            _work = Task.Run(RunRestartAsync);
            return Describe();
        }
    }

    private async Task RunRestartAsync()
    {
        cue.Abandon("YARG was restarted before the players started the song");

        var polite = TimeSpan.FromMilliseconds(options.Value.CloseTimeoutMilliseconds);

        if (!host.RequestClose(polite))
        {
            Note("stopping", "YARG did not close politely; terminating it");

            if (!host.Kill(polite))
            {
                Finish("YARG did not exit even when terminated; it may need attention at the PC");
                return;
            }
        }

        lock (_gate)
        {
            Begin("starting", "YARG closed; launching it again");
        }

        await RunLaunchAsync().ConfigureAwait(false);
    }

    private async Task RunLaunchAsync()
    {
        var settings = options.Value;
        var launchedAt = clock.GetUtcNow();

        try
        {
            host.Start(settings.ExecutablePath);
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Finish($"YARG could not be started: {error.Message}");
            return;
        }

        var deadline = clock.GetUtcNow() + TimeSpan.FromMilliseconds(settings.ReadyTimeoutMilliseconds);
        var poll = TimeSpan.FromMilliseconds(settings.PollMilliseconds);

        // Observable first: the stream goes live seconds before the library finishes loading.
        while (true)
        {
            var snapshot = tracker.Snapshot(clock.GetUtcNow());

            // Only datagrams that arrived after the launch speak for the new process. Right
            // after a restart the tracker still holds the old process's last Menu reading,
            // and trusting it would press Enter at a screen that no longer exists.
            var fromThisLaunch = snapshot.Freshness == LiveFreshness.Live
                && snapshot.ReceivedAt is { } received && received >= launchedAt;

            if (fromThisLaunch && snapshot.Scene != YargScene.Unknown)
            {
                break;
            }

            if (fromThisLaunch)
            {
                Note("loading", "YARG is loading its library");
            }

            if (host.Count() == 0)
            {
                Finish("YARG exited while it was starting");
                return;
            }

            if (clock.GetUtcNow() >= deadline)
            {
                Finish(fromThisLaunch
                    ? $"YARG is running but did not finish loading within {settings.ReadyTimeoutMilliseconds / 1000} s"
                    : $"YARG is running but its data stream never appeared within {settings.ReadyTimeoutMilliseconds / 1000} s; "
                        + "check that the data stream is enabled in YARG's settings");
                return;
            }

            await Task.Delay(poll).ConfigureAwait(false);
        }

        if (!settings.OpenLibraryAfterLaunch)
        {
            Finish("YARG is ready at its start menu");
            return;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(settings.SettleMilliseconds)).ConfigureAwait(false);

        string outcome;

        using (actuation.Hold())
        {
            // Re-read under the gate: a player may have already moved on.
            var snapshot = tracker.Snapshot(clock.GetUtcNow());

            if (snapshot.Scene != YargScene.Menu)
            {
                outcome = $"YARG is ready (scene={snapshot.Scene}); the Music Library was not opened";
            }
            else if (actuator.InputBlockedReason() is { } blocked)
            {
                outcome = $"YARG is ready, but the Music Library was not opened: {blocked}";
            }
            else if (!actuator.TryFocusYarg())
            {
                outcome = "YARG is ready, but it could not be brought to the front; the Music Library was not opened";
            }
            else if (actuator.ForegroundState() is { IsYargForeground: false } foreground)
            {
                outcome = $"YARG is ready, but {foreground.Owner} has the screen; the Music Library was not opened";
            }
            else if (!actuator.PressEnter())
            {
                outcome = "YARG is ready, but Windows refused the Enter that opens the Music Library";
            }
            else
            {
                outcome = "YARG is ready at the Music Library";
            }
        }

        Finish(outcome);
    }

    private string? Refusal()
    {
        var path = options.Value.ExecutablePath;

        if (string.IsNullOrWhiteSpace(path))
        {
            return "launching YARG is not configured on the theater PC (YargProcess:ExecutablePath)";
        }

        return File.Exists(path) ? null : $"the configured YARG executable does not exist: {path}";
    }

    private YargProcessStatus Describe()
    {
        var configured = !string.IsNullOrWhiteSpace(options.Value.ExecutablePath);

        if (_operation is not null)
        {
            return new(_operation, _detail, configured, _updatedAt);
        }

        var running = host.Count();
        var state = running switch
        {
            0 => "not-running",
            1 => "running",
            _ => "unknown",
        };

        // The last outcome stays readable until the process state contradicts it: a launch
        // that failed with no process left keeps its reason, and a YARG that later exits
        // on its own reads as not running rather than still "ready".
        var outcomeStillTrue = _detail.Length > 0 && (running > 0) == _outcomeHadProcess;
        var detail = running switch
        {
            0 when outcomeStillTrue => _detail,
            0 => "YARG is not running",
            1 when outcomeStillTrue => _detail,
            1 => "YARG is running",
            _ => $"{running} YARG instances are running",
        };

        return new(state, detail, configured, _updatedAt == default ? clock.GetUtcNow() : _updatedAt);
    }

    private YargProcessStatus Refused(string reason) =>
        new("refused", reason, !string.IsNullOrWhiteSpace(options.Value.ExecutablePath), clock.GetUtcNow());

    private void Begin(string operation, string detail)
    {
        _operation = operation;
        _detail = detail;
        _updatedAt = clock.GetUtcNow();
    }

    private void Note(string operation, string detail)
    {
        lock (_gate)
        {
            if (_operation == operation && _detail == detail)
            {
                return;
            }

            _operation = operation;
            _detail = detail;
            _updatedAt = clock.GetUtcNow();
        }
    }

    private void Finish(string detail)
    {
        var hadProcess = host.Count() > 0;

        lock (_gate)
        {
            _operation = null;
            _detail = detail;
            _outcomeHadProcess = hadProcess;
            _updatedAt = clock.GetUtcNow();
        }
    }
}

/// <summary>Launches YARG when Barkeep starts, if configured and not already running.</summary>
internal sealed class YargAutoLaunch(
    YargLifecycleService lifecycle,
    IOptions<YargProcessOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.LaunchAtStartup)
        {
            return;
        }

        // Give the UDP listener a moment to bind, so an already-running YARG is seen as one.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lifecycle.Launch();
    }
}

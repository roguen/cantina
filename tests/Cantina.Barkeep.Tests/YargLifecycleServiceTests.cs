// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.Barkeep.Yarg.Lifecycle;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// YARG's lifecycle (#23, D-038) against a scripted process host and a fed tracker: refusal
/// by name, readiness judged only by what the new process broadcasts, the Music Library
/// opened once, and restarts that never end a song without confirmation.
/// </summary>
public sealed class YargLifecycleServiceTests : IDisposable
{
    private sealed class FakeHost : IYargProcessHost
    {
        public int Running { get; set; }

        public bool IgnoresClose { get; set; }

        public List<string> Calls { get; } = [];

        public int Count() => Running;

        public void Start(string executablePath)
        {
            Calls.Add("start");
            Running = 1;
        }

        public bool RequestClose(TimeSpan timeout)
        {
            Calls.Add("close");

            if (!IgnoresClose)
            {
                Running = 0;
            }

            return Running == 0;
        }

        public bool Kill(TimeSpan timeout)
        {
            Calls.Add("kill");
            Running = 0;
            return true;
        }
    }

    private sealed class FakeActuator : IYargActuator
    {
        public List<string> Actions { get; } = [];

        public int YargProcessCount() => 1;

        public string? InputBlockedReason() => null;

        public bool TryFocusYarg()
        {
            Actions.Add("focus");
            return true;
        }

        public (bool IsYargForeground, string Owner) ForegroundState() => (true, "YARG");

        public bool ClickSearchBox()
        {
            Actions.Add("click");
            return true;
        }

        public bool ClearSearch()
        {
            Actions.Add("clear");
            return true;
        }

        public bool ClickAt(int x, int y) => true;

        public string TypeablePortion(string query) => query;

        public bool TypeQuery(string query)
        {
            Actions.Add($"type:{query}");
            return true;
        }

        public bool PressEnter()
        {
            Actions.Add("enter");
            return true;
        }

        public bool PressEscape() => true;
    }

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    private readonly YargSessionTracker _tracker = new();
    private readonly FakeHost _host = new();
    private readonly FakeActuator _actuator = new();
    private readonly ActuationGate _gate = new();
    private readonly SetlistJournal _journal;
    private readonly YargCueService _cue;
    private readonly string _executable;

    public YargLifecycleServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _executable = Path.Combine(_directory, "YARG.exe");
        File.WriteAllText(_executable, "stand-in");
        _journal = SetlistJournal.Open(Path.Combine(_directory, "journal"), TimeProvider.System);
        _cue = new YargCueService(_tracker, _actuator, _gate, _journal, TimeProvider.System);
    }

    public void Dispose()
    {
        _journal.Dispose();
        _gate.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Temp leftovers are cleaned by the OS.
        }
    }

    private YargLifecycleService Service(string? executable = null, int readyTimeout = 3000) =>
        new(_host, _actuator, _gate, _tracker, _cue,
            Options.Create(new YargProcessOptions
            {
                ExecutablePath = executable ?? _executable,
                ReadyTimeoutMilliseconds = readyTimeout,
                SettleMilliseconds = 0,
                CloseTimeoutMilliseconds = 100,
                PollMilliseconds = 10,
            }),
            TimeProvider.System);

    private void Feed(YargScene scene, YargPlayState playState = YargPlayState.NoSong) =>
        _tracker.OnDatagram(DatagramBuilder.Build(scene, playState), "s", DateTimeOffset.UtcNow);

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; waited < 300 && !condition(); waited++)
        {
            await Task.Delay(10);
        }

        Assert.True(condition(), "the condition never held");
    }

    [Fact]
    public void LaunchingRefusesByNameWhenNoExecutableIsConfigured()
    {
        var status = Service(executable: "").Launch();

        Assert.Equal("refused", status.State);
        Assert.Contains("not configured", status.Detail, StringComparison.Ordinal);
        Assert.Empty(_host.Calls);
    }

    [Fact]
    public void LaunchingRefusesByNameWhenTheExecutableIsMissing()
    {
        var status = Service(executable: Path.Combine(_directory, "gone.exe")).Launch();

        Assert.Equal("refused", status.State);
        Assert.Contains("does not exist", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void LaunchingARunningYargDoesNothing()
    {
        _host.Running = 1;

        var status = Service().Launch();

        Assert.Equal("running", status.State);
        Assert.Empty(_host.Calls);
    }

    [Fact]
    public async Task AFreshLaunchWaitsForTheLibraryThenOpensItOnce()
    {
        var service = Service();

        Assert.Equal("starting", service.Launch().State);

        // The stream goes live while the library is still loading: not ready yet.
        await Until(() => _host.Calls.Contains("start"));
        Feed(YargScene.Unknown);
        await Until(() => service.Status.State == "loading");
        Assert.Empty(_actuator.Actions);

        Feed(YargScene.Menu);
        await service.Work;

        Assert.Equal(["focus", "enter"], _actuator.Actions);
        Assert.Equal("running", service.Status.State);
        Assert.Contains("Music Library", service.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARestartIgnoresTheOldProcesssLastMenuReading()
    {
        // The tracker still says Live/Menu from before the launch. That reading belongs to
        // a process that no longer exists, and pressing Enter on it would land nowhere.
        Feed(YargScene.Menu);
        var service = Service();

        service.Launch();
        await Task.Delay(100);

        Assert.Equal("starting", service.Status.State);
        Assert.Empty(_actuator.Actions);

        Feed(YargScene.Menu);
        await service.Work;

        Assert.Equal(["focus", "enter"], _actuator.Actions);
    }

    [Fact]
    public async Task ALaunchThatNeverBroadcastsFailsByName()
    {
        var service = Service(readyTimeout: 150);

        service.Launch();
        await service.Work;

        Assert.Equal("running", service.Status.State);
        Assert.Contains("data stream never appeared", service.Status.Detail, StringComparison.Ordinal);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void ARestartDuringASongRefusesWithoutConfirmation()
    {
        _host.Running = 1;
        Feed(YargScene.Gameplay, YargPlayState.Playing);

        var status = Service().Restart(confirm: false);

        Assert.Equal("refused", status.State);
        Assert.Contains("confirm", status.Detail, StringComparison.Ordinal);
        Assert.Empty(_host.Calls);
    }

    [Fact]
    public async Task ARestartClosesPolitelyThenLaunchesAgain()
    {
        _host.Running = 1;
        Feed(YargScene.Menu);
        var service = Service();

        Assert.Equal("stopping", service.Restart(confirm: false).State);
        await Until(() => _host.Calls.Contains("start"));
        Feed(YargScene.Menu);
        await service.Work;

        Assert.Equal(["close", "start"], _host.Calls);
        Assert.Equal("running", service.Status.State);
    }

    [Fact]
    public async Task AYargThatIgnoresTheCloseIsTerminated()
    {
        _host.Running = 1;
        _host.IgnoresClose = true;
        Feed(YargScene.Menu);
        var service = Service();

        service.Restart(confirm: false);
        await Until(() => _host.Calls.Contains("start"));
        Feed(YargScene.Menu);
        await service.Work;

        Assert.Equal(["close", "kill", "start"], _host.Calls);
    }

    [Fact]
    public async Task ARestartFailsACueThatWasWaitingOnThePlayers()
    {
        _host.Running = 1;
        Feed(YargScene.Menu);
        _cue.Cue(new CueRequest("cue-1", new SetlistEntry("h1", "Song", "Band"), "song"));
        Assert.Equal("pending-players", _cue.Current!.State);

        var service = Service();
        service.Restart(confirm: false);
        await Until(() => _host.Calls.Contains("start"));
        Feed(YargScene.Menu);
        await service.Work;

        Assert.Equal("failed", _cue.Current!.State);
        Assert.Contains("restarted", _cue.Current.Detail, StringComparison.Ordinal);
    }
}

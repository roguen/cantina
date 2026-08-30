// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The debug stand-in against a scripted actuator: it refuses by name unless a cue is
/// actually waiting on the players and the confirms can land, and it sends exactly the
/// configured count when everything holds. The pacing is zeroed — cadence belongs to the
/// theater, not the test host.
/// </summary>
public sealed class PlayerStandInServiceTests : IDisposable
{
    private sealed class FakeActuator : IYargActuator
    {
        public int Processes { get; set; } = 1;

        public bool FocusSucceeds { get; set; } = true;

        public bool EnterSucceeds { get; set; } = true;

        public string? InputBlocked { get; set; }

        public List<string> Actions { get; } = [];

        public int YargProcessCount() => Processes;

        public string? InputBlockedReason() => InputBlocked;

        public bool TryFocusYarg()
        {
            Actions.Add("focus");
            return FocusSucceeds;
        }

        public (bool IsYargForeground, string Owner) ForegroundState() =>
            FocusSucceeds ? (true, "YARG") : (false, "holocron");

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

        public bool ClickAt(int x, int y)
        {
            Actions.Add($"click-at:{x},{y}");
            return true;
        }

        public string TypeablePortion(string query) => query;

        public bool TypeQuery(string query)
        {
            Actions.Add($"type:{query}");
            return true;
        }

        public bool PressEnter()
        {
            Actions.Add("enter");
            return EnterSucceeds;
        }

        public bool PressEscape()
        {
            Actions.Add("escape");
            return true;
        }
    }

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    private readonly YargSessionTracker _tracker = new();
    private readonly FakeActuator _actuator = new();
    private readonly SetlistJournal _journal;
    private readonly YargCueService _cue;
    private readonly PlayerStandInService _standIn;

    public PlayerStandInServiceTests()
    {
        _journal = SetlistJournal.Open(_directory, TimeProvider.System);
        _cue = new YargCueService(_tracker, _actuator, _journal, TimeProvider.System);
        _standIn = new PlayerStandInService(_tracker, _actuator, _cue, TimeProvider.System,
            Options.Create(new DebugOptions
            {
                Enabled = true,
                PlayerConfirmations = 2,
                ConfirmationLeadMilliseconds = 0,
                ConfirmationSettleMilliseconds = 0,
            }));
    }

    public void Dispose()
    {
        _journal.Dispose();

        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Temp leftovers are cleaned by the OS.
        }
    }

    private void FeedMenu() =>
        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu, YargPlayState.NoSong), "s", DateTimeOffset.UtcNow);

    private void CueASong()
    {
        FeedMenu();
        _cue.Cue(new CueRequest("cue-1", new SetlistEntry("h1", "Everlong", "Foo Fighters"), "everlong"));
        _actuator.Actions.Clear();
    }

    [Fact]
    public void RefusesWhenNoCueIsWaitingOnThePlayers()
    {
        // The confirms are Enters at whatever screen is up. Without a pending cue there
        // is no evidence instrument setup is on screen, and a blind Enter at an unknown
        // menu is exactly what the observation rules forbid.
        FeedMenu();

        var status = _standIn.Confirm();

        Assert.Equal("refused", status.State);
        Assert.Contains("cue a song first", status.Detail, StringComparison.Ordinal);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void RefusesWhenTheHostWouldSwallowTheInput()
    {
        CueASong();
        _actuator.InputBlocked = "the workstation is locked; injected input reaches the secure desktop, not the game";

        var status = _standIn.Confirm();

        Assert.Equal("refused", status.State);
        Assert.Equal(_actuator.InputBlocked, status.Detail);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void RefusesOnceTheSceneHasLeftTheMenu()
    {
        // Gameplay already started — perhaps a player confirmed for real. Enter during
        // gameplay is not a ready confirm any more.
        CueASong();
        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "s", DateTimeOffset.UtcNow);

        var status = _standIn.Confirm();

        Assert.Equal("refused", status.State);
        Assert.Contains("no longer on screen", status.Detail, StringComparison.Ordinal);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void SendsOneReadyConfirmPerConfiguredPlayer()
    {
        CueASong();
        FeedMenu();

        var status = _standIn.Confirm();

        Assert.Equal("sent", status.State);
        Assert.Equal(["focus", "enter", "enter"], _actuator.Actions);

        // Sending is not success: the cue is still the players' to resolve, by outcome.
        Assert.Equal("pending-players", _cue.Current!.State);
    }

    [Fact]
    public void ARefusedConfirmFailsNamingWhichOne()
    {
        CueASong();
        FeedMenu();
        _actuator.EnterSucceeds = false;

        var status = _standIn.Confirm();

        Assert.Equal("failed", status.State);
        Assert.Contains("confirm 1 of 2", status.Detail, StringComparison.Ordinal);
    }
}

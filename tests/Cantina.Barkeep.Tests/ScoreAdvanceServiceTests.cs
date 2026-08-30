// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The score-screen advance (#39) as a deterministic state machine: fed snapshots, judged
/// by what the scripted actuator was asked to do and where the cursor lands. The pacing
/// is zeroed — cadence belongs to the theater.
/// </summary>
public sealed class ScoreAdvanceServiceTests : IDisposable
{
    private sealed class FakeActuator : IYargActuator
    {
        public string? InputBlocked { get; set; }

        public List<string> Actions { get; } = [];

        public int YargProcessCount() => 1;

        public string? InputBlockedReason() => InputBlocked;

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
            return true;
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
    private readonly ScoreAdvanceService _advance;

    public ScoreAdvanceServiceTests()
    {
        _journal = SetlistJournal.Open(_directory, TimeProvider.System);
        _cue = new YargCueService(_tracker, _actuator, _journal, TimeProvider.System);
        _advance = new ScoreAdvanceService(_actuator, _cue, _journal,
            Options.Create(new AdvanceOptions
            {
                GraceMilliseconds = 0,
                MenuSettleMilliseconds = 0,
                OutcomeBoundMilliseconds = 10000,
            }),
            TimeProvider.System);

        _journal.Append(new SetlistIntent
        {
            CommandId = "add-1",
            Kind = SetlistIntentKind.Add,
            Entry = new SetlistEntry("h1", "First", "Band", @"C:\songs\first"),
        }, TimeProvider.System, out _);
        _journal.Append(new SetlistIntent
        {
            CommandId = "add-2",
            Kind = SetlistIntentKind.Add,
            Entry = new SetlistEntry("h2", "Second", "Band", @"C:\songs\second"),
        }, TimeProvider.System, out _);
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

    private LiveState Feed(YargScene scene, YargPlayState playState)
    {
        _tracker.OnDatagram(DatagramBuilder.Build(scene, playState), "s", DateTimeOffset.UtcNow);
        return _tracker.Snapshot(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void DisarmedItObservesAndTouchesNothing()
    {
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));

        Assert.Empty(_actuator.Actions);
        Assert.Equal("Idle", _advance.Status.Phase);
    }

    [Fact]
    public void AScoreScreenWithANextSongGetsOneContinueAfterTheGrace()
    {
        _advance.SetEnabled(true);

        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));   // enters grace
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));   // grace elapsed (0 ms): presses

        Assert.Equal(["focus", "enter"], _actuator.Actions);
        Assert.Equal("AwaitingMenu", _advance.Status.Phase);
    }

    [Fact]
    public void TheMenuAfterTheScoreScreenCuesTheNextEntry()
    {
        _advance.SetEnabled(true);
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _actuator.Actions.Clear();

        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));    // menu first seen
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));    // settled (0 ms): cues

        Assert.Equal(["focus", "click", "clear", "type:Second", "enter", "enter"], _actuator.Actions);
        Assert.Equal("Cueing", _advance.Status.Phase);
        Assert.Contains("Second", _advance.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AConfirmedCueMovesTheCursorAndTheEpisodeEnds()
    {
        _advance.SetEnabled(true);
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));

        // The requested song loads; the cue's own poller path confirms it.
        Feed(YargScene.Gameplay, YargPlayState.Playing);
        _tracker.OnCurrentSong(
            """{"Name":"Second","Artist":"Band","ActualLocation":"C:\\songs\\second","Hash":{"HashBytes":"h2"}}""");
        _cue.TryConfirm(_tracker.Snapshot(DateTimeOffset.UtcNow));

        _advance.Observe(_tracker.Snapshot(DateTimeOffset.UtcNow));

        Assert.Equal(1, _journal.State.Cursor);
        Assert.Equal("Idle", _advance.Status.Phase);
        Assert.Contains("advanced", _advance.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayersDismissingDuringTheGraceSkipsThePressButStillCues()
    {
        _advance.SetEnabled(true);
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));

        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));    // players pressed it
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));

        Assert.DoesNotContain("enter", _actuator.Actions.TakeWhile(action => action != "click"));
        Assert.Equal("Cueing", _advance.Status.Phase);
    }

    [Fact]
    public void NoNextSongMeansNoPressAndANamedReason()
    {
        _journal.Append(new SetlistIntent
        {
            CommandId = "move-end",
            Kind = SetlistIntentKind.MoveCursor,
            Cursor = 1,
        }, TimeProvider.System, out _);
        _advance.SetEnabled(true);

        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));

        Assert.Empty(_actuator.Actions);
        Assert.Contains("no next song", _advance.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ABlockedHostRefusesByNameWithoutPressing()
    {
        _actuator.InputBlocked = "the workstation is locked; injected input reaches the secure desktop, not the game";
        _advance.SetEnabled(true);

        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));

        Assert.Empty(_actuator.Actions);
        Assert.Contains("could not press CONTINUE", _advance.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AMismatchedLoadEndsTheEpisodeWithoutMovingTheCursor()
    {
        _advance.SetEnabled(true);
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Score, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));
        _advance.Observe(Feed(YargScene.Menu, YargPlayState.NoSong));

        Feed(YargScene.Gameplay, YargPlayState.Playing);
        _tracker.OnCurrentSong(
            """{"Name":"Wrong Song","Artist":"Other","ActualLocation":"C:\\songs\\wrong","Hash":{"HashBytes":"h9"}}""");
        _cue.TryConfirm(_tracker.Snapshot(DateTimeOffset.UtcNow));

        _advance.Observe(_tracker.Snapshot(DateTimeOffset.UtcNow));

        Assert.Equal(0, _journal.State.Cursor);
        Assert.Contains("failed", _advance.Status.Detail, StringComparison.Ordinal);
    }
}

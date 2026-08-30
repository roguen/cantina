// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The cue pipeline against a scripted actuator and a fed tracker: every gate refusal by
/// name, the pending-players resolution in both directions, idempotent replay, and
/// supersession. No sleeps, no sockets, no YARG — the acceptance run on the theater PC
/// proves the real actuator (D-008's split).
/// </summary>
public sealed class YargCueServiceTests : IDisposable
{
    private sealed class FakeActuator : IYargActuator
    {
        public int Processes { get; set; } = 1;

        public bool FocusSucceeds { get; set; } = true;

        public bool TypeSucceeds { get; set; } = true;

        /// <summary>Set to the named reason when the host would swallow injected input.</summary>
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

        public string TypeablePortion(string query) =>
            string.Join(' ',
                new string([.. query.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '\'' or ',' or '.')])
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        public bool TypeQuery(string query)
        {
            Actions.Add($"type:{query}");
            return TypeSucceeds;
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

    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 15, 0, 0, TimeSpan.Zero);

    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    private readonly YargSessionTracker _tracker = new();
    private readonly FakeActuator _actuator = new();
    private readonly SetlistJournal _journal;
    private readonly YargCueService _service;

    public YargCueServiceTests()
    {
        _journal = SetlistJournal.Open(_directory, TimeProvider.System);
        _service = new YargCueService(_tracker, _actuator, new ActuationGate(), _journal, TimeProvider.System);
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

    private static CueRequest Request(string id = "cue-1") =>
        new(id, new SetlistEntry("hash-1", "The Unforgiven", "Metallica"), "unforgiven");

    private void FeedMenu() =>
        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu, YargPlayState.NoSong), "s", DateTimeOffset.UtcNow);

    [Fact]
    public void RefusesWhenYargIsNotRunning()
    {
        _actuator.Processes = 0;
        FeedMenu();

        var status = _service.Cue(Request());

        Assert.Equal("refused", status.State);
        Assert.Equal("YARG is not running", status.Detail);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void ATitleWithUnmappableCharactersTypesItsTypeablePortion()
    {
        // The first live iPad cue died on "(Bang Your Head) Metal Health": the whole
        // query was refused because two characters had no scan code. The cue now types
        // what the keyboard can produce and lets verify-by-outcome judge the match.
        FeedMenu();

        var status = _service.Cue(new CueRequest(
            "cue-parens",
            new SetlistEntry("h9", "(Bang Your Head) Metal Health", "Quiet Riot"),
            "(Bang Your Head) Metal Health"));

        Assert.Equal("pending-players", status.State);
        Assert.Contains("type:Bang Your Head Metal Health", _actuator.Actions);
    }

    [Fact]
    public void RefusesWhenTheHostWouldSwallowTheInput()
    {
        // The failure this guards against is invisible after the fact: Windows accepts
        // every event and the game receives none. So it is refused before anything is
        // sent, and the reason is the one the host gave.
        _actuator.InputBlocked = "the workstation is locked; injected input reaches the secure desktop, not the game";
        FeedMenu();

        var status = _service.Cue(Request());

        Assert.Equal("refused", status.State);
        Assert.Equal(_actuator.InputBlocked, status.Detail);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void RefusesWhenTheStreamIsNotLive()
    {
        // Process alive, wire silent: the exact Holocron-hides-YARG shape of D-024.
        var status = _service.Cue(Request());

        Assert.Equal("refused", status.State);
        Assert.StartsWith("YARG is not observable", status.Detail, StringComparison.Ordinal);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void RefusesDuringAPausedSong()
    {
        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Paused), "s", DateTimeOffset.UtcNow);

        var status = _service.Cue(Request());

        Assert.Equal("refused", status.State);
        Assert.Equal("the game is paused; resume it on the pause menu", status.Detail);
    }

    [Fact]
    public void RefusesDuringGameplayBecauseACueNeverInterrupts()
    {
        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "s", DateTimeOffset.UtcNow);

        var status = _service.Cue(Request());

        Assert.Equal("refused", status.State);
        Assert.Contains("never interrupts", status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ActuatesInTheProvenOrderAndGoesPending()
    {
        FeedMenu();

        var status = _service.Cue(Request());

        Assert.Equal("pending-players", status.State);
        Assert.Equal(
            ["focus", "click", "clear", "type:unforgiven", "enter", "enter"],
            _actuator.Actions);
    }

    [Fact]
    public void ConfirmsWhenGameplayCarriesTheRequestedHash()
    {
        FeedMenu();
        _service.Cue(Request());

        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "s", DateTimeOffset.UtcNow);
        _tracker.OnCurrentSong(
            """{"Name":"The Unforgiven","Artist":"Metallica","Hash":{"HashBytes":"hash-1"}}""");

        _service.TryConfirm(_tracker.Snapshot(DateTimeOffset.UtcNow));

        Assert.Equal("done", _service.Current!.State);
    }

    [Fact]
    public void AMismatchedLoadFailsNamingWhatLoaded()
    {
        // D-017: fuzzy search can select something plausible and wrong; the honest
        // failure names it.
        FeedMenu();
        _service.Cue(Request());

        _tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "s", DateTimeOffset.UtcNow);
        _tracker.OnCurrentSong(
            """{"Name":"Bad Reputation","Artist":"Thin Lizzy","Hash":{"HashBytes":"other-hash"}}""");

        _service.TryConfirm(_tracker.Snapshot(DateTimeOffset.UtcNow));

        Assert.Equal("failed", _service.Current!.State);
        Assert.Contains("Bad Reputation", _service.Current.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AReplayedCommandIdIsNotReExecuted()
    {
        FeedMenu();
        _service.Cue(Request());
        _actuator.Actions.Clear();

        FeedMenu();
        var replay = _service.Cue(Request());

        Assert.Equal("replayed", replay.State);
        Assert.Empty(_actuator.Actions);
    }

    [Fact]
    public void ASecondCueSupersedesThePendingOneAsAmbiguous()
    {
        FeedMenu();
        _service.Cue(Request("cue-1"));

        FeedMenu();
        var second = _service.Cue(Request("cue-2"));

        Assert.Equal("pending-players", second.State);

        // The superseded command is resolved ambiguous in the journal: replaying it
        // reports that rather than acting.
        Assert.False(_journal.AppendPending(
            new SetlistIntent { CommandId = "cue-1", Kind = SetlistIntentKind.Cue }, TimeProvider.System));
    }

    [Fact]
    public void AFailedActuationResolvesTheJournalAndReportsTheStep()
    {
        FeedMenu();
        _actuator.TypeSucceeds = false;

        var status = _service.Cue(Request());

        Assert.Equal("failed", status.State);
        Assert.Contains("refused by Windows", status.Detail, StringComparison.Ordinal);
    }

    private sealed class SlowThreadSafeActuator : IYargActuator
    {
        private readonly object _lock = new();
        private readonly List<string> _actions = [];

        public IReadOnlyList<string> Snapshot()
        {
            lock (_lock)
            {
                return [.. _actions];
            }
        }

        private bool Record(string action)
        {
            lock (_lock)
            {
                _actions.Add(action);
            }

            return true;
        }

        public int YargProcessCount() => 1;

        public string? InputBlockedReason() => null;

        public bool TryFocusYarg() => Record("focus");

        public (bool IsYargForeground, string Owner) ForegroundState() => (true, "YARG");

        public bool ClickSearchBox() => Record("click");

        public bool ClearSearch() => Record("clear");

        public bool ClickAt(int x, int y) => Record($"click-at:{x},{y}");

        public string TypeablePortion(string query) => query;

        public bool TypeQuery(string query)
        {
            Record($"type:{query}");
            Thread.Sleep(30);   // widen the window two unserialized typings would collide in
            return true;
        }

        public bool PressEnter() => Record("enter");

        public bool PressEscape() => Record("escape");
    }

    [Fact]
    public async Task ConcurrentCuesNeverInterleaveTheirKeystrokes()
    {
        // Measured live 2026-08-30: a double-tapped Play now interleaved two typings
        // into "head mbeatnagl yhoeuarl thhead metal hea", matched nothing, and
        // stranded the cue at pending-players. The actuation gate serializes whole
        // sequences, pauses included.
        var actuator = new SlowThreadSafeActuator();
        using var gate = new ActuationGate();
        var service = new YargCueService(_tracker, actuator, gate, _journal, TimeProvider.System);
        FeedMenu();

        var first = Task.Run(() => service.Cue(
            new CueRequest("c-1", new SetlistEntry("h1", "One", "A"), "one")));
        var second = Task.Run(() => service.Cue(
            new CueRequest("c-2", new SetlistEntry("h2", "Two", "A"), "two")));
        await Task.WhenAll(first, second);

        var actions = actuator.Snapshot();
        var starts = actions.Select((action, index) => (action, index))
            .Where(pair => pair.action == "focus")
            .Select(pair => pair.index)
            .ToList();

        Assert.Equal(2, starts.Count);

        foreach (var start in starts)
        {
            Assert.Equal("click", actions[start + 1]);
            Assert.Equal("clear", actions[start + 2]);
            Assert.StartsWith("type:", actions[start + 3], StringComparison.Ordinal);
            Assert.Equal("enter", actions[start + 4]);
            Assert.Equal("enter", actions[start + 5]);
        }
    }
}

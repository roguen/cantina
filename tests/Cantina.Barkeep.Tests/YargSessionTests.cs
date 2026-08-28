// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Buffers.Binary;
using Cantina.YargSession;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// Builds datagrams to the normative layout in <c>docs/yarg-interface.md</c>, which is
/// itself capture-backed (D-010). These bytes test the parser against the documented
/// contract; they claim nothing about YARG behavior beyond what the captures already
/// established.
/// </summary>
internal static class DatagramBuilder
{
    public static byte[] Build(
        YargScene scene = YargScene.Menu,
        YargPlayState playState = YargPlayState.NoSong,
        byte version = 3)
    {
        var data = new byte[YargDatagram.LegacyLength];
        BinaryPrimitives.WriteUInt32LittleEndian(data, YargDatagram.HeaderMagic);
        data[4] = version;
        data[5] = 1;
        data[6] = (byte)scene;
        data[7] = (byte)playState;
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(9), 120f);
        return data;
    }
}

public sealed class YargDatagramTests
{
    [Fact]
    public void ParsesTheCaptureConfirmedLayout()
    {
        var data = DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing);

        var parsed = YargDatagram.TryParse(data, out var datagram, out var rejection);

        Assert.True(parsed);
        Assert.Null(rejection);
        Assert.NotNull(datagram);
        Assert.Equal(3, datagram.DatagramVersion);
        Assert.Equal(YargScene.Gameplay, datagram.Scene);
        Assert.Equal(YargPlayState.Playing, datagram.PlayState);
        Assert.Equal(120f, datagram.BeatsPerMinute);
        Assert.Empty(datagram.StarPower);
    }

    [Fact]
    public void RejectsAWrongHeaderWithAReason()
    {
        var data = DatagramBuilder.Build();
        data[0] = 0x00;

        Assert.False(YargDatagram.TryParse(data, out var datagram, out var rejection));
        Assert.Null(datagram);
        Assert.NotNull(rejection);
    }

    [Fact]
    public void RejectsAShortDatagramInsteadOfGuessing()
    {
        var data = DatagramBuilder.Build()[..(YargDatagram.LegacyLength - 1)];

        Assert.False(YargDatagram.TryParse(data, out _, out var rejection));
        Assert.Contains("short", rejection, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadsByteSevenAsThreeStates()
    {
        // D-012: reading this byte as a boolean collapsed Playing and Paused and made this
        // project document its own parsing bug as an upstream quirk. Never again.
        foreach (var state in new[] { YargPlayState.NoSong, YargPlayState.Playing, YargPlayState.Paused })
        {
            Assert.True(YargDatagram.TryParse(DatagramBuilder.Build(playState: state), out var datagram, out _));
            Assert.Equal(state, datagram!.PlayState);
        }
    }
}

public sealed class CurrentSongDocumentTests
{
    private const string RealShapedJson =
        """
        {"SubType":0,"ActualLocation":"C:\\Songs\\Trivium - Detonation",
         "Hash":{"HashBytes":"c8x9d+RbXWICoVQWFZQ57vLAzCI="},
         "Name":"Detonation","Artist":"Trivium",
         "Charter":"Harm<color=#0072bc>o</color>nix"}
        """;

    [Fact]
    public void ReadsTheNestedHashBytes()
    {
        // The hash is Hash.HashBytes. Reading the outer key returns the inner key's NAME,
        // constant across songs - the defect that silently suppressed identity changes in
        // the D-018 harness.
        Assert.True(CurrentSongDocument.TryParse(RealShapedJson, out var document, out var empty));
        Assert.False(empty);
        Assert.Equal("c8x9d+RbXWICoVQWFZQ57vLAzCI=", document!.Hash);
        Assert.Equal("Detonation", document.Title);
        Assert.Equal("Trivium", document.Artist);
    }

    [Fact]
    public void EmptyContentIsNoSongNotAnError()
    {
        // Zero-length is a real value on this machine: "no song loaded" is an empty file,
        // not a missing one (D-021 spec review).
        Assert.False(CurrentSongDocument.TryParse(string.Empty, out _, out var empty));
        Assert.True(empty);
    }

    [Fact]
    public void MalformedJsonIsAnErrorNotNoSong()
    {
        Assert.False(CurrentSongDocument.TryParse("{\"Hash\":", out _, out var empty));
        Assert.False(empty);
    }
}

public sealed class YargSessionTrackerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 12, 0, 0, TimeSpan.Zero);

    private const string SongJson =
        """{"Name":"The Unforgiven","Artist":"Metallica","Hash":{"HashBytes":"Is90eweGbwOBNrH8z1KcR+ncK1Y="}}""";

    [Fact]
    public void LatchesIdentityAndHoldsItThroughTheScoreScreen()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "sender", T0);
        tracker.OnCurrentSong(SongJson);

        // The file clears ~86 ms after the scene changes (D-010). Empty must not un-know.
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Score), "sender", T0.AddSeconds(1));
        tracker.OnCurrentSong(string.Empty);

        var snapshot = tracker.Snapshot(T0.AddSeconds(1));
        Assert.Equal(YargScene.Score, snapshot.Scene);
        Assert.NotNull(snapshot.Song);
        Assert.Equal("The Unforgiven", snapshot.Song.Title);
        Assert.Equal(SongSource.Observed, snapshot.SongSource);
    }

    [Fact]
    public void ClearsTheLatchAfterAMenuDwell()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "sender", T0);
        tracker.OnCurrentSong(SongJson);

        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu), "sender", T0.AddSeconds(2));
        Assert.NotNull(tracker.Snapshot(T0.AddSeconds(2)).Song);

        var afterDwell = T0.AddSeconds(2) + YargSessionTracker.MenuDwellToClearLatch;
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu), "sender", afterDwell);
        Assert.Null(tracker.Snapshot(afterDwell).Song);
    }

    [Fact]
    public void ReportsMultipleSendersAsTheNamedFault()
    {
        // The stream is a LAN broadcast; interleaving two games manufactured a withdrawn
        // finding once (Time Log session 009). Never resolved by picking a sender.
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(), "hostA:61374", T0);
        tracker.OnDatagram(DatagramBuilder.Build(), "hostB:52000", T0.AddMilliseconds(10));

        var snapshot = tracker.Snapshot(T0.AddMilliseconds(20));
        Assert.Equal(SessionFault.MultipleSources, snapshot.Fault);
        Assert.Equal(2, snapshot.Senders.Count);
    }

    [Fact]
    public void FreshnessDemotesOnlyAfterTheDebounce()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(), "sender", T0);

        Assert.Equal(LiveFreshness.Live, tracker.Snapshot(T0.AddMilliseconds(100)).Freshness);

        // Raw tier is Stale at 600 ms, but a healthy run has shown a 538 ms gap (D-018),
        // so the demotion must hold for the 1 s debounce before it is reported.
        Assert.Equal(LiveFreshness.Live, tracker.Snapshot(T0.AddMilliseconds(600)).Freshness);
        Assert.Equal(LiveFreshness.Stale, tracker.Snapshot(T0.AddMilliseconds(1700)).Freshness);
    }

    [Fact]
    public void APromotionIsImmediate()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(), "sender", T0);
        Assert.Equal(LiveFreshness.Stale, tracker.Snapshot(T0.AddSeconds(2)).Freshness);

        tracker.OnDatagram(DatagramBuilder.Build(), "sender", T0.AddSeconds(2));
        Assert.Equal(LiveFreshness.Live, tracker.Snapshot(T0.AddSeconds(2).AddMilliseconds(1)).Freshness);
    }

    [Fact]
    public void SilenceBecomesStreamDeadWithItsName()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(DatagramBuilder.Build(), "sender", T0);

        var snapshot = tracker.Snapshot(T0.AddSeconds(10));
        Assert.Equal(LiveFreshness.Dead, snapshot.Freshness);
        Assert.Equal(SessionFault.StreamDead, snapshot.Fault);
    }

    [Fact]
    public void NoDatagramsEverIsItsOwnNamedFault()
    {
        var tracker = new YargSessionTracker();

        var snapshot = tracker.Snapshot(T0);
        Assert.Equal(LiveFreshness.Dead, snapshot.Freshness);
        Assert.Equal(SessionFault.NoDatagrams, snapshot.Fault);
    }

    [Fact]
    public void PortConflictIsSurfacedNotRetried()
    {
        // D-013: a failed bind is a named, actionable condition - another application
        // holds the YARG data port - never an empty or frozen live state.
        var tracker = new YargSessionTracker();
        tracker.ReportPortConflict();

        Assert.Equal(SessionFault.PortConflict, tracker.Snapshot(T0).Fault);
    }

    [Fact]
    public void MalformedDatagramsAreCountedNeverGuessed()
    {
        var tracker = new YargSessionTracker();
        tracker.OnDatagram(new byte[10], "sender", T0);

        var snapshot = tracker.Snapshot(T0);
        Assert.Equal(0, snapshot.DatagramsAccepted);
        Assert.Equal(1, snapshot.DatagramsRejected);
        Assert.Null(snapshot.ReceivedAt);
    }
}

public sealed class LatchReassertionTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);

    private const string Song =
        """{"Name":"The Unforgiven","Artist":"Metallica","Hash":{"HashBytes":"Is90ewe"}}""";

    [Fact]
    public void TheSameSongRelatchesAfterAMenuDwellClear()
    {
        // The self-test caught this on the theater PC: the file populates during the
        // load screen, before the wire says Gameplay, so the latch lands inside a stale
        // menu dwell, is cleared, and the leftover dedup hash then blocked the same song
        // from ever latching again.
        var tracker = new YargSessionTracker();

        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu), "s", T0);
        var staleDwell = T0 + YargSessionTracker.MenuDwellToClearLatch + TimeSpan.FromSeconds(10);
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu), "s", staleDwell);

        tracker.OnCurrentSong(Song);
        Assert.NotNull(tracker.Snapshot(staleDwell).Song);

        // The next menu datagram clears the latch (the dwell is long past).
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Menu), "s", staleDwell.AddMilliseconds(100));
        Assert.Null(tracker.Snapshot(staleDwell.AddMilliseconds(100)).Song);

        // The file still holds the same song; the next poll must re-latch it.
        tracker.OnCurrentSong(Song);
        Assert.NotNull(tracker.Snapshot(staleDwell.AddMilliseconds(200)).Song);

        // And once gameplay arrives the latch sticks.
        tracker.OnDatagram(DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing), "s", staleDwell.AddMilliseconds(300));
        Assert.NotNull(tracker.Snapshot(staleDwell.AddMilliseconds(300)).Song);
    }
}

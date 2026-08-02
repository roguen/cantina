// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Globalization;
using System.Net;

namespace Cantina.Spikes.YargObserve;

/// <summary>
/// Accumulates exactly the evidence issue #2 asks for: whether YARG emits, at what
/// datagram version and rate, to which destination, and across which scene transitions.
/// </summary>
internal sealed class CaptureStats
{
    /// <summary>Bytes 0 through 46; the version-3 datagram ends there.</summary>
    private const int TrackedOffsets = 47;

    private readonly int[] _offsetChanges = new int[TrackedOffsets];
    private readonly HashSet<byte>[] _offsetValues =
        [.. Enumerable.Range(0, TrackedOffsets).Select(_ => new HashSet<byte>())];

    private YargScene? _scene;
    private YargPlayState? _playState;
    private byte[]? _previous;

    public long Accepted { get; private set; }

    public long Rejected { get; set; }

    public bool CurrentSongObserved { get; set; }

    public HashSet<byte> Versions { get; } = [];

    public HashSet<int> Lengths { get; } = [];

    public HashSet<string> Senders { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Destinations { get; } = new(StringComparer.Ordinal);

    public List<YargScene> SceneOrder { get; } = [];

    public int MaxPlayers { get; private set; }

    public void Record(YargDatagram datagram, int length, EndPoint? sender, IPAddress? destination)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        Accepted++;
        Versions.Add(datagram.DatagramVersion);
        Lengths.Add(length);
        MaxPlayers = Math.Max(MaxPlayers, datagram.StarPower.Count);

        if (sender is not null)
        {
            Senders.Add(sender.ToString() ?? "unknown");
        }

        if (destination is not null)
        {
            Destinations.Add(destination.ToString());
        }
    }

    /// <summary>
    /// Reports a scene transition. The first observed scene is reported too, unless it is
    /// <see cref="YargScene.Unknown"/>, which carries no information.
    /// </summary>
    public bool TryTakeSceneChange(YargDatagram datagram, out YargScene previous)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        previous = _scene ?? YargScene.Unknown;
        if (_scene == datagram.Scene)
        {
            return false;
        }

        var isFirst = _scene is null;
        _scene = datagram.Scene;
        SceneOrder.Add(datagram.Scene);

        return !isFirst || datagram.Scene != YargScene.Unknown;
    }

    /// <summary>Reports play-state transitions after the first observation sets a baseline.</summary>
    public bool TryTakePlayStateChange(YargDatagram datagram, out YargPlayState previous)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        previous = _playState ?? YargPlayState.NoSong;

        if (_playState == datagram.PlayState)
        {
            return false;
        }

        var isFirst = _playState is null;
        _playState = datagram.PlayState;

        return !isFirst;
    }

    /// <summary>
    /// Counts how often each byte offset changes and which values it takes. A field that
    /// never moves across a whole session is a different kind of fact from one that moves
    /// constantly, and separating the two is how an unknown byte gets identified.
    /// </summary>
    public void ObserveBytes(ReadOnlySpan<byte> datagram)
    {
        var length = Math.Min(datagram.Length, TrackedOffsets);

        for (var offset = 0; offset < length; offset++)
        {
            var value = datagram[offset];

            if (_offsetValues[offset].Count < 16)
            {
                _offsetValues[offset].Add(value);
            }

            if (_previous is not null && _previous.Length > offset && _previous[offset] != value)
            {
                _offsetChanges[offset]++;
            }
        }

        _previous = datagram[..length].ToArray();
    }

    /// <summary>
    /// Separates bytes that moved from bytes that never did. Both matter: a frozen byte is
    /// where an unidentified state flag hides, and reporting "nothing moved" explicitly is
    /// what keeps an empty section from reading as a broken tool.
    /// </summary>
    public IEnumerable<string> SummarizeByteActivity()
    {
        yield return "  BYTE ACTIVITY";

        if (Accepted == 0)
        {
            yield return "    no datagrams accepted";
            yield break;
        }

        yield return Line($"    byte  7: {_offsetChanges[7]} changes, values [{Render(7)}]  (play state)");

        var changed = Enumerable
            .Range(0, TrackedOffsets)
            .Where(offset => _offsetChanges[offset] > 0)
            .OrderByDescending(offset => _offsetChanges[offset])
            .ToList();

        if (changed.Count == 0)
        {
            yield return "    no byte changed at all during this run";
        }
        else
        {
            yield return "    changed:";
            foreach (var offset in changed)
            {
                yield return Line($"      byte {offset,2}: {_offsetChanges[offset],7} changes, values [{Render(offset)}]");
            }
        }

        var frozen = Enumerable
            .Range(0, TrackedOffsets)
            .Where(offset => _offsetChanges[offset] == 0)
            .Select(offset => $"{offset}=0x{_offsetValues[offset].FirstOrDefault():X2}")
            .ToList();

        yield return Line($"    frozen for the whole run ({frozen.Count} offsets):");

        for (var i = 0; i < frozen.Count; i += 10)
        {
            yield return "      " + string.Join("  ", frozen.Skip(i).Take(10));
        }
    }

    private string Render(int offset)
    {
        var values = _offsetValues[offset];

        return values.Count >= 16
            ? "16+ distinct"
            : string.Join(" ", values.Order().Select(value => $"0x{value:X2}"));
    }

    public IEnumerable<string> Summarize(TimeSpan elapsed, bool yargRunning)
    {
        var rate = elapsed.TotalSeconds > 0 ? Accepted / elapsed.TotalSeconds : 0;

        yield return Line($"SUMMARY over {elapsed.TotalSeconds:0.0}s");
        yield return Line($"  accepted {Accepted}, rejected {Rejected}, rate {rate:0.0}/s");
        yield return Line($"  datagram versions: {Format(Versions)}");
        yield return Line($"  datagram lengths:  {Format(Lengths)}");
        yield return Line($"  senders:           {Format(Senders)}");
        yield return Line($"  destinations:      {Format(Destinations)}  <- broadcast vs unicast answer");
        yield return Line($"  max players seen:  {MaxPlayers}");
        // Cap this. With two producers on the port the order alternates thousands of times
        // and floods everything useful off the screen.
        var order = SceneOrder.Count switch
        {
            0 => "none",
            <= 24 => string.Join(" -> ", SceneOrder),
            _ => string.Join(" -> ", SceneOrder.Take(12)) +
                 $"  ... [{SceneOrder.Count} transitions total] ...  " +
                 string.Join(" -> ", SceneOrder.TakeLast(4)),
        };

        yield return Line($"  scene order:       {order}");

        if (Senders.Count > 1)
        {
            yield return $"  WARNING: {Senders.Count} distinct senders on this port. More than one YARG";
            yield return "  instance is broadcasting, so scene and play state are interleaved between";
            yield return "  games and belong to neither. Close all but one before trusting any reading.";
        }
        yield return Line($"  currentSong populated: {CurrentSongObserved}");
        yield return Line($"  yarg process at end:   {yargRunning}");

        if (Accepted == 0)
        {
            yield return "  NO DATAGRAMS. Check that UDP Data Stream is enabled in YARG, that YARG is";
            yield return "  running, and that the Windows firewall is not dropping inbound UDP.";
        }
    }

    private static string Line(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Format<T>(IReadOnlyCollection<T> values) =>
        values.Count == 0
            ? "none"
            : string.Join(", ", values.Select(value => value?.ToString() ?? "null").Order(StringComparer.Ordinal));
}

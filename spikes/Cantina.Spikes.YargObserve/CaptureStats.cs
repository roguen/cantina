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
    private YargScene? _scene;
    private bool? _paused;

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

    /// <summary>Reports any pause-state change after the first observation establishes a baseline.</summary>
    public bool TryTakePauseChange(YargDatagram datagram)
    {
        ArgumentNullException.ThrowIfNull(datagram);

        if (_paused == datagram.Paused)
        {
            return false;
        }

        var isFirst = _paused is null;
        _paused = datagram.Paused;

        return !isFirst;
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
        yield return Line($"  scene order:       {(SceneOrder.Count > 0 ? string.Join(" -> ", SceneOrder) : "none")}");
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

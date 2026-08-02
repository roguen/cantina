// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Globalization;

namespace Cantina.Spikes.YargObserve;

/// <summary>One operator mark: the whole datagram, frozen at the instant the operator pressed Enter.</summary>
internal sealed record CaptureMark(int Index, TimeSpan Elapsed, string Label, YargScene Scene, byte[] Datagram);

/// <summary>
/// Operator-driven marks, so a capture can answer "did anything change when I did X?"
///
/// Without marks, a byte that never moves is ambiguous: it cannot be distinguished from an
/// operator who forgot to perform the action. A mark on each side of the action turns that
/// non-event into evidence.
/// </summary>
internal sealed class MarkLog
{
    private readonly List<CaptureMark> _marks = [];

    public int Count => _marks.Count;

    public CaptureMark Add(TimeSpan elapsed, string label, YargScene scene, ReadOnlySpan<byte> datagram)
    {
        var mark = new CaptureMark(_marks.Count + 1, elapsed, label, scene, datagram.ToArray());
        _marks.Add(mark);
        return mark;
    }

    /// <summary>
    /// Diffs consecutive marks. The offsets that changed between "before I paused" and
    /// "after I paused" are the pause signal, wherever it actually lives.
    /// </summary>
    public IEnumerable<string> Summarize()
    {
        if (_marks.Count == 0)
        {
            yield return "  no operator marks recorded";
            yield return "  press Enter during a run to mark the timeline; two marks around an";
            yield return "  action show exactly which bytes that action moved";
            yield break;
        }

        foreach (var mark in _marks)
        {
            yield return Line($"  mark {mark.Index} at {mark.Elapsed.TotalSeconds:0.000}s scene={mark.Scene} play={(YargPlayState)mark.Datagram[7]} \"{mark.Label}\"");
        }

        if (_marks.Count < 2)
        {
            yield return "  only one mark: no pair to diff";
            yield break;
        }

        yield return string.Empty;
        yield return "  DIFFS BETWEEN CONSECUTIVE MARKS";

        for (var i = 1; i < _marks.Count; i++)
        {
            var before = _marks[i - 1];
            var after = _marks[i];
            var changes = Diff(before.Datagram, after.Datagram).ToList();

            yield return Line($"  {before.Index} -> {after.Index} ({before.Elapsed.TotalSeconds:0.0}s -> {after.Elapsed.TotalSeconds:0.0}s)");

            if (changes.Count == 0)
            {
                yield return "      no byte changed";
                continue;
            }

            foreach (var change in changes)
            {
                yield return $"      {change}";
            }
        }
    }

    private static IEnumerable<string> Diff(byte[] before, byte[] after)
    {
        var length = Math.Min(before.Length, after.Length);

        for (var offset = 0; offset < length; offset++)
        {
            if (before[offset] == after[offset])
            {
                continue;
            }

            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"byte {offset,2} : 0x{before[offset]:X2} -> 0x{after[offset]:X2}{Annotate(offset)}");
        }

        if (before.Length != after.Length)
        {
            yield return $"length {before.Length} -> {after.Length}";
        }
    }

    /// <summary>Names the offsets this spike cares about, so a diff is readable without the spec open.</summary>
    private static string Annotate(int offset) => offset switch
    {
        4 => "  (datagram version)",
        6 => "  (scene)",
        7 => "  (play state: 0 no song, 1 playing, 2 paused)",
        8 => "  (venue size)",
        13 => "  (song section)",
        14 => "  (guitar notes)",
        15 => "  (bass notes)",
        16 => "  (drum notes)",
        17 => "  (keys notes)",
        34 => "  (lighting cue)",
        35 => "  (post processing)",
        36 => "  (fog)",
        37 => "  (strobe)",
        38 => "  (beat)",
        39 => "  (keyframe)",
        40 => "  (bonus effect)",
        41 => "  (autogen)",
        >= 9 and <= 12 => "  (bpm float)",
        >= 18 and <= 33 => "  (vocal/harmony float)",
        >= 44 and <= 46 => "  (camera cut)",
        _ => string.Empty,
    };

    private static string Line(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Publishes the most recent accepted datagram to the mark reader. Each update assigns a
/// fresh array, so a reader either sees the whole previous datagram or the whole new one.
/// </summary>
internal sealed class LatestDatagram
{
    private sealed record Snapshot(byte[] Datagram, YargScene Scene);

    private Snapshot? _value;

    public void Set(ReadOnlySpan<byte> datagram, YargScene scene) =>
        Volatile.Write(ref _value, new Snapshot(datagram.ToArray(), scene));

    public bool TryGet(out byte[] datagram, out YargScene scene)
    {
        var snapshot = Volatile.Read(ref _value);
        datagram = snapshot?.Datagram ?? [];
        scene = snapshot?.Scene ?? YargScene.Unknown;
        return snapshot is not null;
    }
}

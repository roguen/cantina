// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Library;

/// <summary>
/// Which instruments a chart supports, in the Clone Hero vocabulary every source here
/// already speaks: <c>diff_guitar</c>, <c>diff_bass</c>, <c>diff_drums</c>,
/// <c>diff_keys</c>, <c>diff_vocals</c>. A value of −1 means the instrument is not
/// charted; 0–6 is the chart's own difficulty rating. A vocals chart is what makes
/// lyrics available, which is the selection criterion the operator actually uses when
/// several versions of a song exist. The same shape describes a local song.ini, a local
/// .sng, and a Chorus Encore search result, so the iPad renders one thing.
/// </summary>
public sealed record SongInstruments(int Guitar, int Bass, int Drums, int Keys, int Vocals)
{
    public static readonly SongInstruments Unknown = new(-1, -1, -1, -1, -1);

    /// <summary>Reads the five diff keys from any string-keyed metadata bag; a missing
    /// or unparseable value is "not charted", never an error.</summary>
    public static SongInstruments FromValues(IReadOnlyDictionary<string, string> values) =>
        new(Diff(values, "diff_guitar"),
            Diff(values, "diff_bass"),
            Diff(values, "diff_drums"),
            Diff(values, "diff_keys"),
            Diff(values, "diff_vocals"));

    private static int Diff(IReadOnlyDictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var raw)
            && int.TryParse(raw.Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : -1;
}

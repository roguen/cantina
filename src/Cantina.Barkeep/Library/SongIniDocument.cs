// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Library;

/// <summary>
/// The metadata block of a Clone Hero-format <c>song.ini</c> — the file YARG itself reads,
/// which is what makes the filesystem the authoritative metadata source (D-025) rather
/// than YARG's private <c>songcache.bin</c>. Parsing is deliberately forgiving about
/// section-name case and whitespace and deliberately strict about what it promises: a
/// folder without a usable name and artist is skipped with a named reason, never guessed.
/// </summary>
public sealed record SongIniDocument
{
    public required string Title { get; init; }

    public required string Artist { get; init; }

    public string Album { get; init; } = string.Empty;

    public string Genre { get; init; } = string.Empty;

    public string Year { get; init; } = string.Empty;

    public string Charter { get; init; } = string.Empty;

    public int? SongLengthMilliseconds { get; init; }

    public SongInstruments Instruments { get; init; } = SongInstruments.Unknown;

    public static bool TryParse(string content, out SongIniDocument? document)
    {
        document = null;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inSongSection = false;

        foreach (var raw in content.Split('\n'))
        {
            var line = raw.Trim();

            if (line.Length == 0 || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inSongSection = line[1..^1].Trim().Equals("song", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSongSection)
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (key.Length > 0 && !values.ContainsKey(key))
            {
                values[key] = value;
            }
        }

        if (!values.TryGetValue("name", out var title) || title.Length == 0
            || !values.TryGetValue("artist", out var artist) || artist.Length == 0)
        {
            return false;
        }

        document = new SongIniDocument
        {
            Title = title,
            Artist = artist,
            Album = values.GetValueOrDefault("album", string.Empty),
            Genre = values.GetValueOrDefault("genre", string.Empty),
            Year = values.GetValueOrDefault("year", string.Empty),
            Charter = values.GetValueOrDefault("charter", string.Empty),
            SongLengthMilliseconds =
                values.TryGetValue("song_length", out var length)
                && int.TryParse(length, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0
                    ? parsed
                    : null,
            Instruments = SongInstruments.FromValues(values),
        };

        return true;
    }
}

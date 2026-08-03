// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;

namespace Cantina.Spikes.YargInput;

/// <summary>
/// Reads YARG's <c>currentSong.json</c>, which is the only surface that identifies the song
/// actually loaded. For a selection spike this is the whole point: the datagram can prove a
/// song started, but only this file proves <em>which</em> song, and "which" is the entire
/// question in issue #4.
/// </summary>
internal sealed record CurrentSong(string Location, string ContentHash)
{
    public static CurrentSong? TryRead(string directory)
    {
        var path = Path.Combine(directory, "currentSong.json");

        string raw;

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            raw = reader.ReadToEnd();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            var location = root.TryGetProperty("ActualLocation", out var locationValue)
                ? locationValue.GetString() ?? string.Empty
                : string.Empty;

            var hash = string.Empty;

            if (root.TryGetProperty("Hash", out var hashValue) &&
                hashValue.TryGetProperty("HashBytes", out var bytes))
            {
                hash = bytes.GetString() ?? string.Empty;
            }

            return location.Length == 0 && hash.Length == 0
                ? null
                : new CurrentSong(location, hash);
        }
        catch (JsonException)
        {
            // A partially written file is expected; the caller retries.
            return null;
        }
    }

    /// <summary>Last two path segments, which is enough to identify the chart without printing a full local path.</summary>
    public string ShortLocation
    {
        get
        {
            var parts = Location.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 2 ? Location : string.Join('/', parts[^2..]);
        }
    }
}

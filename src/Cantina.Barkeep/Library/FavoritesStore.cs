// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;

namespace Cantina.Barkeep.Library;

/// <summary>One star, set or cleared.</summary>
public sealed record FavoriteRequest(string Location, bool Favored);

/// <summary>
/// The operator's starred songs, by location — the filter that narrows 448 songs to the
/// ones the house actually plays. Pure preference state: no journal, no observation,
/// just a durable set with atomic writes so a crash mid-save can never eat the list. A
/// damaged file is set aside by name and the store starts empty rather than refusing to
/// boot — losing stars is recoverable; a dead host is not.
/// </summary>
public sealed class FavoritesStore
{
    private const string FileName = "favorites.json";

    private readonly object _gate = new();
    private readonly string _path;
    private readonly HashSet<string> _locations;

    private FavoritesStore(string path, HashSet<string> locations)
    {
        _path = path;
        _locations = locations;
    }

    public static FavoritesStore Open(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, FileName);
        var locations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(path))
        {
            try
            {
                var stored = JsonSerializer.Deserialize<string[]>(File.ReadAllText(path)) ?? [];
                locations.UnionWith(stored);
            }
            catch (JsonException)
            {
                File.Move(path, path + ".damaged-" + Path.GetRandomFileName(), overwrite: false);
            }
        }

        return new FavoritesStore(path, locations);
    }

    public IReadOnlyList<string> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _locations];
            }
        }
    }

    /// <summary>Stars or unstars a location. Idempotent; returns the resulting state.</summary>
    public bool Set(string location, bool favored)
    {
        lock (_gate)
        {
            var changed = favored ? _locations.Add(location) : _locations.Remove(location);

            if (changed)
            {
                // Write-then-move, so the file on disk is always a complete list.
                var staging = _path + ".writing";
                File.WriteAllText(staging, JsonSerializer.Serialize(_locations.Order(StringComparer.OrdinalIgnoreCase)));
                File.Move(staging, _path, overwrite: true);
            }

            return favored;
        }
    }
}

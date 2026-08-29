// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;

namespace Cantina.Barkeep.Library;

public sealed class LibraryOptions
{
    public const string SectionName = "Library";

    /// <summary>
    /// Song directories to index. Empty means mirror YARG's own configuration: the
    /// <c>SongFolders</c> array in its settings file, read-only — Cantina indexes exactly
    /// what the game indexes, so search results are always cueable.
    /// </summary>
    public IList<string> SongDirectories { get; } = [];

    public IReadOnlyList<string> ResolveDirectories(string yargDirectory)
    {
        if (SongDirectories.Count > 0)
        {
            return [.. SongDirectories];
        }

        var settingsPath = Path.Combine(yargDirectory, "settings.json");

        try
        {
            using var json = JsonDocument.Parse(File.ReadAllText(settingsPath));

            if (json.RootElement.TryGetProperty("SongFolders", out var folders)
                && folders.ValueKind == JsonValueKind.Array)
            {
                return [.. folders.EnumerateArray()
                    .Where(f => f.ValueKind == JsonValueKind.String)
                    .Select(f => f.GetString()!)];
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Fall through to empty: a missing YARG install indexes nothing, honestly.
        }

        return [];
    }
}

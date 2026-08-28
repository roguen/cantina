// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;

namespace Cantina.YargSession;

/// <summary>
/// The song identity YARG states on disk in <c>currentSong.json</c>, beside its settings.
///
/// Two facts about this file shape every reader (D-010, D-017):
/// it is <b>zero-length</b> while no song is loaded — empty is a value, not an error —
/// and it clears about 86 ms after the scene changes, so identity must be latched by the
/// caller rather than read on demand at a boundary.
///
/// The hash lives at <c>Hash.HashBytes</c>. Reading the outer <c>"Hash"</c> key returns
/// the inner key's <em>name</em>, constant across songs; that defect once suppressed the
/// identity-change signal an entire measurement depended on (D-018's harness).
/// </summary>
public sealed record CurrentSongDocument
{
    public required string Title { get; init; }

    public required string Artist { get; init; }

    /// <summary>Stable content hash, base64 as YARG writes it.</summary>
    public required string Hash { get; init; }

    /// <summary>Absolute path of the loaded song's folder or archive.</summary>
    public required string Location { get; init; }

    // YARG's key names, not this type's property names: the outer "Hash" object holding
    // "HashBytes" is the file's shape, and it must not follow a rename of the Hash
    // property above. CA1507 would otherwise couple the two.
    private const string OuterHashKey = "Hash";
    private const string HashBytesKey = "HashBytes";

    /// <summary>
    /// Parses the file content. Returns false for empty content (no song loaded) and for
    /// malformed or unexpectedly shaped JSON; the two cases are distinguished by
    /// <paramref name="empty"/> so a caller never treats a parse failure as "no song".
    /// </summary>
    public static bool TryParse(string? content, out CurrentSongDocument? document, out bool empty)
    {
        document = null;
        empty = string.IsNullOrWhiteSpace(content);

        if (empty)
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(content!);
            var root = json.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!root.TryGetProperty(OuterHashKey, out var hashElement)
                || hashElement.ValueKind != JsonValueKind.Object
                || !hashElement.TryGetProperty(HashBytesKey, out var hashBytes)
                || hashBytes.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            document = new CurrentSongDocument
            {
                Title = StringOrEmpty(root, "Name"),
                Artist = StringOrEmpty(root, "Artist"),
                Hash = hashBytes.GetString()!,
                Location = StringOrEmpty(root, "ActualLocation"),
            };

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string StringOrEmpty(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!
            : string.Empty;
}

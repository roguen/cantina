// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;

namespace Cantina.Barkeep.Library;

/// <summary>
/// The metadata block of a version-1 <c>.sng</c> archive — the single-file chart format
/// Geomitron Bridge writes and YARG reads.
///
/// Implemented against a real file, not a specification read: D-025 deliberately left this
/// unparsed until the first archive landed, and the first archive landed on 2026-08-29
/// (<c>Foo Fighters - Everlong (Hoph2o).sng</c>, downloaded through Geomitron Bridge's own
/// UI). Its layout: 6-byte magic <c>SNGPKG</c>, uint32 version (1), a 16-byte seed the
/// audio payloads are masked with, then a metadata section — uint64 byte length, uint64
/// pair count, and per pair an int32-length-prefixed UTF-8 key and value. The metadata
/// keys are the same vocabulary as <c>song.ini</c> (<c>name</c>, <c>artist</c>,
/// <c>charter</c>, <c>song_length</c>…), which is what lets one index treat both shapes
/// as songs.
///
/// Only the header and metadata are read. The file table and payloads that follow are
/// YARG's business; parsing stops at the metadata boundary, so a truncated or hostile
/// payload section cannot affect indexing.
/// </summary>
public sealed record SngDocument
{
    /// <summary>Everything before the metadata pairs: magic + version + seed + two uint64 lengths.</summary>
    private const int HeaderLength = 6 + 4 + 16 + 8 + 8;

    /// <summary>
    /// The real file's metadata is 717 bytes for 31 pairs. A megabyte is two orders of
    /// magnitude of headroom; anything past it is not metadata, whatever it claims.
    /// </summary>
    private const int MaximumMetadataBytes = 1024 * 1024;

    private const int MaximumPairs = 4096;

    private static readonly byte[] Magic = "SNGPKG"u8.ToArray();

    public required string Title { get; init; }

    public required string Artist { get; init; }

    public string Album { get; init; } = string.Empty;

    public string Genre { get; init; } = string.Empty;

    public string Year { get; init; } = string.Empty;

    public string Charter { get; init; } = string.Empty;

    public int? SongLengthMilliseconds { get; init; }

    /// <summary>
    /// Reads the metadata from the head of a <c>.sng</c> stream. A named reason comes back
    /// for every rejection, because an arrival that cannot be indexed must fail visibly
    /// (docs/geomitron-bridge-integration.md), never silently.
    /// </summary>
    public static bool TryRead(Stream stream, out SngDocument? document, out string reason)
    {
        document = null;

        Span<byte> header = stackalloc byte[HeaderLength];

        if (!TryFill(stream, header))
        {
            reason = "sng-truncated-header";
            return false;
        }

        if (!header[..6].SequenceEqual(Magic))
        {
            reason = "sng-bad-magic";
            return false;
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(header[6..10]);

        if (version != 1)
        {
            // A future version may move the metadata. Refusing by name beats parsing
            // garbage into a song title.
            reason = $"sng-unsupported-version-{version}";
            return false;
        }

        var metadataLength = BinaryPrimitives.ReadUInt64LittleEndian(header[26..34]);
        var pairCount = BinaryPrimitives.ReadUInt64LittleEndian(header[34..42]);

        if (metadataLength > MaximumMetadataBytes || pairCount > MaximumPairs)
        {
            reason = "sng-metadata-oversized";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var metadata = new byte[(int)metadataLength];

        if (!TryFill(stream, metadata))
        {
            reason = "sng-truncated-metadata";
            return false;
        }

        var offset = 0;

        for (ulong pair = 0; pair < pairCount; pair++)
        {
            if (!TryReadString(metadata, ref offset, out var key) ||
                !TryReadString(metadata, ref offset, out var value))
            {
                reason = "sng-malformed-metadata";
                return false;
            }

            values[key] = value;
        }

        // The same rule the ini parser applies: a chart without a usable name and artist
        // is skipped with a reason, never guessed at.
        if (!values.TryGetValue("name", out var title) || string.IsNullOrWhiteSpace(title) ||
            !values.TryGetValue("artist", out var artist) || string.IsNullOrWhiteSpace(artist))
        {
            reason = "sng-missing-name-or-artist";
            return false;
        }

        document = new SngDocument
        {
            Title = title.Trim(),
            Artist = artist.Trim(),
            Album = values.GetValueOrDefault("album", string.Empty).Trim(),
            Genre = values.GetValueOrDefault("genre", string.Empty).Trim(),
            Year = values.GetValueOrDefault("year", string.Empty).Trim(),
            Charter = values.GetValueOrDefault("charter", string.Empty).Trim(),
            SongLengthMilliseconds =
                values.TryGetValue("song_length", out var length) &&
                int.TryParse(length, out var milliseconds) && milliseconds > 0
                    ? milliseconds
                    : null,
        };
        reason = string.Empty;
        return true;
    }

    private static bool TryReadString(byte[] buffer, ref int offset, out string value)
    {
        value = string.Empty;

        if (offset + 4 > buffer.Length)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(offset, 4));
        offset += 4;

        // Long arithmetic, deliberately: a crafted length near int.MaxValue wraps
        // `offset + length` negative in int math, sails past the guard, and turns a
        // named rejection into a thrown exception - which would have stopped the host
        // at startup through the library scan. Found by review.
        if (length < 0 || offset + (long)length > buffer.Length)
        {
            return false;
        }

        value = Encoding.UTF8.GetString(buffer, offset, length);
        offset += length;
        return true;
    }

    private static bool TryFill(Stream stream, Span<byte> destination)
    {
        var filled = 0;

        while (filled < destination.Length)
        {
            var read = stream.Read(destination[filled..]);

            if (read == 0)
            {
                return false;
            }

            filled += read;
        }

        return true;
    }
}

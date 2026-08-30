// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Buffers.Binary;
using System.Text;
using Cantina.Barkeep.Library;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The .sng parser against the version-1 layout measured from the first real archive
/// (D-030): magic, version, seed, then length-prefixed metadata pairs. The builder here
/// reproduces that measured layout for synthetic cases; the acceptance run on the theater
/// PC parses the actual file.
/// </summary>
public sealed class SngDocumentTests
{
    /// <summary>Writes version-1 .sng bytes: header, metadata pairs, and a stub beyond.</summary>
    internal static byte[] Build(
        IReadOnlyList<(string Key, string Value)> pairs,
        uint version = 1,
        ulong? claimedMetadataLength = null,
        ulong? claimedPairCount = null)
    {
        var metadata = new MemoryStream();
        Span<byte> length = stackalloc byte[4];

        foreach (var (key, value) in pairs)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var valueBytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32LittleEndian(length, keyBytes.Length);
            metadata.Write(length);
            metadata.Write(keyBytes);
            BinaryPrimitives.WriteInt32LittleEndian(length, valueBytes.Length);
            metadata.Write(length);
            metadata.Write(valueBytes);
        }

        var output = new MemoryStream();
        output.Write("SNGPKG"u8);
        Span<byte> number = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(number[..4], version);
        output.Write(number[..4]);
        output.Write(new byte[16]);
        BinaryPrimitives.WriteUInt64LittleEndian(number, claimedMetadataLength ?? (ulong)metadata.Length);
        output.Write(number);
        BinaryPrimitives.WriteUInt64LittleEndian(number, claimedPairCount ?? (ulong)pairs.Count);
        output.Write(number);
        metadata.Position = 0;
        metadata.CopyTo(output);
        // Whatever follows the metadata is not the parser's business.
        output.Write("file-table-and-payloads"u8);
        return output.ToArray();
    }

    private static readonly (string, string)[] Everlong =
    [
        ("delay", "0"),
        ("name", "Everlong"),
        ("artist", "Foo Fighters"),
        ("album", "The Colour and the Shape"),
        ("genre", "Rock"),
        ("year", "1997"),
        ("charter", "Hoph2o"),
        ("song_length", "252673"),
    ];

    [Fact]
    public void ReadsTheMeasuredLayout()
    {
        using var stream = new MemoryStream(Build(Everlong));

        Assert.True(SngDocument.TryRead(stream, out var document, out _));
        Assert.NotNull(document);
        Assert.Equal("Everlong", document.Title);
        Assert.Equal("Foo Fighters", document.Artist);
        Assert.Equal("Hoph2o", document.Charter);
        Assert.Equal(252673, document.SongLengthMilliseconds);
    }

    [Fact]
    public void RefusesWhatItCannotVouchFor()
    {
        // Every rejection carries a name, because a skipped arrival must be visible.
        var cases = new (byte[] Bytes, string Reason)[]
        {
            (Build(Everlong, version: 2), "sng-unsupported-version-2"),
            (Build([("name", "X")]), "sng-missing-name-or-artist"),
            (Build(Everlong, claimedMetadataLength: 512UL * 1024 * 1024), "sng-metadata-oversized"),
            (Build(Everlong, claimedPairCount: 100), "sng-malformed-metadata"),
            // Long enough for a whole header read, so the magic is what gets judged; a
            // shorter garbage file honestly reports truncation instead.
            ("this is not an sng archive and it is long enough to prove it"u8.ToArray(), "sng-bad-magic"),
            ("SNG"u8.ToArray(), "sng-truncated-header"),
        };

        foreach (var (bytes, expected) in cases)
        {
            using var stream = new MemoryStream(bytes);
            Assert.False(SngDocument.TryRead(stream, out _, out var reason));
            Assert.Equal(expected, reason);
        }
    }

    [Fact]
    public void ATruncatedMetadataSectionIsNamedNotParsed()
    {
        var whole = Build(Everlong);
        using var stream = new MemoryStream(whole[..(whole.Length - 60)]);

        // The claimed metadata length runs past the truncation.
        Assert.False(SngDocument.TryRead(stream, out _, out var reason));
        Assert.Equal("sng-truncated-metadata", reason);
    }
}

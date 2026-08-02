// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Buffers.Binary;

namespace Cantina.Spikes.YargObserve;

/// <summary>
/// Scene reported at byte 6. This is the auto-advance signal: it states the score
/// screen directly, rather than inferring it from the lighting cue.
/// </summary>
internal enum YargScene : byte
{
    Unknown = 0,
    Menu = 1,
    Gameplay = 2,
    Score = 3,
    Calibration = 4,
    Practice = 5,
}

/// <summary>Beatline pulse at byte 38.</summary>
internal enum YargBeat : byte
{
    Off = 0,
    Measure = 1,
    Strong = 2,
    Weak = 3,
}

/// <summary>
/// Lighting cue at byte 34, as a <c>CueByte</c> ordinal.
/// These are NOT the spaced DMX channel values (NoCue 0, Menu 10, Score 20, ...).
/// Applying the DMX table to this byte yields plausible, wrong answers.
/// </summary>
internal enum YargCue : byte
{
    Default = 0,
    Dischord = 1,
    Chorus = 2,
    CoolManual = 3,
    Stomp = 4,
    Verse = 5,
    WarmManual = 6,
    BigRockEnding = 7,
    BlackoutFast = 8,
    BlackoutSlow = 9,
    BlackoutSpotlight = 10,
    CoolAutomatic = 11,
    FlareFast = 12,
    FlareSlow = 13,
    Frenzy = 14,
    Intro = 15,
    Harmony = 16,
    Silhouettes = 17,
    SilhouettesSpotlight = 18,
    Searchlights = 19,
    StrobeFastest = 20,
    StrobeFast = 21,
    StrobeMedium = 22,
    StrobeSlow = 23,
    StrobeOff = 24,
    Sweep = 25,
    WarmAutomatic = 26,
    KeyframeFirst = 27,
    KeyframeNext = 28,
    KeyframePrevious = 29,
    Menu = 30,
    Score = 31,
    NoCue = 32,
}

/// <summary>Per-player star power, present only when the datagram version is 4 or later.</summary>
internal readonly record struct YargStarPower(byte Amount, bool IsActive);

/// <summary>
/// Provisional reader for YARG's UDP data-stream datagram.
///
/// The layout is documented in <c>docs/yarg-interface.md</c> and was derived by reading
/// YALCY's LGPL parser, not by capturing packets. Confirming it against YARG 0.15 stable
/// is the entire point of this spike. Where a capture disagrees, the capture wins.
///
/// The type is deliberately free of I/O and of any Barkeep dependency so it can move into
/// a dependency-light parser project once the wire contract is fixed.
/// </summary>
internal sealed record YargDatagram
{
    /// <summary>Header magic, ASCII "YARG", read as a little-endian uint32.</summary>
    public const uint HeaderMagic = 0x59415247;

    /// <summary>Shortest datagram YALCY accepts: through <c>CameraCutSubject</c> at byte 46.</summary>
    public const int LegacyLength = 47;

    /// <summary>Fixed length once the star-power count is present.</summary>
    public const int StarPowerFixedLength = 49;

    /// <summary>First datagram version carrying the per-player star-power tail.</summary>
    public const byte StarPowerVersion = 4;

    private const int StarPowerEntryLength = 2;

    public required byte DatagramVersion { get; init; }
    public required byte Platform { get; init; }
    public required YargScene Scene { get; init; }
    public required bool Paused { get; init; }
    public required byte VenueSize { get; init; }
    public required float BeatsPerMinute { get; init; }
    public required byte SongSection { get; init; }
    public required byte GuitarNotes { get; init; }
    public required byte BassNotes { get; init; }
    public required byte DrumsNotes { get; init; }
    public required byte KeysNotes { get; init; }
    public required float VocalsNote { get; init; }
    public required float Harmony0Note { get; init; }
    public required float Harmony1Note { get; init; }
    public required float Harmony2Note { get; init; }
    public required YargCue LightingCue { get; init; }
    public required byte PostProcessing { get; init; }
    public required bool FogState { get; init; }
    public required byte StrobeState { get; init; }
    public required YargBeat Beat { get; init; }
    public required byte Keyframe { get; init; }
    public required bool BonusEffect { get; init; }
    public required bool AutoGen { get; init; }
    public required byte Spotlight { get; init; }
    public required byte Singalong { get; init; }
    public required byte CameraCutConstraint { get; init; }
    public required byte CameraCutPriority { get; init; }
    public required byte CameraCutSubject { get; init; }
    public required IReadOnlyList<YargStarPower> StarPower { get; init; }

    /// <summary>Byte count this datagram was expected to occupy, given its version and player count.</summary>
    public required int ExpectedLength { get; init; }

    /// <summary>
    /// Parses a datagram. Returns false with a reason rather than guessing: an unknown or
    /// truncated payload must be reported, never inferred.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> data, out YargDatagram? datagram, out string? rejection)
    {
        datagram = null;
        rejection = null;

        if (data.Length < LegacyLength)
        {
            rejection = $"short datagram: {data.Length} bytes, minimum {LegacyLength}";
            return false;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(data);
        if (magic != HeaderMagic)
        {
            rejection = $"header 0x{magic:X8}, expected 0x{HeaderMagic:X8}";
            return false;
        }

        var version = data[4];
        var starPower = Array.Empty<YargStarPower>();
        var expected = LegacyLength;

        if (version >= StarPowerVersion)
        {
            if (data.Length < StarPowerFixedLength)
            {
                rejection = $"version {version} needs {StarPowerFixedLength} bytes, got {data.Length}";
                return false;
            }

            var playerCount = BinaryPrimitives.ReadUInt16LittleEndian(data[47..]);
            expected = StarPowerFixedLength + (playerCount * StarPowerEntryLength);

            if (data.Length < expected)
            {
                rejection = $"{playerCount} players need {expected} bytes, got {data.Length}";
                return false;
            }

            var players = new YargStarPower[playerCount];
            for (var i = 0; i < playerCount; i++)
            {
                var offset = StarPowerFixedLength + (i * StarPowerEntryLength);
                players[i] = new YargStarPower(data[offset], data[offset + 1] != 0);
            }

            starPower = players;
        }

        datagram = new YargDatagram
        {
            DatagramVersion = version,
            Platform = data[5],
            Scene = (YargScene)data[6],
            Paused = data[7] != 0,
            VenueSize = data[8],
            BeatsPerMinute = BinaryPrimitives.ReadSingleLittleEndian(data[9..]),
            SongSection = data[13],
            GuitarNotes = data[14],
            BassNotes = data[15],
            DrumsNotes = data[16],
            KeysNotes = data[17],
            VocalsNote = BinaryPrimitives.ReadSingleLittleEndian(data[18..]),
            Harmony0Note = BinaryPrimitives.ReadSingleLittleEndian(data[22..]),
            Harmony1Note = BinaryPrimitives.ReadSingleLittleEndian(data[26..]),
            Harmony2Note = BinaryPrimitives.ReadSingleLittleEndian(data[30..]),
            LightingCue = (YargCue)data[34],
            PostProcessing = data[35],
            FogState = data[36] != 0,
            StrobeState = data[37],
            Beat = (YargBeat)data[38],
            Keyframe = data[39],
            BonusEffect = data[40] != 0,
            AutoGen = data[41] != 0,
            Spotlight = data[42],
            Singalong = data[43],
            CameraCutConstraint = data[44],
            CameraCutPriority = data[45],
            CameraCutSubject = data[46],
            StarPower = starPower,
            ExpectedLength = expected,
        };

        return true;
    }

    /// <summary>One-line summary of the fields this spike is trying to confirm.</summary>
    public string Describe() =>
        $"scene={Scene} paused={Paused} bpm={BeatsPerMinute:0.##} cue={LightingCue} " +
        $"beat={Beat} section={SongSection} players={StarPower.Count}";
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Globalization;

namespace Cantina.Spikes.YargObserve;

/// <summary>Command-line options, parsed without taking a dependency.</summary>
internal sealed record SpikeOptions
{
    public required int Port { get; init; }

    public required string YargDirectory { get; init; }

    public required int Seconds { get; init; }

    public required string? OutPath { get; init; }

    /// <summary>
    /// YARG stores per-channel state under Unity's LocalLow. Only the <c>release</c>
    /// channel exists on the theater PC today; a nightly install would use its own folder.
    /// </summary>
    public static string DefaultYargDirectory { get; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData",
            "LocalLow",
            "YARC",
            "YARG",
            "release");

    public static SpikeOptions? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var port = 36107;
        var directory = DefaultYargDirectory;
        var seconds = 0;
        string? outPath = null;

        for (var i = 0; i < args.Length; i++)
        {
            var hasValue = i + 1 < args.Length;
            switch (args[i])
            {
                case "--port" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var parsedPort):
                    port = parsedPort;
                    i++;
                    break;
                case "--yarg-dir" when hasValue:
                    directory = args[i + 1];
                    i++;
                    break;
                case "--seconds" when hasValue && int.TryParse(args[i + 1], CultureInfo.InvariantCulture, out var parsedSeconds):
                    seconds = parsedSeconds;
                    i++;
                    break;
                case "--out" when hasValue:
                    outPath = args[i + 1];
                    i++;
                    break;
                case "--help":
                case "-h":
                    return null;
                default:
                    Console.Error.WriteLine($"unrecognized or incomplete argument: {args[i]}");
                    return null;
            }
        }

        return new SpikeOptions
        {
            Port = port,
            YargDirectory = directory,
            Seconds = seconds,
            OutPath = outPath,
        };
    }

    public static void PrintUsage() =>
        Console.WriteLine("""
            cantina yarg-observe - spike for issue #2

            Listens for YARG's UDP data stream and watches currentSong.json, printing one
            correlated timeline. It observes only: it never sends on the port, writes to
            YARG's directory, or changes game settings.

              --port <n>         UDP port (default 36107)
              --yarg-dir <path>  YARG channel dir (default: LocalLow\YARC\YARG\release)
              --seconds <n>      stop after n seconds (default: run until Ctrl+C)
              --out <path>       append a transcript file for review before committing
              -h, --help         this message

            Exit code 0 if at least one datagram was accepted, 1 if none, 2 on bad usage.
            """);
}

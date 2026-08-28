// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg;

public sealed class YargSessionOptions
{
    public const string SectionName = "Yarg";

    /// <summary>
    /// Disabling skips the UDP listener and the file poller entirely. Integration tests
    /// use this so the pipeline under test receives only deterministic input (D-008).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>YARG's UDP data-stream port (docs/yarg-interface.md).</summary>
    public int Port { get; set; } = 36107;

    /// <summary>
    /// YARG's per-channel settings directory carrying currentSong.json. Empty selects
    /// the default under the user profile.
    /// </summary>
    public string YargDirectory { get; set; } = string.Empty;

    /// <summary>
    /// currentSong.json poll interval. The file clears ~86 ms after a scene change
    /// (D-010), so the poll must be comfortably inside that window to latch identity
    /// before it vanishes; 25 ms is the cadence the spikes proved.
    /// </summary>
    public int CurrentSongPollMilliseconds { get; set; } = 25;

    public string ResolveYargDirectory() =>
        string.IsNullOrWhiteSpace(YargDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "YARC", "YARG", "release")
            : YargDirectory;
}

// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg.Lifecycle;

/// <summary>
/// How Barkeep launches and restarts YARG (#23, decided 2026-09-13; D-038). The timings are
/// the ones measured on the theater PC that day, not guesses: window at 6.5 s, data stream
/// live at 11.3 s, library loaded and the scene reported at about 49 s.
/// </summary>
public sealed class YargProcessOptions
{
    public const string SectionName = "YargProcess";

    /// <summary>
    /// The YARG executable, named explicitly. Never discovered from YARC Launcher's private
    /// files, and never accepted from a client: the same boundary D-007 draws around
    /// Geomitron Bridge. Empty means launching is not configured and every launch refuses
    /// by name.
    /// </summary>
    public string ExecutablePath { get; set; } = string.Empty;

    /// <summary>Launch YARG when Barkeep starts, if it is not already running.</summary>
    public bool LaunchAtStartup { get; set; }

    /// <summary>
    /// After a fresh launch, press one Enter at the start menu to open the Music Library, so
    /// the theater is cue-ready without a walk to the PC. Safe only because a fresh process
    /// has no remembered cursor: it sits on QUICKPLAY, and Enter opens the library (measured
    /// by screenshot, 2026-09-13). If a dialog ever intervenes, the next cue fails by name.
    /// </summary>
    public bool OpenLibraryAfterLaunch { get; set; } = true;

    /// <summary>How long a launch may take to become observable and leave the loading scene.</summary>
    public int ReadyTimeoutMilliseconds { get; set; } = 150_000;

    /// <summary>A beat for the start menu to finish animating before the Enter.</summary>
    public int SettleMilliseconds { get; set; } = 2_000;

    /// <summary>How long a polite close gets before YARG is terminated.</summary>
    public int CloseTimeoutMilliseconds { get; set; } = 15_000;

    public int PollMilliseconds { get; set; } = 250;
}

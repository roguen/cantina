// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Acquisition;

public sealed class AcquisitionOptions
{
    public const string SectionName = "Acquisition";

    /// <summary>
    /// The directory Geomitron Bridge writes completed <c>.sng</c> downloads into — the
    /// verified filesystem handoff of D-007. Empty disables acquisition entirely, which is
    /// the default: Barkeep never watches a directory nobody named. The operator configures
    /// this to the same canonical directory Bridge's own UI names, and Barkeep never reads
    /// Bridge's settings to discover it (docs/geomitron-bridge-integration.md).
    /// </summary>
    public string WatchDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Largest .sng accepted. The real Everlong archive is 2.4 MB; charts with videos run
    /// to a few hundred MB. Anything past this is refused by name, not imported.
    /// </summary>
    public long MaximumSngBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Delay between the stability probes that decide a file has finished writing.</summary>
    public int StabilityProbeMilliseconds { get; set; } = 1500;

    /// <summary>
    /// How long to give YARG's Scan Songs before proceeding. The 2026-08-29 measurement
    /// rescanned 448 songs in well under this; the settle is deliberately generous because
    /// the scan has no wire signal to observe (D-015's menu blindness), so time is the only
    /// bound available.
    /// </summary>
    public int ScanSettleSeconds { get; set; } = 12;

    /// <summary>Periodic reconciliation sweep, covering watcher events missed while down.</summary>
    public int ReconcileMinutes { get; set; } = 10;

    /// <summary>
    /// Screen coordinates of the Music Library's MORE OPTIONS control and the SCAN SONGS
    /// entry in the popup it opens. Measured on the theater PC at 3840×2160 on 2026-08-29 —
    /// evidence, not constants, exactly like the search box coordinate (D-017): a YARG
    /// update that moves either breaks the refresh silently, and the cue's
    /// verify-by-outcome is what catches it.
    /// </summary>
    public int MoreOptionsX { get; set; } = 1340;

    public int MoreOptionsY { get; set; } = 2064;

    public int ScanSongsX { get; set; } = 1903;

    public int ScanSongsY { get; set; } = 939;
}

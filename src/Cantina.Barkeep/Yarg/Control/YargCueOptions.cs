// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg.Control;

public sealed class YargCueOptions
{
    public const string SectionName = "YargCue";

    /// <summary>
    /// Screen coordinate of the song-list search box. The defaults are the values
    /// measured on the theater PC at 3840×2160 (D-017) — evidence, not constants. The
    /// search field has no keyboard focus route, so a click is the only way in, and the
    /// verify-by-outcome loop is what catches a stale coordinate: the query lands
    /// nowhere, no song loads, and the cue reports it rather than claiming success.
    /// </summary>
    public int SearchBoxX { get; set; } = 1968;

    public int SearchBoxY { get; set; } = 161;
}

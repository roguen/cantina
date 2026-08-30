// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// The bench-testing surface. Off by default and invisible when off: the endpoints answer
/// 404, and the client never renders the section. When enabled, the operator can stand in
/// for the players' ready confirms at instrument setup — the one screen the product
/// otherwise never touches (D-015) — so a cue can be driven to gameplay from the iPad
/// with nobody holding an instrument.
/// </summary>
public sealed class DebugOptions
{
    public const string SectionName = "Debug";

    public bool Enabled { get; set; }

    /// <summary>
    /// Ready confirms to send, one per configured player. This theater has two profiles
    /// (measured during D-018 staging), and the acceptance cue suite's two confirms are
    /// the proven sequence this copies.
    /// </summary>
    public int PlayerConfirmations { get; set; } = 2;

    /// <summary>Pause before the first confirm — instrument setup needs a beat to render.</summary>
    public int ConfirmationLeadMilliseconds { get; set; } = 2000;

    /// <summary>Pause between confirms, the cue suite's measured cadence.</summary>
    public int ConfirmationSettleMilliseconds { get; set; } = 1500;
}

/// <summary>What the client learns about the debug surface: that it exists at all, and
/// how many players the stand-in will confirm for.</summary>
public sealed record DebugView(bool Enabled, int PlayerConfirmations);

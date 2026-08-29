// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// The synthetic-input seam. Every method is one proven primitive from the D-014/D-017
/// spikes; the cue service composes them and owns all policy. Implementations report what
/// Windows accepted — a sent event is never treated as a received one (D-015).
/// </summary>
public interface IYargActuator
{
    /// <summary>Windows processes named YARG. More than one poisons every oracle.</summary>
    int YargProcessCount();

    /// <summary>
    /// Whether synthetic input can reach YARG at all, and the named reason when it cannot.
    /// Null means the path is clear.
    ///
    /// This exists because the ways Windows blocks injected input are all **silent**:
    /// <c>SendInput</c> returns success, the event count comes back right, and nothing
    /// arrives. This project has already recorded one wrong conclusion from exactly that
    /// shape — every failed typing run had 100% acceptance, and the variable was elsewhere
    /// (D-017). A condition that cannot be observed after the fact has to be checked before
    /// it, so the cue can refuse by name rather than report a success nobody received.
    /// </summary>
    string? InputBlockedReason();

    /// <summary>
    /// Brings YARG to the foreground, verified by reading the foreground window rather
    /// than trusting the request (D-014's TryFocus). This is the explicit first step of a
    /// cue that docs/failure-behavior.md permits; it is never done silently.
    /// </summary>
    bool TryFocusYarg();

    /// <summary>Whether YARG owns the screen right now, and who does if not.</summary>
    (bool IsYargForeground, string Owner) ForegroundState();

    /// <summary>
    /// Clicks the configured search-box coordinate. The coordinate is evidence, not a
    /// constant (D-017): it depends on resolution and YARG's layout, and the verify-by-
    /// outcome loop is what catches it going stale.
    /// </summary>
    bool ClickSearchBox();

    /// <summary>Clears any leftover filter text. Stale text silently changes the match (D-017).</summary>
    bool ClearSearch();

    /// <summary>
    /// Types the query with virtual key + scan code — the shape a real keyboard produces.
    /// Returns false without typing anything when a character has no mapping, because a
    /// query that types differently from what was requested invalidates the selection.
    /// </summary>
    bool TypeQuery(string query);

    /// <summary>One Enter press.</summary>
    bool PressEnter();

    /// <summary>One Escape press. Pauses during gameplay (D-014).</summary>
    bool PressEscape();
}

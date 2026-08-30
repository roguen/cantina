// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Acquisition;

/// <summary>
/// Decides whether an arrival is a whole, contained, plausibly-sized <c>.sng</c> file —
/// the probe half of the verified filesystem handoff.
///
/// Stability means two things at once: the size stopped moving between probes, and the
/// file can be opened with no writer holding it. Geomitron Bridge stages downloads in its
/// private temp directory and moves them into the library, but a move across volumes is a
/// copy, and antivirus can hold a new file open — so neither signal alone is trusted
/// (docs/geomitron-bridge-integration.md, phase 1 step 5).
/// </summary>
public sealed class FileArrivalPort(IOptions<AcquisitionOptions> options) : ISongArrivalPort
{
    private readonly Dictionary<string, long> _lastLength = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<SongArrivalProbeResult> ProbeAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        // TrimEnd matters: Path.GetFullPath preserves a trailing separator, and a watch
        // directory pasted as "C:\songs\" would then fail BOTH containment prongs for
        // every legitimate file - acquisition entirely dead with the misleading reason
        // "arrival-escapes-watch-root". Found by review, verified on this machine.
        var root = Path.GetFullPath(settings.WatchDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(root, candidate.RelativePath));

        // Containment: the candidate must resolve to a file directly inside the watch
        // root. A relative path carrying traversal, a rooted name, or a separator resolves
        // elsewhere and is refused by name — it cannot merely be ignored, because a
        // rejected arrival must be visible (security-model.md).
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(full), root, StringComparison.OrdinalIgnoreCase))
        {
            return SongArrivalProbeResult.Rejected("arrival-escapes-watch-root");
        }

        if (!string.Equals(Path.GetExtension(full), ".sng", StringComparison.OrdinalIgnoreCase))
        {
            return SongArrivalProbeResult.Rejected("arrival-not-sng");
        }

        var info = new FileInfo(full);

        if (!info.Exists)
        {
            _lastLength.Remove(full);
            return SongArrivalProbeResult.Rejected("arrival-vanished");
        }

        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            _lastLength.Remove(full);
            return SongArrivalProbeResult.Rejected("arrival-is-reparse-point");
        }

        if (info.Length > settings.MaximumSngBytes)
        {
            // Every terminal path clears the baseline, or a singleton port leaks one
            // entry per path that ever failed to stabilize.
            _lastLength.Remove(full);
            return SongArrivalProbeResult.Rejected("arrival-oversized");
        }

        // Size must hold still across one probe interval...
        if (!_lastLength.TryGetValue(full, out var previous) || previous != info.Length)
        {
            _lastLength[full] = info.Length;
            await Task.Delay(settings.StabilityProbeMilliseconds, cancellationToken);
            return SongArrivalProbeResult.Stabilizing();
        }

        // ...and nothing may still hold the file for writing. FileShare.Read fails while a
        // writer has it open, which is exactly the signal wanted.
        try
        {
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            await Task.Delay(settings.StabilityProbeMilliseconds, cancellationToken);
            return SongArrivalProbeResult.Stabilizing();
        }

        _lastLength.Remove(full);
        return SongArrivalProbeResult.Ready();
    }
}

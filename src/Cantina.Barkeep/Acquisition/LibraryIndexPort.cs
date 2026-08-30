// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Library;
using Cantina.Barkeep.Yarg;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Acquisition;

/// <summary>
/// Validates an arrived <c>.sng</c> against the real format and folds it into the song
/// index. The identity handed back is the file's full path — the same join key everything
/// else uses (D-025): <c>currentSong.json</c> states where the loaded song lives, and for
/// an archive that is the archive's path.
/// </summary>
public sealed class LibraryIndexPort(
    SongIndex index,
    IOptions<AcquisitionOptions> acquisition,
    IOptions<LibraryOptions> library,
    IOptions<YargSessionOptions> yarg,
    TimeProvider clock) : ISongIndexPort
{
    public ValueTask<SongIndexResult> ValidateAndIndexAsync(
        SongArrivalCandidate candidate,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(acquisition.Value.WatchDirectory);
        var full = Path.GetFullPath(Path.Combine(root, candidate.RelativePath));

        SngDocument? document;
        string reason;

        try
        {
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (!SngDocument.TryRead(stream, out document, out reason) || document is null)
            {
                return ValueTask.FromResult(SongIndexResult.Rejected(reason));
            }
        }
        catch (IOException)
        {
            return ValueTask.FromResult(SongIndexResult.Rejected("sng-unreadable"));
        }

        // A full rescan rather than a single-entry insert, deliberately: the scan is the
        // one code path that decides what is a song (89 ms over this library, measured),
        // and a second insert path would be a second copy of that decision. The rescan
        // also picks up anything else that arrived while nobody was watching.
        index.Scan(library.Value.ResolveDirectories(yarg.Value.ResolveYargDirectory()), clock);

        var indexed = index.Search(document.Title, limit: 200)
            .Any(song => string.Equals(song.Location, full, StringComparison.OrdinalIgnoreCase));

        return ValueTask.FromResult(indexed
            ? SongIndexResult.Accepted(new SongIdentity(full))
            : SongIndexResult.Rejected("indexed-song-not-found-after-scan"));
    }
}

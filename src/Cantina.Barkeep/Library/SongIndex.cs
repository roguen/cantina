// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Library;

/// <summary>One indexed song. The folder path is the identity Cantina joins on (D-025).</summary>
public sealed record IndexedSong
{
    public required string Location { get; init; }

    public required string Title { get; init; }

    public required string Artist { get; init; }

    public required string Album { get; init; }

    public required string Genre { get; init; }

    public required string Year { get; init; }

    public required string Charter { get; init; }

    public int? SongLengthMilliseconds { get; init; }

    /// <summary>
    /// YARG's content hash, learned by observation the first time this song is seen
    /// loaded (D-025): Cantina never computes it, because the algorithm is YARG's
    /// private implementation detail.
    /// </summary>
    public string? LearnedHash { get; init; }
}

/// <summary>A folder the scan could not index, with the reason stated (M2 requirement).</summary>
public sealed record SkippedFolder(string Location, string Reason);

public sealed record ScanReport(
    int Indexed,
    IReadOnlyList<SkippedFolder> Skipped,
    double DurationMilliseconds,
    IReadOnlyList<string> DirectoriesScanned);

/// <summary>
/// The song library index: a scan of the same folders YARG reads, searchable across
/// title, artist, album, and charter. All in memory — 447 songs on the theater PC, and
/// honesty about failures matters more than scale machinery (D-025 rejected a database
/// for the same reason D-023 did).
/// </summary>
public sealed class SongIndex
{
    private readonly object _gate = new();
    private readonly Dictionary<string, IndexedSong> _songs = new(StringComparer.OrdinalIgnoreCase);
    private ScanReport _lastScan = new(0, [], 0, []);

    public ScanReport LastScan
    {
        get
        {
            lock (_gate)
            {
                return _lastScan;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _songs.Count;
            }
        }
    }

    /// <summary>
    /// Walks the directories for song folders. A song folder is one carrying a
    /// <c>song.ini</c> beside its note file; a folder that looks like a song but cannot
    /// be indexed is reported with its reason, never silently dropped — a count that
    /// quietly reads "covered everything" when it did not is the failure mode this
    /// project keeps finding elsewhere.
    /// </summary>
    public ScanReport Scan(IReadOnlyList<string> directories, TimeProvider clock)
    {
        var start = clock.GetTimestamp();
        var found = new Dictionary<string, IndexedSong>(StringComparer.OrdinalIgnoreCase);
        var skipped = new List<SkippedFolder>();
        var scanned = new List<string>();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                skipped.Add(new SkippedFolder(directory, "directory-missing"));
                continue;
            }

            scanned.Add(directory);

            foreach (var iniPath in Directory.EnumerateFiles(directory, "song.ini", SearchOption.AllDirectories))
            {
                var folder = Path.GetDirectoryName(iniPath)!;

                string content;

                try
                {
                    content = File.ReadAllText(iniPath);
                }
                catch (IOException)
                {
                    skipped.Add(new SkippedFolder(folder, "ini-unreadable"));
                    continue;
                }

                if (!SongIniDocument.TryParse(content, out var ini) || ini is null)
                {
                    skipped.Add(new SkippedFolder(folder, "ini-missing-name-or-artist"));
                    continue;
                }

                if (found.ContainsKey(folder))
                {
                    skipped.Add(new SkippedFolder(folder, "duplicate-location"));
                    continue;
                }

                found[folder] = new IndexedSong
                {
                    Location = folder,
                    Title = ini.Title,
                    Artist = ini.Artist,
                    Album = ini.Album,
                    Genre = ini.Genre,
                    Year = ini.Year,
                    Charter = ini.Charter,
                    SongLengthMilliseconds = ini.SongLengthMilliseconds,
                };
            }

            // .sng archives are the Geomitron Bridge handoff shape (D-007). Their parse
            // waited for a real file rather than a guessed format (D-025); the first one
            // landed 2026-08-29 and SngDocument was written against it (D-030). The
            // location an .sng is indexed under is the file path itself, which is what
            // currentSong.json states for an archive-loaded song, so the cue pipeline's
            // path join works unchanged.
            foreach (var sngPath in Directory.EnumerateFiles(directory, "*.sng", SearchOption.AllDirectories))
            {
                if (found.ContainsKey(sngPath))
                {
                    skipped.Add(new SkippedFolder(sngPath, "duplicate-location"));
                    continue;
                }

                SngDocument? sng;
                string reason;

                try
                {
                    using var stream = new FileStream(
                        sngPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                    if (!SngDocument.TryRead(stream, out sng, out reason) || sng is null)
                    {
                        skipped.Add(new SkippedFolder(sngPath, reason));
                        continue;
                    }
                }
                catch (IOException)
                {
                    // Mid-download, or locked by the writer. The acquisition watcher
                    // retries these; a plain rescan just reports the fact.
                    skipped.Add(new SkippedFolder(sngPath, "sng-unreadable"));
                    continue;
                }

                found[sngPath] = new IndexedSong
                {
                    Location = sngPath,
                    Title = sng.Title,
                    Artist = sng.Artist,
                    Album = sng.Album,
                    Genre = sng.Genre,
                    Year = sng.Year,
                    Charter = sng.Charter,
                    SongLengthMilliseconds = sng.SongLengthMilliseconds,
                };
            }
        }

        var report = new ScanReport(
            found.Count,
            skipped,
            clock.GetElapsedTime(start).TotalMilliseconds,
            scanned);

        lock (_gate)
        {
            // Carry learned hashes across rescans: observation is durable knowledge.
            foreach (var pair in _songs)
            {
                if (pair.Value.LearnedHash is { } hash && found.TryGetValue(pair.Key, out var fresh))
                {
                    found[pair.Key] = fresh with { LearnedHash = hash };
                }
            }

            _songs.Clear();

            foreach (var pair in found)
            {
                _songs[pair.Key] = pair.Value;
            }

            _lastScan = report;
        }

        return report;
    }

    /// <summary>The song indexed at exactly this location, if any — the D-025 join key as a lookup.</summary>
    public IndexedSong? FindByLocation(string location)
    {
        lock (_gate)
        {
            return _songs.GetValueOrDefault(location);
        }
    }

    /// <summary>
    /// Records the hash YARG stated for a loaded song. First observation wins; a
    /// different hash for the same folder replaces it, because the chart may genuinely
    /// have been re-downloaded.
    /// </summary>
    public void LearnHash(string location, string hash)
    {
        lock (_gate)
        {
            if (_songs.TryGetValue(location, out var song) && song.LearnedHash != hash)
            {
                _songs[location] = song with { LearnedHash = hash };
            }
        }
    }

    /// <summary>
    /// Case-insensitive search across title, artist, album, and charter, ranked so a
    /// title match outranks an artist match outranks the rest. Unlike YARG's own fuzzy
    /// search (D-017), matching is plain substring: predictable results are the point,
    /// because the cue pipeline verifies by outcome and surprises cost a whole song.
    /// </summary>
    public IReadOnlyList<IndexedSong> Search(string query, int limit = 50)
    {
        var trimmed = query.Trim();

        lock (_gate)
        {
            if (trimmed.Length == 0)
            {
                return [.. _songs.Values
                    .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
                    .Take(limit)];
            }

            return [.. _songs.Values
                .Select(song => (Song: song, Rank: Rank(song, trimmed)))
                .Where(entry => entry.Rank > 0)
                .OrderByDescending(entry => entry.Rank)
                .ThenBy(entry => entry.Song.Title, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .Select(entry => entry.Song)];
        }
    }

    private static int Rank(IndexedSong song, string query)
    {
        if (song.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if (song.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 4;
        }

        if (song.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        if (song.Album.Contains(query, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        return song.Charter.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
    }
}

/// <summary>The search endpoint's response: results plus the honesty of the scan behind them.</summary>
public sealed record SongSearchResponse(
    IReadOnlyList<IndexedSong> Results,
    int TotalIndexed,
    ScanReport LastScan);

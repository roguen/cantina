// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Library;

namespace Cantina.Barkeep.Tests;

public sealed class SongIniDocumentTests
{
    [Fact]
    public void ParsesTheRealWorldShape()
    {
        const string ini =
            """
            [song]
            name = Bad Company
            artist = Bad Company
            album = Bad Company
            genre = Rock
            year = 1974
            charter = Sygenysis + Nunchuck
            song_length = 287156
            diff_guitar = 3
            """;

        Assert.True(SongIniDocument.TryParse(ini, out var document));
        Assert.Equal("Bad Company", document!.Title);
        Assert.Equal("Sygenysis + Nunchuck", document.Charter);
        Assert.Equal(287156, document.SongLengthMilliseconds);
    }

    [Fact]
    public void SectionNameCaseAndCrlfDoNotMatter()
    {
        Assert.True(SongIniDocument.TryParse("[Song]\r\nname=A\r\nartist=B\r\n", out var document));
        Assert.Equal("A", document!.Title);
    }

    [Fact]
    public void MissingNameOrArtistIsARefusalNotAGuess()
    {
        Assert.False(SongIniDocument.TryParse("[song]\nname = Only A Title\n", out _));
        Assert.False(SongIniDocument.TryParse("name = No Section At All\nartist = X\n", out _));
    }
}

public sealed class SongIndexTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Temp leftovers are cleaned by the OS.
        }
    }

    private string AddSong(string folder, string title, string artist, string charter = "c")
    {
        var path = Path.Combine(_root, folder);
        Directory.CreateDirectory(path);
        File.WriteAllText(
            Path.Combine(path, "song.ini"),
            $"[song]\nname = {title}\nartist = {artist}\ncharter = {charter}\n");
        return path;
    }

    [Fact]
    public void ScansAndReportsSkipsWithReasons()
    {
        AddSong("good", "Detonation", "Trivium");
        Directory.CreateDirectory(Path.Combine(_root, "broken"));
        File.WriteAllText(Path.Combine(_root, "broken", "song.ini"), "[song]\nname = No Artist\n");
        // A garbage .sng is skipped with the parser's reason - D-025's placeholder
        // ("sng-metadata-not-yet-implemented") retired when D-030 implemented the format.
        File.WriteAllText(Path.Combine(_root, "archive.sng"), "not parsed");

        var index = new SongIndex();
        var report = index.Scan([_root], TimeProvider.System);

        Assert.Equal(1, report.Indexed);
        Assert.Contains(report.Skipped, s => s.Reason == "ini-missing-name-or-artist");
        Assert.Contains(report.Skipped, s => s.Reason == "sng-truncated-header");
    }

    [Fact]
    public void AMissingDirectoryIsANamedSkipNotAnError()
    {
        var index = new SongIndex();
        var report = index.Scan([Path.Combine(_root, "does-not-exist")], TimeProvider.System);

        Assert.Equal(0, report.Indexed);
        Assert.Contains(report.Skipped, s => s.Reason == "directory-missing");
    }

    [Fact]
    public void SearchRanksTitleOverArtistOverCharter()
    {
        AddSong("a", "Unforgiven Nights", "Nobody");
        AddSong("b", "Something Else", "The Unforgiven Band");
        AddSong("c", "Third Song", "Nobody", charter: "unforgiven-charts");

        var index = new SongIndex();
        index.Scan([_root], TimeProvider.System);

        var results = index.Search("unforgiven");

        Assert.Equal(3, results.Count);
        Assert.Equal("Unforgiven Nights", results[0].Title);
        Assert.Equal("The Unforgiven Band", results[1].Artist);
        Assert.Equal("unforgiven-charts", results[2].Charter);
    }

    [Fact]
    public void LearnedHashesSurviveARescan()
    {
        var location = AddSong("a", "Detonation", "Trivium");

        var index = new SongIndex();
        index.Scan([_root], TimeProvider.System);
        index.LearnHash(location, "learned-hash");
        index.Scan([_root], TimeProvider.System);

        var song = Assert.Single(index.Search("Detonation"));
        Assert.Equal("learned-hash", song.LearnedHash);
    }
}

// SPDX-License-Identifier: LGPL-3.0-or-later

using Cantina.Barkeep.Library;

namespace Cantina.Barkeep.Tests;

/// <summary>The starred-songs store: durable, idempotent, and honest about damage.</summary>
public sealed class FavoritesStoreTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public void StarsSurviveAReopen()
    {
        var directory = TempDirectory();

        var store = FavoritesStore.Open(directory);
        store.Set(@"C:\songs\a", favored: true);
        store.Set(@"C:\songs\b", favored: true);
        store.Set(@"C:\songs\a", favored: false);

        var reopened = FavoritesStore.Open(directory);

        Assert.Equal([@"C:\songs\b"], reopened.All);
    }

    [Fact]
    public void SettingTheSameStateTwiceIsANoOp()
    {
        var store = FavoritesStore.Open(TempDirectory());

        Assert.True(store.Set(@"C:\songs\a", favored: true));
        Assert.True(store.Set(@"C:\songs\a", favored: true));
        Assert.Single(store.All);
    }

    [Fact]
    public void ADamagedFileIsSetAsideAndTheStoreStartsEmpty()
    {
        var directory = TempDirectory();
        File.WriteAllText(Path.Combine(directory, "favorites.json"), "{ not json");

        var store = FavoritesStore.Open(directory);

        Assert.Empty(store.All);
        Assert.Single(Directory.GetFiles(directory, "favorites.json.damaged-*"));
    }
}

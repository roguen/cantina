// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Configuration;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The theater settings file (D-037): opt-in by name, beneath environment and command
/// line, and loud when the named file is missing.
/// </summary>
public sealed class TheaterConfigurationTests
{
    private static string WriteSettings(string json)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "theater.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void NothingLoadsUnlessTheFileIsNamed()
    {
        // The in-process test hosts run on the theater PC itself; a file that attached
        // itself would switch them into LAN mode with the real certificate.
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?> { ["Network:Mode"] = "Loopback" });

        Assert.Null(TheaterConfiguration.Attach(manager, manager));
        Assert.Equal("Loopback", manager["Network:Mode"]);
    }

    [Fact]
    public void TheNamedFileSuppliesTheTheaterSettings()
    {
        var path = WriteSettings("""{ "Network": { "Mode": "Lan", "Port": 80 } }""");
        var manager = new ConfigurationManager();
        manager.AddCommandLine([$"--{TheaterConfiguration.Key}={path}"]);

        Assert.Equal(Path.GetFullPath(path), TheaterConfiguration.Attach(manager, manager));
        Assert.Equal("Lan", manager["Network:Mode"]);
        Assert.Equal("80", manager["Network:Port"]);
    }

    [Fact]
    public void TheCommandLineStillOverridesTheFile()
    {
        // A one-off override at the prompt must beat the file, the same way it beats
        // appsettings.json.
        var path = WriteSettings("""{ "Network": { "Port": 80 } }""");
        var manager = new ConfigurationManager();
        manager.AddCommandLine([$"--{TheaterConfiguration.Key}={path}", "--Network:Port=5273"]);

        TheaterConfiguration.Attach(manager, manager);

        Assert.Equal("5273", manager["Network:Port"]);
    }

    [Fact]
    public void ANamedFileThatIsMissingFailsStartupByName()
    {
        var missing = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName(), "theater.json");
        var manager = new ConfigurationManager();
        manager.AddCommandLine([$"--{TheaterConfiguration.Key}={missing}"]);

        var error = Assert.Throws<FileNotFoundException>(() => TheaterConfiguration.Attach(manager, manager));

        Assert.Contains("does not exist", error.Message, StringComparison.Ordinal);
    }
}

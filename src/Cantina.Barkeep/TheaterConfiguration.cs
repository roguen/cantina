// SPDX-License-Identifier: LGPL-3.0-or-later

using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Cantina.Barkeep;

/// <summary>
/// The theater's own settings file, loaded only when asked for by name (D-037). Until
/// 2026-09-13 the theater ran from a process started with a dozen environment variables
/// in one shell; a reboot took it down and nothing remembered how to bring it back. The
/// settings now live in one JSON file the startup task points at with
/// <c>--TheaterConfig</c>.
///
/// Opt-in on purpose: the tests host Barkeep in-process on this same PC, and a file that
/// loaded itself would quietly switch every test run into LAN mode with the real
/// certificate. Nothing loads unless the key is set.
///
/// The file slots in beneath environment variables and the command line, so a one-off
/// override still wins over the file, the same precedence as appsettings.json.
/// </summary>
public static class TheaterConfiguration
{
    public const string Key = "TheaterConfig";

    /// <summary>
    /// Attaches the file named by <see cref="Key"/>, if any. Returns the attached path, or
    /// null when no file was asked for. A named file that does not exist fails startup by
    /// name: a theater that silently booted in loopback mode would look healthy and be
    /// unreachable.
    /// </summary>
    public static string? Attach(IConfigurationBuilder configuration, IConfiguration current)
    {
        var path = current[Key];

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var full = Path.GetFullPath(path);

        if (!File.Exists(full))
        {
            throw new FileNotFoundException(
                $"{Key} names a theater settings file that does not exist: {full}", full);
        }

        var source = new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
        {
            Path = full,
            Optional = false,
            ReloadOnChange = false,
        };
        source.ResolveFileProvider();

        var sources = configuration.Sources;
        var overrides = sources
            .Select((candidate, index) => (candidate, index))
            .FirstOrDefault(pair => pair.candidate is EnvironmentVariablesConfigurationSource
                or CommandLineConfigurationSource);

        if (overrides.candidate is null)
        {
            sources.Add(source);
        }
        else
        {
            sources.Insert(overrides.index, source);
        }

        return full;
    }
}

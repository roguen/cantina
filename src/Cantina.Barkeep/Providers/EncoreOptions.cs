// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Providers;

/// <summary>
/// The Chorus Encore integration (D-032). Encore publishes no API terms; the posture that
/// substitutes for them is written into these defaults and into docs/chart-provider.md:
/// the client identifies itself, searches at a walking pace, downloads only what a person
/// asked for, one at a time, and can be turned off with one setting. The provider is a
/// donation-funded community service whose stated top cost is bandwidth — Cantina behaves
/// like a guest who knows that.
/// </summary>
public sealed class EncoreOptions
{
    public const string SectionName = "Encore";

    public bool Enabled { get; set; } = true;

    public string ApiBaseUrl { get; set; } = "https://api.enchor.us";

    public string FilesBaseUrl { get; set; } = "https://files.enchor.us";

    /// <summary>Results per search — a screenful, not a scrape.</summary>
    public int PerPage { get; set; } = 25;

    /// <summary>Minimum interval between outbound searches, enforced server-side.</summary>
    public int SearchCooldownMilliseconds { get; set; } = 1000;

    /// <summary>Downloads allowed per rolling hour. Bench use never approaches this.</summary>
    public int DownloadsPerHour { get; set; } = 30;

    /// <summary>
    /// Where a download lands before it is complete and validated. Kept outside the
    /// acquisition watch directory so the watcher never sees a half-written file under a
    /// name it would try to import. Empty resolves beside the journal under ProgramData.
    /// </summary>
    public string StagingDirectory { get; set; } = string.Empty;

    public string ResolveStagingDirectory() =>
        StagingDirectory.Length > 0
            ? StagingDirectory
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Cantina", "staging");
}

/// <summary>What the client learns about the provider surface: that it exists.</summary>
public sealed record ProviderView(bool Enabled);

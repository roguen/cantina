// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Setlist;

public sealed class SetlistOptions
{
    public const string SectionName = "Setlist";

    /// <summary>
    /// Where the journal and snapshot live. Empty selects Barkeep's own data directory
    /// under local application data — never anywhere near YARG's files (D-023).
    /// </summary>
    public string DataDirectory { get; set; } = string.Empty;

    public string ResolveDataDirectory() =>
        string.IsNullOrWhiteSpace(DataDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Cantina", "Barkeep")
            : DataDirectory;
}

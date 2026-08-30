// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Providers;

/// <summary>What the provider integration says when the network lets it down.</summary>
public static partial class ProviderLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "The Encore download for {Md5} failed; the iPad was told the same sentence.")]
    public static partial void DownloadFailed(ILogger logger, string md5, Exception error);
}

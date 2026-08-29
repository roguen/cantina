// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Network;

/// <summary>
/// What Barkeep says about the certificate it is serving over its life, as distinct from
/// what it says at startup. A renewal landing is the one event here that means the design
/// is working, and a failed reload is the one that means it is not.
/// </summary>
public static partial class NetworkLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Certificate reloaded for {Subject}, now valid until {NotAfter}. A renewal has landed.")]
    public static partial void CertificateReloaded(ILogger logger, string subject, DateTimeOffset notAfter);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Certificate file changed but could not be loaded: {Reason}. Still serving the previous one; will retry.")]
    public static partial void CertificateReloadFailed(ILogger logger, string reason);
}

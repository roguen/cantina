// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Access;

/// <summary>
/// What Barkeep says about its own binding, and deliberately nothing about the requests it
/// serves. There is no request log here on purpose: a log line naming a token, a ticket, a
/// song path, or a device's address is a disclosure that outlives the session it describes.
///
/// The pairing code is the one secret that is logged, because the console is where the
/// operator reads it — and it is short-lived, single-use, and useless off this machine.
/// </summary>
public static partial class AccessLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Barkeep is on the LAN at https://{Address}:{Port}.")]
    public static partial void LanBinding(ILogger logger, string address, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "iPad onboarding: http://{Address}:{Port}/onboarding")]
    public static partial void Onboarding(ILogger logger, string address, int port);

    [LoggerMessage(Level = LogLevel.Information, Message = "Theater authority fingerprint {Fingerprint}. Compare this on the iPad before trusting the profile.")]
    public static partial void AuthorityFingerprint(ILogger logger, string fingerprint);

    [LoggerMessage(Level = LogLevel.Information, Message = "Inbound firewall rule, if the iPad cannot connect. Barkeep does not run it: {Command}")]
    public static partial void FirewallRule(ILogger logger, string command);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pairing code {Code}, valid until {ExpiresAt}. It is shown here and nowhere else.")]
    public static partial void PairingCode(ILogger logger, string code, DateTimeOffset expiresAt);

    [LoggerMessage(Level = LogLevel.Information, Message = "No device is paired yet.")]
    public static partial void NoDevicePaired(ILogger logger);
}

// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Access;

/// <summary>A device the operator paired, as anyone but the device itself may see it.</summary>
public sealed record PairedDevice(
    string DeviceId,
    string Label,
    DateTimeOffset PairedAt,
    DateTimeOffset? LastSeenAt);

/// <summary>The one moment a token exists in plaintext: the response to a successful pairing.</summary>
public sealed record PairingGrant(string DeviceId, string Label, string Token, DateTimeOffset PairedAt);

/// <summary>What the operator sees when a pairing window opens. The code never leaves this host by any other route.</summary>
public sealed record PairingWindowState(string Code, DateTimeOffset ExpiresAt, int AttemptsRemaining);

/// <summary>Why a pairing attempt did not produce a token. Every reason is named; none is "invalid request".</summary>
public enum PairingResult
{
    Accepted,
    NoWindowOpen,
    Expired,
    WrongCode,
    TooManyAttempts,
}

/// <summary>What the iPad is told before it trusts anything: names, ports, and the fingerprint to compare.</summary>
public sealed record OnboardingDescription(
    string Service,
    string TheaterName,
    string SecureUrl,
    IReadOnlyList<string> HostNames,
    string CertificateUrl,
    string CertificateFingerprint,
    bool NeedsDeviceTrust,
    bool Paired);

/// <summary>What an unpaired device sends: the code the operator read off the theater PC.</summary>
public sealed record PairingClaim(string? Code, string? Label);

/// <summary>Why pairing was refused, named rather than described.</summary>
public sealed record PairingRefused(PairingResult Reason);

/// <summary>A one-connection credential for the live socket.</summary>
public sealed record LiveTicket(string Ticket, DateTimeOffset ExpiresAt);

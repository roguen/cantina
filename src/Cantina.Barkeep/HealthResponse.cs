// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep;

public sealed record HealthResponse(string Status, string Service, CertificateHealth? Certificate);

/// <summary>
/// What Barkeep can say about the certificate it is serving, and how long it has left.
///
/// This exists because of the one way a publicly trusted certificate is *worse* than the
/// private theater authority: renewal is done by machinery Barkeep does not own and cannot
/// see, so a renewal that quietly stopped presents as a theater that works perfectly until
/// a day weeks later when nothing connects and nobody knows why (D-029).
///
/// The private authority has the opposite shape — a ten-year anchor and a leaf Barkeep
/// reissues itself — so this is reported for both and alarming for one.
///
/// Null when Barkeep is loopback-only, which serves no TLS at all.
/// </summary>
public sealed record CertificateHealth(
    string Source,
    bool NeedsDeviceTrust,
    DateTimeOffset NotAfter,
    int DaysRemaining,
    string Status);

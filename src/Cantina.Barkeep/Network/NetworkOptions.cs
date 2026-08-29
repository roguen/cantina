// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Network;

/// <summary>How far Barkeep listens. Loopback is the default and always will be (D-026).</summary>
public enum BarkeepBinding
{
    /// <summary>Loopback only. Nothing reaches Barkeep that is not already running on this host.</summary>
    Loopback,

    /// <summary>Loopback, plus exactly one explicit LAN interface address over TLS.</summary>
    Lan,
}

public sealed class NetworkOptions
{
    public const string SectionName = "Network";

    public BarkeepBinding Mode { get; set; } = BarkeepBinding.Loopback;

    /// <summary>
    /// The one interface address the LAN listeners bind. Empty asks for the address of the
    /// interface that holds the default IPv4 gateway. Never <c>IPAddress.Any</c>: the
    /// theater PC also carries a Tailscale interface, and <c>Any</c> would publish the
    /// control surface to a different network than the one the operator is standing on
    /// (D-026).
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// The plain-HTTP port. On loopback it serves everything, as it always has. On the LAN
    /// it serves the onboarding surface only — the certificate and the trust instructions —
    /// and redirects the rest to <see cref="SecurePort"/>.
    /// </summary>
    public int Port { get; set; } = 5273;

    /// <summary>The TLS port. On the LAN the whole control surface lives here and nowhere else.</summary>
    public int SecurePort { get; set; } = 5274;

    /// <summary>
    /// Extra names to carry in the certificate and accept in the Host header. The machine
    /// name and its <c>.local</c> form are included without being listed here.
    /// </summary>
    public IList<string> HostNames { get; } = [];

    /// <summary>
    /// Extra browser origins to accept. The Vite dev server is one; nothing else should be.
    /// </summary>
    public IList<string> AdditionalOrigins { get; } = [];

    /// <summary>
    /// Leaf certificate lifetime. Apple rejects a TLS server certificate whose validity
    /// exceeds 398 days, so this stays below that and rotation is a designed event rather
    /// than an outage (D-026).
    /// </summary>
    public int LeafCertificateDays { get; set; } = 397;

    /// <summary>Where the certificate authority and device registry live. Empty selects the setlist data directory.</summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>
    /// A certificate supplied from outside — a PKCS#12 file holding the server certificate,
    /// its key, and any intermediates. When this is set Barkeep serves it and **never
    /// creates a theater authority at all**: there is nothing for the iPad to install,
    /// because a publicly trusted certificate is already trusted (D-029).
    ///
    /// Empty falls back to the private theater authority, which is what a site with no
    /// domain name, no internet, or no wish to run an ACME client still gets.
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>Password for <see cref="CertificatePath"/>. Empty means the file has none.</summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// How close to expiry the certificate has to be before Barkeep calls it a problem.
    /// A publicly trusted certificate is renewed by machinery Barkeep does not own and
    /// cannot see, so the failure mode is a renewal that quietly stopped and a theater that
    /// breaks weeks later. This is the tripwire for that (D-029).
    /// </summary>
    public int CertificateWarnDays { get; set; } = 21;
}

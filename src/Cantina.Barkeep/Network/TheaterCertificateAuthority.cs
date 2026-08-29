// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Cantina.Barkeep.Network;

/// <summary>
/// The certificate a LAN binding serves, and where it came from.
///
/// <see cref="Authority"/> is null when the certificate was supplied from outside and is
/// already publicly trusted. That null is the whole difference the iPad feels: an authority
/// means a profile to install and a fingerprint to compare, and no authority means the
/// device just connects (D-029).
/// </summary>
public sealed record TheaterCertificates(
    X509Certificate2 Server,
    X509Certificate2? Authority,
    bool AuthorityCreated,
    bool ServerIssued,
    string? AuthorityFingerprint,
    string? AuthorityFilePath,
    X509Certificate2Collection? Chain = null)
{
    /// <summary>True when the iPad has to be taught to trust this, false when the world already does.</summary>
    public bool NeedsDeviceTrust => Authority is not null;

    public DateTimeOffset NotAfter => Server.NotAfter.ToUniversalTime();

    public int DaysUntilExpiry(DateTimeOffset now) => (int)Math.Floor((NotAfter - now).TotalDays);
}

/// <summary>
/// A private certificate authority for one theater, and the server certificate it signs.
///
/// Two certificates rather than one self-signed leaf, for a reason that shows up on the
/// iPad: a trust anchor installed on the device once can keep signing new server
/// certificates, so rotating the server certificate — annually, or the moment DHCP hands
/// this host a different address — never asks the operator to touch the iPad again. A
/// self-signed leaf would (D-026).
///
/// The private keys are files in Barkeep's data directory under the operator's profile,
/// protected by the profile ACL and nothing more. That is exactly the protection the
/// setlist journal already has on this single-user host, and it is stated here rather than
/// implied: anyone who can read that directory can impersonate the theater to the iPad.
/// </summary>
public static class TheaterCertificateAuthority
{
    public const string AuthorityFileName = "theater-ca.pfx";
    public const string ServerFileName = "theater-server.pfx";
    public const string AuthorityPublicFileName = "cantina-theater-ca.cer";

    private const string AuthoritySubject = "CN=Cantina Theater CA, O=Cantina";
    private const string ServerSubject = "CN=Barkeep, O=Cantina";
    private static readonly TimeSpan AuthorityLifetime = TimeSpan.FromDays(3650);
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromDays(30);

    /// <summary>
    /// Load or create the authority, then load or re-issue the server certificate. The
    /// server certificate is re-issued when it is missing, near expiry, or no longer names
    /// every address and host the binding actually uses — the last of which is what a DHCP
    /// address change looks like from in here.
    /// </summary>
    public static TheaterCertificates Ensure(
        string directory,
        IReadOnlyList<string> hostNames,
        IReadOnlyList<IPAddress> addresses,
        int serverDays,
        DateTimeOffset now)
    {
        Directory.CreateDirectory(directory);

        var authorityPath = Path.Combine(directory, AuthorityFileName);
        var serverPath = Path.Combine(directory, ServerFileName);

        var authority = Load(authorityPath);
        var authorityCreated = false;

        if (authority is null || authority.NotAfter.ToUniversalTime() <= now.UtcDateTime.Add(RenewalMargin))
        {
            authority?.Dispose();
            authority = CreateAuthority(now);
            Save(authorityPath, authority);
            authorityCreated = true;
        }

        var publicPath = Path.Combine(directory, AuthorityPublicFileName);
        WriteBytes(publicPath, authority.Export(X509ContentType.Cert));

        var server = Load(serverPath);
        var serverIssued = false;

        if (server is null || authorityCreated || !Covers(server, now, hostNames, addresses))
        {
            server?.Dispose();
            server = IssueServer(authority, hostNames, addresses, serverDays, now);
            Save(serverPath, server);
            serverIssued = true;
        }

        return new TheaterCertificates(
            server,
            authority,
            authorityCreated,
            serverIssued,
            Fingerprint(authority),
            publicPath,
            Chain: null);
    }

    /// <summary>
    /// Load a certificate somebody else issued — in practice a Let's Encrypt leaf delivered
    /// by the site's existing ACME machinery. Barkeep does no ACME of its own on purpose:
    /// issuing and renewing is a job the network already does for other services, and
    /// duplicating it here would mean a second place holding a DNS credential.
    ///
    /// Failing to load is fatal rather than a silent fall back to the private authority. A
    /// server that quietly serves a different certificate than the operator configured is a
    /// server whose clients fail in a way nobody can explain.
    /// </summary>
    public static TheaterCertificates LoadSupplied(string path, string? password, string? keyPath = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Network:CertificatePath names '{path}', which does not exist. " +
                "Clear the setting to fall back to the theater authority, or fix the path.",
                path);
        }

        return string.IsNullOrWhiteSpace(keyPath)
            ? FromPkcs12(path, password)
            : FromPem(path, keyPath);
    }

    private static TheaterCertificates FromPkcs12(string path, string? password)
    {
        var server = X509CertificateLoader.LoadPkcs12(
            File.ReadAllBytes(path),
            string.IsNullOrEmpty(password) ? null : password,
            X509KeyStorageFlags.Exportable);

        return Supplied(server, chain: null, path);
    }

    /// <summary>
    /// The shape an ACME client actually writes: a PEM chain file and a PEM key beside it.
    ///
    /// Two things have to happen that are easy to miss. The **intermediates must be sent** —
    /// a client that does not already hold Let's Encrypt's intermediate cannot build a path
    /// to the root, and serving the leaf alone works on whichever machine you happened to
    /// test on and fails elsewhere. And the key that <c>CreateFromPemFile</c> produces is
    /// **ephemeral**, which Windows will not use for a TLS server at all, so it goes through
    /// the same PKCS#12 round trip the generated certificates use.
    /// </summary>
    private static TheaterCertificates FromPem(string certificatePath, string keyPath)
    {
        if (!File.Exists(keyPath))
        {
            throw new FileNotFoundException(
                $"Network:CertificateKeyPath names '{keyPath}', which does not exist.",
                keyPath);
        }

        using var keyed = X509Certificate2.CreateFromPemFile(certificatePath, keyPath);

        var pem = new X509Certificate2Collection();
        pem.ImportFromPemFile(certificatePath);

        var chain = new X509Certificate2Collection();

        // Everything after the leaf is the chain to send. A file holding only the leaf is
        // legal and leaves this empty, which is why the option's documentation says to point
        // at the full chain.
        for (var index = 1; index < pem.Count; index++)
        {
            chain.Add(pem[index]);
        }

        return Supplied(Persistable(keyed), chain.Count > 0 ? chain : null, certificatePath);
    }

    private static TheaterCertificates Supplied(X509Certificate2 server, X509Certificate2Collection? chain, string path)
    {
        if (!server.HasPrivateKey)
        {
            throw new InvalidOperationException(
                $"The certificate at '{path}' carries no private key, so it cannot serve TLS.");
        }

        return new TheaterCertificates(
            server,
            Authority: null,
            AuthorityCreated: false,
            ServerIssued: false,
            AuthorityFingerprint: null,
            AuthorityFilePath: null,
            chain);
    }

    /// <summary>The SHA-256 fingerprint an operator compares on the iPad before trusting the profile.</summary>
    public static string Fingerprint(X509Certificate2 certificate) =>
        Convert.ToHexString(SHA256.HashData(certificate.RawData))
            .Chunk(2)
            .Select(pair => new string(pair))
            .Aggregate((left, right) => left + ":" + right);

    private static bool Covers(
        X509Certificate2 certificate,
        DateTimeOffset now,
        IReadOnlyList<string> hostNames,
        IReadOnlyList<IPAddress> addresses)
    {
        if (certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime.Add(RenewalMargin) ||
            certificate.NotBefore.ToUniversalTime() > now.UtcDateTime)
        {
            return false;
        }

        var extension = certificate.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault();

        if (extension is null)
        {
            return false;
        }

        var dns = extension.EnumerateDnsNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var ips = extension.EnumerateIPAddresses().ToHashSet();

        return hostNames.All(dns.Contains) && addresses.All(ips.Contains);
    }

    private static X509Certificate2 CreateAuthority(DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(AuthoritySubject, key, HashAlgorithmName.SHA256);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        using var created = request.CreateSelfSigned(now.AddMinutes(-5), now.Add(AuthorityLifetime));
        return Persistable(created);
    }

    private static X509Certificate2 IssueServer(
        X509Certificate2 authority,
        IReadOnlyList<string> hostNames,
        IReadOnlyList<IPAddress> addresses,
        int serverDays,
        DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(ServerSubject, key, HashAlgorithmName.SHA256);

        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();

        foreach (var name in hostNames)
        {
            subjectAlternativeNames.AddDnsName(name);
        }

        foreach (var address in addresses)
        {
            subjectAlternativeNames.AddIpAddress(address);
        }

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(
            X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, includeKeyIdentifier: true, includeIssuerAndSerial: false));
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());

        // Apple rejects a server certificate valid for more than 398 days, so the caller's
        // day count is clamped rather than trusted.
        var lifetime = TimeSpan.FromDays(Math.Clamp(serverDays, 1, 397));
        var notBefore = now.AddMinutes(-5);
        var notAfter = now.Add(lifetime);

        if (notAfter > authority.NotAfter.ToUniversalTime())
        {
            notAfter = authority.NotAfter.ToUniversalTime();
        }

        using var signed = request.Create(authority, notBefore, notAfter, SerialNumber());
        using var withKey = signed.CopyWithPrivateKey(key);
        return Persistable(withKey);
    }

    private static byte[] SerialNumber()
    {
        var serial = RandomNumberGenerator.GetBytes(16);
        serial[0] &= 0x7F;
        return serial;
    }

    private static X509Certificate2? Load(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(path),
                password: null,
                X509KeyStorageFlags.Exportable);
        }
        catch (CryptographicException)
        {
            // An unreadable key file is replaced, not fatal. Pairing survives it: a device
            // token is bound to the registry, not to the certificate.
            return null;
        }
    }

    private static void Save(string path, X509Certificate2 certificate) =>
        WriteBytes(path, certificate.Export(X509ContentType.Pkcs12));

    private static void WriteBytes(string path, byte[] bytes)
    {
        var temp = path + ".tmp";

        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }

        File.Move(temp, path, overwrite: true);
    }

    /// <summary>
    /// A freshly built certificate carries an ephemeral key that Windows will not use for
    /// TLS. The PKCS#12 round trip is what makes it serviceable.
    /// </summary>
    private static X509Certificate2 Persistable(X509Certificate2 certificate) =>
        X509CertificateLoader.LoadPkcs12(
            certificate.Export(X509ContentType.Pkcs12),
            password: null,
            X509KeyStorageFlags.Exportable);
}

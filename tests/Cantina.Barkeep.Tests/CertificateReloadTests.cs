// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Cantina.Barkeep.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The renewal path (D-029). Kestrel reads its certificate once at startup, so without this
/// a delivered renewal would change nothing until a restart — which is to say, until the old
/// certificate expired and the theater stopped working.
/// </summary>
public sealed class CertificateReloadTests
{
    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>Writes a PEM chain and key the way an ACME client delivers them.</summary>
    private static (string Chain, string Key, string Thumbprint) Deliver(string directory, string name, int days)
    {
        var issued = TheaterCertificateAuthority.Ensure(
            Path.Combine(directory, name), [name], [IPAddress.Loopback], days, DateTimeOffset.UtcNow);

        var chain = Path.Combine(directory, "fullchain.cer");
        File.WriteAllText(chain,
            issued.Server.ExportCertificatePem() + Environment.NewLine +
            issued.Authority!.ExportCertificatePem() + Environment.NewLine);

        var key = Path.Combine(directory, "server.key");
        File.WriteAllText(key, issued.Server.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());

        return (chain, key, issued.Server.Thumbprint);
    }

    private static TheaterCertificateSource Source(string chain, string key)
    {
        var options = new NetworkOptions { CertificatePath = chain, CertificateKeyPath = key };
        var loaded = TheaterCertificateAuthority.LoadSupplied(chain, null, key);
        return new TheaterCertificateSource(loaded, options, NullLogger.Instance);
    }

    [Fact]
    public void ADeliveredRenewalIsPickedUpWithoutARestart()
    {
        var directory = TempDirectory();
        var first = Deliver(directory, "cantina.aero4ge.com", 90);

        using var source = Source(first.Chain, first.Key);
        Assert.Equal(first.Thumbprint, source.Current.Server.Thumbprint);
        Assert.True(source.Watching);

        // Nothing has changed, so nothing reloads.
        Assert.False(source.RefreshIfChanged());

        // The issuer renews and delivers over the same paths.
        var second = Deliver(directory, "renewed.aero4ge.com", 120);
        Assert.NotEqual(first.Thumbprint, second.Thumbprint);

        Assert.True(source.RefreshIfChanged());
        Assert.Equal(second.Thumbprint, source.Current.Server.Thumbprint);
    }

    [Fact]
    public void AHalfWrittenFileKeepsTheCertificateThatWorks()
    {
        var directory = TempDirectory();
        var good = Deliver(directory, "cantina.aero4ge.com", 90);

        using var source = Source(good.Chain, good.Key);

        // scp is not atomic, so a poll can catch the file mid-copy. Dropping the live
        // certificate because a copy was in flight would turn a renewal into an outage.
        File.WriteAllText(good.Chain, "-----BEGIN CERTIFICATE-----\nnot base64 at all\n");

        Assert.False(source.RefreshIfChanged());
        Assert.Equal(good.Thumbprint, source.Current.Server.Thumbprint);

        // And it recovers when the copy finishes, rather than needing a restart.
        var finished = Deliver(directory, "finished.aero4ge.com", 90);
        Assert.True(source.RefreshIfChanged());
        Assert.Equal(finished.Thumbprint, source.Current.Server.Thumbprint);
    }

    [Fact]
    public void TheTheaterAuthorityIsNotWatched()
    {
        // It reissues its own leaf at startup and has a ten-year anchor, so there is nothing
        // arriving from outside to notice. Claiming to watch it would be a claim with no
        // mechanism behind it.
        var directory = TempDirectory();
        var issued = TheaterCertificateAuthority.Ensure(
            directory, ["localhost"], [IPAddress.Loopback], 397, DateTimeOffset.UtcNow);

        using var source = new TheaterCertificateSource(issued, new NetworkOptions(), NullLogger.Instance);

        Assert.False(source.Watching);
        Assert.False(source.RefreshIfChanged());
        Assert.True(source.Current.NeedsDeviceTrust);
    }
}

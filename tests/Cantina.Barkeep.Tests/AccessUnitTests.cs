// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Security.Cryptography.X509Certificates;
using Cantina.Barkeep.Access;
using Cantina.Barkeep.Network;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The parts of D-026 that are decisions rather than plumbing: what the registry stores,
/// what the pairing window refuses, what a ticket is worth twice, what the certificate
/// covers, and what the binding resolves to.
/// </summary>
public sealed class AccessUnitTests
{
    private static string TempDirectory() =>
        Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());

    private static NetworkOptions LanOptions(string address) =>
        new() { Mode = BarkeepBinding.Lan, Address = address, Port = 5273, SecurePort = 5274 };

    [Fact]
    public void TheRegistryStoresNoUsableCredential()
    {
        var directory = TempDirectory();
        var registry = DeviceRegistry.Open(directory);
        var grant = registry.Grant("iPad", DateTimeOffset.UtcNow);

        var stored = File.ReadAllText(Path.Combine(directory, "paired-devices.json"));

        // The token was handed out once. Anyone reading the file afterwards finds a hash.
        Assert.DoesNotContain(grant.Token, stored, StringComparison.Ordinal);
        Assert.Contains("tokenHash", stored, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void APairedTokenSurvivesARestartAndARevokedOneDoesNot()
    {
        var directory = TempDirectory();
        var first = DeviceRegistry.Open(directory);
        var keep = first.Grant("iPad", DateTimeOffset.UtcNow);
        var lose = first.Grant("old iPad", DateTimeOffset.UtcNow);

        Assert.True(first.Revoke(lose.DeviceId));

        var second = DeviceRegistry.Open(directory);

        Assert.NotNull(second.Authenticate(keep.Token, DateTimeOffset.UtcNow));
        Assert.Null(second.Authenticate(lose.Token, DateTimeOffset.UtcNow));
        Assert.Null(second.Authenticate("not-a-token", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ThePairingWindowClosesOnTheFifthWrongCode()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var window = new PairingWindow();
        window.Open(now, TimeSpan.FromMinutes(10));

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.Equal(PairingResult.WrongCode, window.Redeem("ZZZZZZZZ", now));
        }

        Assert.Equal(PairingResult.TooManyAttempts, window.Redeem("ZZZZZZZZ", now));

        // Closed means closed: reopening takes a trip to the theater PC.
        Assert.Equal(PairingResult.NoWindowOpen, window.Redeem("ZZZZZZZZ", now));
    }

    [Fact]
    public void APairingCodeIsSingleUseAndExpiresOnTheClock()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var window = new PairingWindow();
        var opened = window.Open(now, TimeSpan.FromMinutes(10));

        // Spacing and case belong to whoever read the code off the screen.
        var typed = $"{opened.Code[..4].ToLowerInvariant()} {opened.Code[4..]}";
        Assert.Equal(PairingResult.Accepted, window.Redeem(typed, now));
        Assert.Equal(PairingResult.NoWindowOpen, window.Redeem(opened.Code, now));

        var second = window.Open(now, TimeSpan.FromMinutes(10));
        now = now.AddMinutes(11);
        Assert.Equal(PairingResult.Expired, window.Redeem(second.Code, now));
    }

    [Fact]
    public void ALiveTicketIsWorthOneConnection()
    {
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var tickets = new LiveTicketStore();
        var (ticket, _) = tickets.Issue("device-1", now);

        Assert.Equal("device-1", tickets.Redeem(ticket, now));
        Assert.Null(tickets.Redeem(ticket, now));

        var (stale, _) = tickets.Issue("device-1", now);
        Assert.Null(tickets.Redeem(stale, now.AddMinutes(1)));
    }

    [Fact]
    public void TheBindingNamesOneInterfaceAndTheOriginsItServes()
    {
        var endpoints = TheaterEndpoints.Resolve(LanOptions("192.0.2.24"), "THEATER-PC", development: false);

        Assert.Equal(IPAddress.Parse("192.0.2.24"), endpoints.LanAddress);
        Assert.Contains("192.0.2.24", endpoints.AllowedHosts);
        Assert.Contains("theater-pc.local", endpoints.AllowedHosts);
        Assert.Contains("https://192.0.2.24:5274", endpoints.AllowedOrigins);

        // The dev server is a development-only origin and must not appear in a shipped one.
        Assert.DoesNotContain("http://localhost:5173", endpoints.AllowedOrigins);
    }

    [Fact]
    public void TheFirewallRuleIsScopedToTheSubnetAndThePorts()
    {
        var endpoints = TheaterEndpoints.Resolve(LanOptions("192.0.2.24"), "THEATER-PC", development: false);
        var rule = endpoints.FirewallCommand(@"C:\Cantina\Cantina.Barkeep.exe");

        Assert.Contains("dir=in", rule, StringComparison.Ordinal);
        Assert.Contains("protocol=TCP localport=5273,5274", rule, StringComparison.Ordinal);
        Assert.Contains("profile=private", rule, StringComparison.Ordinal);
        Assert.Contains("remoteip=192.0.2.0/24", rule, StringComparison.Ordinal);
        Assert.Contains(@"program=""C:\Cantina\Cantina.Barkeep.exe""", rule, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCertificateNamesEveryAddressAndStaysInsideApplesLimit()
    {
        var directory = TempDirectory();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var names = new[] { "localhost", "theater-pc", "theater-pc.local" };
        var addresses = new[] { IPAddress.Loopback, IPAddress.Parse("192.0.2.24") };

        // Even asked for ten years, the server certificate comes back inside 398 days.
        var issued = TheaterCertificateAuthority.Ensure(directory, names, addresses, 3650, now);

        var subjectAlternativeNames = issued.Server.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .Single();

        Assert.Equal(names, subjectAlternativeNames.EnumerateDnsNames());
        Assert.Equal(addresses, subjectAlternativeNames.EnumerateIPAddresses());
        Assert.True(issued.Server.NotAfter - issued.Server.NotBefore < TimeSpan.FromDays(398));
        Assert.True(issued.AuthorityCreated);
        Assert.True(issued.ServerIssued);
        Assert.True(File.Exists(issued.AuthorityFilePath));
    }

    [Fact]
    public void ADhcpAddressChangeReissuesTheServerCertificateAndKeepsTheAuthority()
    {
        var directory = TempDirectory();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var names = new[] { "localhost", "theater-pc.local" };

        var first = TheaterCertificateAuthority.Ensure(
            directory, names, [IPAddress.Loopback, IPAddress.Parse("192.0.2.24")], 397, now);

        var second = TheaterCertificateAuthority.Ensure(
            directory, names, [IPAddress.Loopback, IPAddress.Parse("192.0.2.77")], 397, now);

        // The iPad trusts the authority, so the authority must not change under it.
        Assert.False(second.AuthorityCreated);
        Assert.Equal(first.AuthorityFingerprint, second.AuthorityFingerprint);

        // The server certificate must, because it no longer names where Barkeep answers.
        Assert.True(second.ServerIssued);
        Assert.NotEqual(first.Server.Thumbprint, second.Server.Thumbprint);

        // And an unchanged binding re-issues nothing at all.
        var third = TheaterCertificateAuthority.Ensure(
            directory, names, [IPAddress.Loopback, IPAddress.Parse("192.0.2.77")], 397, now);

        Assert.False(third.ServerIssued);
        Assert.Equal(second.Server.Thumbprint, third.Server.Thumbprint);
    }

    [Fact]
    public void TheServerCertificateChainsToTheTheaterAuthority()
    {
        var directory = TempDirectory();
        var now = DateTimeOffset.Parse("2026-08-28T20:00:00Z", null);
        var issued = TheaterCertificateAuthority.Ensure(
            directory, ["localhost"], [IPAddress.Loopback], 397, now);

        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(issued.Authority);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = now.UtcDateTime.AddDays(1);

        Assert.True(chain.Build(issued.Server), string.Join(
            "; ", chain.ChainStatus.Select(status => status.StatusInformation.Trim())));
    }
}

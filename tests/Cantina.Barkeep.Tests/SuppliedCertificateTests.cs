// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Cantina.Barkeep.Network;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// What changes when the certificate comes from outside (D-029). The iPad-visible difference
/// is the whole point: no profile to install, no fingerprint to compare, and nothing served
/// to install it from.
/// </summary>
public sealed class SuppliedCertificateTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SuppliedCertificateTests(WebApplicationFactory<Program> factory)
    {
        var directory = Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName());
        Directory.CreateDirectory(directory);

        // Stands in for a Let's Encrypt leaf. What matters to Barkeep is that it was handed
        // a certificate with a key, not who signed it.
        var issued = TheaterCertificateAuthority.Ensure(
            directory,
            ["cantina.aero4ge.com"],
            [IPAddress.Parse("192.0.2.24")],
            90,
            DateTimeOffset.UtcNow);

        var supplied = Path.Combine(directory, "supplied.pfx");
        File.WriteAllBytes(supplied, issued.Server.Export(X509ContentType.Pkcs12));

        _client = factory.WithWebHostBuilder(builder => builder
            .UseSetting("Yarg:Enabled", "false")
            .UseSetting("Network:Mode", "Lan")
            .UseSetting("Network:Address", "192.0.2.24")
            .UseSetting("Network:HostNames:0", "cantina.aero4ge.com")
            .UseSetting("Network:CertificatePath", supplied)
            .UseSetting("Setlist:DataDirectory",
                Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName()))
            .ConfigureServices(services =>
                services.AddSingleton<IStartupFilter, LanTestHost.RequestOriginFilter>()))
            .CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task HealthReportsTheSuppliedCertificateAndItsRemainingLife()
    {
        using var response = await _client.GetAsync("/api/health");
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(health?.Certificate);
        Assert.Equal("supplied", health.Certificate.Source);
        Assert.False(health.Certificate.NeedsDeviceTrust);
        Assert.Equal("ok", health.Certificate.Status);
        Assert.InRange(health.Certificate.DaysRemaining, 85, 90);
    }

    [Fact]
    public async Task TheOnboardingPageHasNothingToInstall()
    {
        using var request = LanTestHost.FromLanPlain(HttpMethod.Get, "/onboarding");
        using var response = await _client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nothing to install", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Certificate Trust Settings", html, StringComparison.Ordinal);

        // And it points at the name, not the address the certificate would not match.
        Assert.Contains("https://cantina.aero4ge.com", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThereIsNoAuthorityToDownload()
    {
        // A 404 rather than an empty file: there is no theater authority in this
        // configuration, so offering one would be offering a fiction.
        using var request = LanTestHost.FromLanPlain(HttpMethod.Get, "/cantina-theater-ca.cer");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OnboardingNamesTheHostRatherThanTheAddress()
    {
        using var request = LanTestHost.FromLan(HttpMethod.Get, "/api/onboarding");
        using var response = await _client.SendAsync(request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("https://cantina.aero4ge.com:5274", body.RootElement.GetProperty("secureUrl").GetString());
        Assert.False(body.RootElement.GetProperty("needsDeviceTrust").GetBoolean());
        Assert.Equal(string.Empty, body.RootElement.GetProperty("certificateFingerprint").GetString());
    }
}

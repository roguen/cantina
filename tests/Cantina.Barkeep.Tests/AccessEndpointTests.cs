// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The access surface of D-026, exercised through the real pipeline: what a LAN request is
/// refused for, what a paired one is allowed, and that the two are decided in one place.
/// </summary>
public sealed class AccessEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AccessEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = LanTestHost.Create(factory);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private int _pairings;

    /// <summary>
    /// Each pairing comes from its own address, because the pairing rate limit is real and
    /// a test suite that shares one address would be measuring the limiter instead.
    /// </summary>
    private async Task<string> PairAsync(string label = "iPad")
    {
        using var open = await _client.PostAsync("/api/pairing/window", Json("{}"));
        open.EnsureSuccessStatusCode();
        using var opened = JsonDocument.Parse(await open.Content.ReadAsStringAsync());
        var code = opened.RootElement.GetProperty("code").GetString();

        using var claim = LanTestHost.FromLan(HttpMethod.Post, "/api/pair", $"192.0.2.{++_pairings + 100}");
        claim.Content = Json($$"""{"code":"{{code}}","label":"{{label}}"}""");
        using var paired = await _client.SendAsync(claim);
        paired.EnsureSuccessStatusCode();

        using var grant = JsonDocument.Parse(await paired.Content.ReadAsStringAsync());
        return grant.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task LoopbackKeepsTheAccessItHasAlwaysHad()
    {
        // The acceptance harness and the developer both reach Barkeep this way, and the
        // trust boundary is unchanged: code already running on the theater PC is trusted.
        using var response = await _client.GetAsync("/api/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnUnpairedLanRequestIsRefusedByName()
    {
        using var request = LanTestHost.FromLan(HttpMethod.Get, "/api/live");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("pairing-required", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Fact]
    public async Task PlainHttpOnTheLanCarriesOnboardingAndRedirectsTheRest()
    {
        using var onboarding = LanTestHost.FromLanPlain(HttpMethod.Get, "/onboarding");
        using var page = await _client.SendAsync(onboarding);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Certificate Trust Settings", await page.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var control = LanTestHost.FromLanPlain(HttpMethod.Get, "/api/live");
        using var redirected = await _client.SendAsync(control);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, redirected.StatusCode);
        Assert.StartsWith(
            $"https://{LanTestHost.LanAddress}:5274/",
            redirected.Headers.Location!.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOnboardingPageNeverCarriesThePairingCode()
    {
        // Physical presence is the trust anchor. A page anyone on the LAN can read must not
        // contain the one secret that authorises a new device.
        using var open = await _client.PostAsync("/api/pairing/window", Json("{}"));
        using var opened = JsonDocument.Parse(await open.Content.ReadAsStringAsync());
        var code = opened.RootElement.GetProperty("code").GetString()!;

        using var request = LanTestHost.FromLanPlain(HttpMethod.Get, "/onboarding");
        using var page = await _client.SendAsync(request);
        var html = await page.Content.ReadAsStringAsync();

        Assert.DoesNotContain(code, html, StringComparison.OrdinalIgnoreCase);

        using var described = LanTestHost.FromLan(HttpMethod.Get, "/api/onboarding");
        using var description = await _client.SendAsync(described);
        Assert.DoesNotContain(code, await description.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task APairingWindowCannotBeOpenedFromTheLan()
    {
        // Unpaired, the credential check stops it first.
        using var anonymous = LanTestHost.FromLan(HttpMethod.Post, "/api/pairing/window");
        anonymous.Content = Json("{}");
        using var refused = await _client.SendAsync(anonymous);
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);

        // Paired is the case that matters: a device that Barkeep already trusts still
        // cannot authorise another one. Only standing at the theater PC can do that.
        var token = await PairAsync();
        using var authenticated = LanTestHost.FromLan(HttpMethod.Post, "/api/pairing/window");
        authenticated.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        authenticated.Content = Json("{}");
        using var response = await _client.SendAsync(authenticated);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnUnservedOriginIsRefusedBeforeAnyEndpointRuns()
    {
        using var request = LanTestHost.FromLan(HttpMethod.Get, "/api/live");
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("origin-not-served", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheServedOriginIsAccepted()
    {
        var token = await PairAsync();

        using var request = LanTestHost.FromLan(HttpMethod.Get, "/api/live");
        request.Headers.Add("Origin", LanTestHost.SecureOrigin);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PairingTakesTheCodeShownAtTheTheaterAndNothingElse()
    {
        using var open = await _client.PostAsync("/api/pairing/window", Json("{}"));
        open.EnsureSuccessStatusCode();

        using var wrong = LanTestHost.FromLan(HttpMethod.Post, "/api/pair");
        wrong.Content = Json("""{"code":"AAAAAAAA","label":"iPad"}""");
        using var refused = await _client.SendAsync(wrong);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("WrongCode", await refused.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task APairedDeviceIsServedAndRevocationIsImmediate()
    {
        var token = await PairAsync("Roguen's iPad");

        using var allowed = LanTestHost.FromLan(HttpMethod.Get, "/api/live");
        allowed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var served = await _client.SendAsync(allowed);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        using var listed = await _client.GetAsync("/api/devices");
        using var devices = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var device = devices.RootElement.EnumerateArray()
            .Single(entry => entry.GetProperty("label").GetString() == "Roguen's iPad");

        // The registry answers with labels and timestamps and never with a credential.
        Assert.False(device.TryGetProperty("tokenHash", out _));

        using var revoked = await _client.DeleteAsync($"/api/devices/{device.GetProperty("deviceId").GetString()}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        using var afterward = LanTestHost.FromLan(HttpMethod.Get, "/api/live");
        afterward.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var refused = await _client.SendAsync(afterward);

        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task TheLiveSocketTakesATicketAndSpendsIt()
    {
        var token = await PairAsync();

        using var request = LanTestHost.FromLan(HttpMethod.Post, "/api/live/ticket");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = Json("{}");
        using var issued = await _client.SendAsync(request);
        issued.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await issued.Content.ReadAsStringAsync());
        var ticket = body.RootElement.GetProperty("ticket").GetString();

        var sockets = _factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = context =>
        {
            context.Headers.Host = LanTestHost.LanAddress;
            context.Headers["X-Test-Remote"] = "192.0.2.99";
            context.Headers["X-Test-Scheme"] = "https";
        };

        using (var socket = await sockets.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, $"/ws/live?ticket={ticket}"),
            CancellationToken.None))
        {
            Assert.Equal(System.Net.WebSockets.WebSocketState.Open, socket.State);
        }

        // The same ticket a second time is not a credential any more.
        await Assert.ThrowsAnyAsync<Exception>(() => sockets.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, $"/ws/live?ticket={ticket}"),
            CancellationToken.None));
    }

    [Fact]
    public async Task TheLiveSocketIsRefusedWithoutATicket()
    {
        var sockets = _factory.Server.CreateWebSocketClient();
        sockets.ConfigureRequest = context =>
        {
            context.Headers.Host = LanTestHost.LanAddress;
            context.Headers["X-Test-Remote"] = "192.0.2.99";
            context.Headers["X-Test-Scheme"] = "https";
        };

        await Assert.ThrowsAnyAsync<Exception>(() => sockets.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, "/ws/live"),
            CancellationToken.None));
    }

    [Fact]
    public async Task TheCertificateIsServedAsAProfileIosWillOffer()
    {
        using var request = LanTestHost.FromLanPlain(HttpMethod.Get, "/cantina-theater-ca.cer");
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/x-x509-ca-cert", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }
}

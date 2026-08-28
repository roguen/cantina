// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Http.Json;
using Cantina.Barkeep;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cantina.Barkeep.Tests;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        // The YARG listener and poller are real I/O; tests stay deterministic (D-008).
        _client = factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Yarg:Enabled", "false")
                .UseSetting("Setlist:DataDirectory",
                    Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName()))).CreateClient();
    }

    [Fact]
    public async Task HealthEndpointReportsARunningBarkeep()
    {
        using var response = await _client.GetAsync("/api/health");
        var health = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(health);
        Assert.Equal("ok", health.Status);
        Assert.Equal("Barkeep", health.Service);
    }

    [Fact]
    public async Task UnknownHostHeaderIsRejected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Host = "attacker.example";

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

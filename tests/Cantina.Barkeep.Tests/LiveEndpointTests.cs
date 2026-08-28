// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Text.Json;
using Cantina.YargSession;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// Exercises the real pipeline with the listener and poller disabled, so the only input
/// is deterministic (D-008): bytes fed straight to the tracker, then read back through
/// the endpoint exactly as the iPad will read them.
/// </summary>
public sealed class LiveEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LiveEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Yarg:Enabled", "false")
                .UseSetting("Setlist:DataDirectory",
                    Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName())));
    }

    [Fact]
    public async Task ReportsNoDatagramsBeforeAnyInput()
    {
        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/live");
        using var state = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Dead", state.RootElement.GetProperty("freshness").GetString());
        Assert.Equal("NoDatagrams", state.RootElement.GetProperty("fault").GetString());
        Assert.Equal(JsonValueKind.Null, state.RootElement.GetProperty("song").ValueKind);
    }

    [Fact]
    public async Task ProjectsAFedObservationThroughTheEndpoint()
    {
        var tracker = _factory.Services.GetRequiredService<YargSessionTracker>();
        tracker.OnDatagram(
            DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing),
            "192.0.2.1:61374",
            DateTimeOffset.UtcNow);

        using var client = _factory.CreateClient();
        using var response = await client.GetAsync("/api/live");
        using var state = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Gameplay", state.RootElement.GetProperty("scene").GetString());
        Assert.Equal("Playing", state.RootElement.GetProperty("playState").GetString());
        Assert.Equal(1, state.RootElement.GetProperty("datagramsAccepted").GetInt64());
    }
}

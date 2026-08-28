// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Cantina.YargSession;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Cantina.Barkeep.Tests;

/// <summary>
/// The push feed end to end: fed observations arrive as frames, and a change produces a
/// new frame carrying the changed field. Deterministic input only (D-008).
/// </summary>
public sealed class LiveSocketTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LiveSocketTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder
                .UseSetting("Yarg:Enabled", "false")
                .UseSetting("Setlist:DataDirectory",
                    Path.Combine(Path.GetTempPath(), "cantina-tests", Path.GetRandomFileName())));
    }

    private static async Task<JsonElement> ReceiveFrameAsync(WebSocket socket)
    {
        var buffer = new byte[16 * 1024];
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await socket.ReceiveAsync(buffer, timeout.Token);
        return JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count)).RootElement.Clone();
    }

    [Fact]
    public async Task PushesTheInitialStateAndThenTheChange()
    {
        var client = _factory.Server.CreateWebSocketClient();
        var tracker = _factory.Services.GetRequiredService<YargSessionTracker>();

        using var socket = await client.ConnectAsync(
            new Uri(_factory.Server.BaseAddress, "/ws/live"),
            CancellationToken.None);

        var first = await ReceiveFrameAsync(socket);
        Assert.Equal("Dead", first.GetProperty("freshness").GetString());

        tracker.OnDatagram(
            DatagramBuilder.Build(YargScene.Gameplay, YargPlayState.Playing),
            "192.0.2.1:61374",
            DateTimeOffset.UtcNow);

        var second = await ReceiveFrameAsync(socket);
        Assert.Equal("Gameplay", second.GetProperty("scene").GetString());
        Assert.Equal("Playing", second.GetProperty("playState").GetString());
    }
}

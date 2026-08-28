// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net.WebSockets;
using System.Text.Json;
using Cantina.YargSession;

namespace Cantina.Barkeep.Yarg;

/// <summary>
/// Streams the live-state projection over a WebSocket, per <c>docs/live-state.md</c>.
///
/// The wire runs at ~90.7 Hz; the iPad must not (D-010's decimation rule). A frame is
/// pushed only when a field the client renders actually changed — scene, play state,
/// latched song, freshness, or fault — plus a slow heartbeat so a silent connection is
/// distinguishable from a dead one. The heartbeat also re-delivers `receivedAt`, which
/// moves constantly and is deliberately excluded from the change signature.
/// </summary>
internal static class LiveStateSocket
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(5);

    public static async Task RunAsync(
        WebSocket socket,
        YargSessionTracker tracker,
        TimeProvider clock,
        JsonSerializerOptions json,
        CancellationToken cancellationToken)
    {
        string? lastSignature = null;
        var lastSent = clock.GetUtcNow() - Heartbeat;

        while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var now = clock.GetUtcNow();
            var snapshot = tracker.Snapshot(now);
            var signature = Signature(snapshot);

            if (signature != lastSignature || now - lastSent >= Heartbeat)
            {
                var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, json);

                try
                {
                    await socket.SendAsync(
                        payload,
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                    return;
                }

                lastSignature = signature;
                lastSent = now;
            }

            try
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static string Signature(LiveState snapshot) =>
        $"{snapshot.Scene}|{snapshot.PlayState}|{snapshot.Song?.Hash}|{snapshot.Freshness}|{snapshot.Fault}";
}

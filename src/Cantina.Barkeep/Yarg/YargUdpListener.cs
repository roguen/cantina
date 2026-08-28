// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.Barkeep.Yarg;

/// <summary>
/// Receives YARG's UDP broadcast and feeds the tracker. All interpretation lives in
/// <see cref="YargSessionTracker"/>; this service only moves bytes.
///
/// The socket always sets <c>SO_REUSEADDR</c>, so Barkeep is never the reason a second
/// consumer fails (D-013). A failed bind is the named port-conflict fault, surfaced
/// through the tracker and NOT retried: the other holder lacks the option, retrying
/// cannot succeed, and a retry loop presents as a hang.
/// </summary>
internal sealed partial class YargUdpListener(
    YargSessionTracker tracker,
    TimeProvider clock,
    IOptions<YargSessionOptions> options,
    ILogger<YargUdpListener> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.Enabled)
        {
            return;
        }

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, settings.Port));
        }
        catch (SocketException exception)
        {
            tracker.ReportPortConflict();
            LogPortConflict(logger, settings.Port, exception.SocketErrorCode);
            return;
        }

        LogListening(logger, settings.Port);

        var buffer = new byte[512];
        var anyEndpoint = new IPEndPoint(IPAddress.Any, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            SocketReceiveFromResult result;

            try
            {
                result = await socket
                    .ReceiveFromAsync(buffer, SocketFlags.None, anyEndpoint, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException exception)
            {
                // A transient receive fault is not a bind conflict. The tracker's
                // freshness tiers report the silence honestly; keep listening.
                LogReceiveFault(logger, exception.SocketErrorCode);
                continue;
            }

            tracker.OnDatagram(
                buffer.AsSpan(0, result.ReceivedBytes),
                result.RemoteEndPoint.ToString() ?? "?",
                clock.GetUtcNow());
        }
    }

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Cannot bind UDP {Port}: {Error}. Another application holds the YARG data port (D-013); not retrying.")]
    private static partial void LogPortConflict(ILogger logger, int port, SocketError error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Listening for YARG datagrams on UDP {Port}.")]
    private static partial void LogListening(ILogger logger, int port);

    [LoggerMessage(Level = LogLevel.Warning, Message = "UDP receive fault {Error}; continuing.")]
    private static partial void LogReceiveFault(ILogger logger, SocketError error);
}

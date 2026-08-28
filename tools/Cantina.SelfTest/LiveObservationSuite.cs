// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Cantina.YargSession;

namespace Cantina.SelfTest;

/// <summary>
/// Validates the Cantina.YargSession library against the real YARG broadcast: the same
/// parser, tracker, latching, and freshness logic Barkeep runs, fed by the real wire for
/// three seconds. INCONCLUSIVE with a named cause when the theater is not in a testable
/// state — a missing YARG is not a failure of Cantina, and saying so would be a lie.
/// </summary>
internal static class LiveObservationSuite
{
    public static async Task<SuiteResult> RunAsync(Transcript transcript)
    {
        if (Process.GetProcessesByName("YARG").Length == 0)
        {
            transcript.Log("SKIP", "live: YARG is not running");
            return new SuiteResult("live", Verdict.Inconclusive,
                "YARG-GONE: the game is not running, so the wire cannot be observed. Start YARG and re-run.");
        }

        var tracker = new YargSessionTracker();

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

        try
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 36107));
        }
        catch (SocketException exception)
        {
            transcript.Log("SKIP", $"live: bind failed ({exception.SocketErrorCode})");
            return new SuiteResult("live", Verdict.Inconclusive,
                $"PORT-CONFLICT: could not bind 36107 ({exception.SocketErrorCode}); "
                + "another consumer without SO_REUSEADDR holds it (D-013)");
        }

        var buffer = new byte[512];
        var anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        var window = Stopwatch.StartNew();

        while (window.Elapsed < TimeSpan.FromSeconds(3))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            try
            {
                var result = await socket
                    .ReceiveFromAsync(buffer, SocketFlags.None, anyEndpoint, timeout.Token)
                    .ConfigureAwait(false);

                tracker.OnDatagram(
                    buffer.AsSpan(0, result.ReceivedBytes),
                    result.RemoteEndPoint.ToString() ?? "?",
                    DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException)
            {
                // Silence inside the window; the tracker's freshness reports it.
            }
        }

        // Diagnostic: exercise the same file-latch path the cue confirm uses.
        var songPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "YARC", "YARG", "release", "currentSong.json");

        if (File.Exists(songPath))
        {
            tracker.OnCurrentSong(await File.ReadAllTextAsync(songPath).ConfigureAwait(false));
        }

        var snapshot = tracker.Snapshot(DateTimeOffset.UtcNow);
        transcript.Log("LIVE",
            $"scene={snapshot.Scene} play={snapshot.PlayState} freshness={snapshot.Freshness} "
            + $"fault={snapshot.Fault} accepted={snapshot.DatagramsAccepted} "
            + $"rejected={snapshot.DatagramsRejected} senders={snapshot.Senders.Count} "
            + $"song={(snapshot.Song is { } latched ? $"\"{latched.Title}\"/{latched.Hash}" : "null")}");

        if (snapshot.DatagramsAccepted == 0)
        {
            return new SuiteResult("live", Verdict.Inconclusive,
                "NO-DATAGRAMS: YARG is running but nothing arrived in 3 s. "
                + "Is the Experimental UDP Data Stream enabled?");
        }

        if (snapshot.Senders.Count > 1)
        {
            return new SuiteResult("live", Verdict.Inconclusive,
                $"MULTI-SOURCE: {snapshot.Senders.Count} senders on the broadcast; "
                + "another YARG on the LAN poisons the oracle (D-020)");
        }

        var pass = snapshot.DatagramsRejected == 0
            && snapshot.Freshness == LiveFreshness.Live
            && snapshot.Fault == SessionFault.None
            && snapshot.DatagramsAccepted >= 100;

        transcript.Case("live", "wire-contract", pass,
            $"{snapshot.DatagramsAccepted} datagrams in 3 s, {snapshot.DatagramsRejected} rejected, "
            + "parser and freshness agree with the capture-backed contract");

        return pass
            ? new SuiteResult("live", Verdict.Pass,
                $"the library tracks live YARG: {snapshot.DatagramsAccepted} accepted, 0 rejected, Live/None")
            : new SuiteResult("live", Verdict.Fail,
                "the wire disagreed with the contract - see the LIVE line");
    }
}

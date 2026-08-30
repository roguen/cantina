// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Cantina.Barkeep.Setlist;
using Cantina.Barkeep.Yarg.Control;
using Cantina.YargSession;
using Microsoft.Extensions.Options;

namespace Cantina.SelfTest;

/// <summary>
/// The full loop on the real machine: gate, actuate, and verify by outcome, using the
/// SAME service, actuator, tracker, and journal classes Barkeep runs. Unlike every other
/// suite, this one SENDS INPUT — the cue itself, and then two Enter presses standing in
/// for the players at instrument setup, which is the players' screen (D-015) and is
/// driven here only because an acceptance test plays the players' role.
///
/// It ends by pausing the song it started, and says so: the polite end state is the
/// pause menu, not a silent concert.
/// </summary>
internal static class CueSuite
{
    public static async Task<SuiteResult> RunAsync(Transcript transcript, string query, string hash, string title)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SuiteResult("cue", Verdict.Inconclusive, "Windows-only");
        }

        if (Process.GetProcessesByName("YARG").Length != 1)
        {
            return new SuiteResult("cue", Verdict.Inconclusive,
                "YARG-GONE: exactly one YARG instance is required. Start it at the song list or start menu.");
        }

        transcript.Log("MODE", "the cue suite SENDS INPUT: the cue sequence, then two Enters standing in for players");

        var tracker = new YargSessionTracker();
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, 36107));

        using var lifetime = new CancellationTokenSource();
        var pump = PumpAsync(socket, tracker, lifetime.Token);

        var journalDirectory = Path.Combine(Path.GetTempPath(), "cantina-selftest", Path.GetRandomFileName());
        using var journal = SetlistJournal.Open(journalDirectory, TimeProvider.System);
        var actuator = new Win32YargActuator(Options.Create(new YargCueOptions()));
        using var actuation = new ActuationGate();
        var service = new YargCueService(tracker, actuator, actuation, journal, TimeProvider.System);

        try
        {
            // Let the tracker see the wire before gating on it.
            await Task.Delay(1500, lifetime.Token).ConfigureAwait(false);

            // Stage the theater: from a fresh launch YARG sits at its start menu, and one
            // Enter opens the Music Library (D-017). Barkeep cannot verify which menu is
            // open - that is #4 open territory and D-015 blind-menu honesty - so the
            // ACCEPTANCE TEST stages the precondition and says so, exactly as it stands
            // in for players later. If YARG was somewhere else, verify-by-outcome reports
            // the miss instead of pretending.
            _ = actuator.TryFocusYarg();
            await Task.Delay(500, lifetime.Token).ConfigureAwait(false);
            _ = actuator.PressEnter();
            transcript.Log("STAGING", "one Enter sent to open the Music Library from the start menu");
            await Task.Delay(2500, lifetime.Token).ConfigureAwait(false);

            var request = new CueRequest(
                $"selftest-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                new SetlistEntry(hash, title, "self-test"),
                query);

            var status = service.Cue(request);
            transcript.Log("CUE", $"state={status.State} detail=\"{status.Detail}\"");

            if (status.State == "refused")
            {
                return new SuiteResult("cue", Verdict.Inconclusive, $"gate refused: {status.Detail}");
            }

            if (status.State != "pending-players")
            {
                return new SuiteResult("cue", Verdict.Fail, $"actuation did not reach pending-players: {status.Detail}");
            }

            // Stand in for the players: instrument setup takes one confirm per configured
            // player; this theater has two profiles (measured during D-018 staging).
            await Task.Delay(2000, lifetime.Token).ConfigureAwait(false);
            _ = actuator.PressEnter();
            await Task.Delay(1500, lifetime.Token).ConfigureAwait(false);
            _ = actuator.PressEnter();
            transcript.Log("PLAYERS", "two ready confirms sent in the players' stead");

            var deadline = Stopwatch.StartNew();
            var songPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "YARC", "YARG", "release", "currentSong.json");

            var readState = "unread";

            while (deadline.Elapsed < TimeSpan.FromSeconds(60))
            {
                try
                {
                    if (File.Exists(songPath))
                    {
                        var content = await File.ReadAllTextAsync(songPath, lifetime.Token).ConfigureAwait(false);
                        readState = $"len={content.Length}";
                        tracker.OnCurrentSong(content);
                    }
                    else
                    {
                        readState = "absent";
                    }
                }
                catch (IOException exception)
                {
                    // Mid-rewrite; the next poll settles it.
                    readState = $"io:{exception.GetType().Name}";
                }

                var probe = tracker.Snapshot(DateTimeOffset.UtcNow);
                service.TryConfirm(probe);

                if (deadline.ElapsedMilliseconds % 5000 < 100)
                {
                    transcript.Log("PROBE",
                        $"scene={probe.Scene} play={probe.PlayState} freshness={probe.Freshness} "
                        + $"accepted={probe.DatagramsAccepted} read={readState} "
                        + $"song={(probe.Song is { } s2 ? s2.Hash : "null")} state={service.Current?.State}");
                }

                if (service.Current is { State: "done" or "failed" })
                {
                    break;
                }

                await Task.Delay(100, lifetime.Token).ConfigureAwait(false);
            }

            var final = service.Current!;
            transcript.Case("cue", "verify-by-outcome", final.State == "done",
                $"state={final.State} loaded={(final.Loaded is { } s ? $"\"{s.Title}\" ({s.Hash})" : "nothing")} detail=\"{final.Detail}\"");

            if (final.State == "done")
            {
                // Leave the theater polite: pause what we started.
                await Task.Delay(1000, lifetime.Token).ConfigureAwait(false);
                _ = actuator.PressEscape();
                transcript.Log("CLEANUP", "Escape sent; the song is paused on the pause menu");

                return new SuiteResult("cue", Verdict.Pass,
                    $"cued \"{title}\" unattended and verified it by outcome: gameplay observed with hash {hash}");
            }

            return final.State == "failed"
                ? new SuiteResult("cue", Verdict.Fail, final.Detail)
                : new SuiteResult("cue", Verdict.Inconclusive,
                    "gameplay was never observed within 30 s; the theater may need more ready confirms");
        }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);

            try
            {
                await pump.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancel.
            }

            try
            {
                Directory.Delete(journalDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Temp leftovers are harmless.
            }
        }
    }

    private static async Task PumpAsync(Socket socket, YargSessionTracker tracker, CancellationToken token)
    {
        var buffer = new byte[512];
        var anyEndpoint = new IPEndPoint(IPAddress.Any, 0);

        while (!token.IsCancellationRequested)
        {
            try
            {
                var result = await socket
                    .ReceiveFromAsync(buffer, SocketFlags.None, anyEndpoint, token)
                    .ConfigureAwait(false);

                tracker.OnDatagram(
                    buffer.AsSpan(0, result.ReceivedBytes),
                    result.RemoteEndPoint.ToString() ?? "?",
                    DateTimeOffset.UtcNow);
            }
            catch (SocketException)
            {
                return;
            }
        }
    }

}

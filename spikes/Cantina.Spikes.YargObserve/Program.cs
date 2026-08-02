// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Cantina.Spikes.YargObserve;

// Spike for issue #2: confirm YARG 0.15's UDP data stream against the provisional
// contract in docs/yarg-interface.md, and find out whether currentSong.json carries the
// song identity the datagram omits.
//
// This observes only. It never transmits on the YARG port, never writes to YARG's
// directory, and never changes game settings.

var options = SpikeOptions.Parse(args);
if (options is null)
{
    SpikeOptions.PrintUsage();
    return 2;
}

using var lifetime = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    lifetime.Cancel();
};

if (options.Seconds > 0)
{
    lifetime.CancelAfter(TimeSpan.FromSeconds(options.Seconds));
}

using var transcript = TryOpenTranscript(options.OutPath, out var transcriptError);
if (transcriptError is not null)
{
    Console.Error.WriteLine(transcriptError);
    return 2;
}

var started = DateTimeOffset.Now;
var stopwatch = Stopwatch.StartNew();
var stats = new CaptureStats();

void Emit(string line)
{
    var stamped = string.Create(
        CultureInfo.InvariantCulture,
        $"[{stopwatch.Elapsed.TotalSeconds,8:0.000}] {line}");

    Console.WriteLine(stamped);
    transcript?.WriteLine(stamped);
    transcript?.Flush();
}

Emit(string.Create(CultureInfo.InvariantCulture, $"cantina yarg-observe, started {started:yyyy-MM-dd HH:mm:ss zzz}"));
Emit(string.Create(CultureInfo.InvariantCulture, $"udp port {options.Port}, SO_REUSEADDR set so a running YALCY or Photonics keeps receiving"));
Emit($"yarg dir {options.YargDirectory}");
Emit(Directory.Exists(options.YargDirectory)
    ? "yarg dir found"
    : "yarg dir MISSING - currentSong watching will report nothing");
Emit($"yarg process running at start: {IsYargRunning()}");
Emit("enable Settings > All Settings > Experimental > UDP Data Stream in YARG, then play one song");
Emit(new string('-', 78));

// The console stays readable, but the transcript keeps the whole payload: currentSong.json
// runs to roughly two thousand characters and its field list is the finding.
void EmitToTranscript(string line)
{
    transcript?.WriteLine(line);
    transcript?.Flush();
}

var songWatcher = new CurrentSongWatcher(options.YargDirectory, TimeSpan.FromMilliseconds(250));
songWatcher.ContentChanged += (name, content) =>
{
    stats.CurrentSongObserved = true;
    Emit($"SONGFILE {name} -> {CurrentSongWatcher.Summarize(content)}");

    if (content.Length > 0)
    {
        EmitToTranscript($"----- begin {name} ({content.Length} chars) -----");
        EmitToTranscript(content);
        EmitToTranscript($"----- end {name} -----");
    }
};
songWatcher.ReadFailed += (name, message) => Emit($"SONGFILE {name} read failed: {message}");

var songTask = RunSafelyAsync(() => songWatcher.RunAsync(lifetime.Token));
var udpTask = RunSafelyAsync(() => ObserveUdpAsync(options.Port, stats, Emit, lifetime.Token));
var heartbeatTask = RunSafelyAsync(() => HeartbeatAsync(stats, Emit, lifetime.Token));

await Task.WhenAll(songTask, udpTask, heartbeatTask).ConfigureAwait(false);

Emit(new string('-', 78));
foreach (var line in stats.Summarize(stopwatch.Elapsed, IsYargRunning()))
{
    Emit(line);
}

return stats.Accepted > 0 ? 0 : 1;

static bool IsYargRunning() => Process.GetProcessesByName("YARG").Length > 0;

// The transcript directory is git-ignored and therefore absent on a fresh clone.
// Create it rather than failing the capture on a missing folder.
static StreamWriter? TryOpenTranscript(string? path, out string? error)
{
    error = null;

    if (path is null)
    {
        return null;
    }

    try
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new StreamWriter(fullPath, append: true, Encoding.UTF8);
    }
    catch (IOException ex)
    {
        error = $"cannot open transcript '{path}': {ex.Message}";
    }
    catch (UnauthorizedAccessException ex)
    {
        error = $"cannot open transcript '{path}': {ex.Message}";
    }
    catch (NotSupportedException ex)
    {
        error = $"invalid transcript path '{path}': {ex.Message}";
    }

    return null;
}

static async Task RunSafelyAsync(Func<Task> work)
{
    try
    {
        await work().ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // Expected on Ctrl+C or when the --seconds deadline fires.
    }
}

static async Task HeartbeatAsync(CaptureStats stats, Action<string> emit, CancellationToken cancellationToken)
{
    var period = TimeSpan.FromSeconds(5);
    using var timer = new PeriodicTimer(period);
    var lastAccepted = 0L;

    while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
    {
        var accepted = stats.Accepted;
        var delta = accepted - lastAccepted;
        lastAccepted = accepted;

        emit(string.Create(
            CultureInfo.InvariantCulture,
            $"rate {delta / period.TotalSeconds:0.0}/s, accepted {accepted}, rejected {stats.Rejected}"));
    }
}

static async Task ObserveUdpAsync(
    int port,
    CaptureStats stats,
    Action<string> emit,
    CancellationToken cancellationToken)
{
    using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    // Same-host coexistence with a lighting consumer is issue #11. YALCY binds with a bare
    // UdpClient and sets no reuse option, so Cantina has to be the accommodating one.
    socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

    // IP_PKTINFO exposes each datagram's destination address, which is how this spike
    // distinguishes broadcast from unicast instead of assuming.
    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);

    socket.Bind(new IPEndPoint(IPAddress.Any, port));

    var buffer = new byte[2048];
    EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

    while (!cancellationToken.IsCancellationRequested)
    {
        SocketReceiveMessageFromResult received;

        try
        {
            received = await socket
                .ReceiveMessageFromAsync(buffer, SocketFlags.None, remote, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            emit($"socket error {ex.SocketErrorCode}: {ex.Message}");
            return;
        }

        var payload = buffer.AsSpan(0, received.ReceivedBytes);
        var destination = received.PacketInformation.Address;
        var sender = received.RemoteEndPoint;

        if (!YargDatagram.TryParse(payload, out var datagram, out var rejection) || datagram is null)
        {
            stats.Rejected++;
            if (stats.Rejected <= 5)
            {
                emit($"REJECT from {sender}: {rejection}");
            }

            continue;
        }

        stats.Record(datagram, received.ReceivedBytes, sender, destination);

        if (stats.Accepted == 1)
        {
            emit($"FIRST datagram from {sender} to {destination}");
            emit(string.Create(
                CultureInfo.InvariantCulture,
                $"  version {datagram.DatagramVersion}, {received.ReceivedBytes} bytes, parser expected {datagram.ExpectedLength}"));
            emit($"  hex {Convert.ToHexString(payload[..Math.Min(payload.Length, YargDatagram.StarPowerFixedLength)])}");
            emit($"  {datagram.Describe()}");
        }

        if (stats.TryTakeSceneChange(datagram, out var previous))
        {
            emit($"SCENE {previous} -> {datagram.Scene}   ({datagram.Describe()})");
        }

        if (stats.TryTakePauseChange(datagram))
        {
            emit($"PAUSE {(datagram.Paused ? "paused" : "resumed")}");
        }
    }
}

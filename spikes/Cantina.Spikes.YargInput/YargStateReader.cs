// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

namespace Cantina.Spikes.YargInput;

internal enum YargScene : byte
{
    Unknown = 0,
    Menu = 1,
    Gameplay = 2,
    Score = 3,
    Calibration = 4,
    Practice = 5,
}

internal enum YargPlayState : byte
{
    NoSong = 0,
    Playing = 1,
    Paused = 2,
}

/// <summary>Scene and play state at a moment in time.</summary>
internal readonly record struct YargState(YargScene Scene, YargPlayState PlayState)
{
    public override string ToString() => $"scene={Scene} play={PlayState}";
}

/// <summary>
/// Minimal reader for the two datagram fields this spike uses as its oracle.
///
/// The full parser and the authoritative layout live in
/// <c>spikes/Cantina.Spikes.YargObserve</c> and <c>docs/yarg-interface.md</c>. Only scene
/// and play state are read here, deliberately: this spike proves that input *landed*, and
/// those are the two fields that change observably when it does. Duplicating six lines
/// keeps the two spikes independent, each with its own safety boundary — one observes
/// only, this one injects input.
/// </summary>
internal sealed class YargStateReader : IDisposable
{
    private const uint HeaderMagic = 0x59415247;
    private const int MinimumLength = 47;

    private readonly Socket _socket;
    private YargState? _current;

    public YargStateReader(int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Never be the reason another consumer cannot bind (D-013).
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
    }

    /// <summary>Most recent decoded state, or null until the first datagram arrives.</summary>
    public YargState? Current => _current;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[2048];

        while (!cancellationToken.IsCancellationRequested)
        {
            var received = await _socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken)
                .ConfigureAwait(false);

            if (received < MinimumLength)
            {
                continue;
            }

            if (BinaryPrimitives.ReadUInt32LittleEndian(buffer) != HeaderMagic)
            {
                continue;
            }

            _current = new YargState((YargScene)buffer[6], (YargPlayState)buffer[7]);
        }
    }

    /// <summary>
    /// Waits until the state has held steady for <paramref name="stableFor"/>, and returns it.
    ///
    /// This exists because focusing YARG is itself a state change: <c>PauseOnFocusLoss</c> is
    /// true, so the operator clicking back into the game resumes it. Taking a baseline while
    /// that is still settling would let a focus-induced transition be misread as the injected
    /// key landing — a false positive, and the worst possible outcome for this spike.
    ///
    /// Null means the state never held still, which is itself worth reporting.
    /// </summary>
    public async Task<YargState?> WaitForStableAsync(
        TimeSpan stableFor,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var overall = System.Diagnostics.Stopwatch.StartNew();
        var candidate = _current;
        var held = System.Diagnostics.Stopwatch.StartNew();

        while (overall.Elapsed < timeout)
        {
            await Task.Delay(10, cancellationToken).ConfigureAwait(false);

            var current = _current;

            if (current is null)
            {
                continue;
            }

            if (candidate is null || current.Value != candidate.Value)
            {
                candidate = current;
                held.Restart();
                continue;
            }

            if (held.Elapsed >= stableFor)
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Waits for the state to differ from <paramref name="baseline"/>, returning how long it
    /// took. A null result means nothing changed inside the timeout, which for this spike is
    /// the finding, not an error.
    /// </summary>
    public async Task<(YargState State, TimeSpan Elapsed)?> WaitForChangeAsync(
        YargState baseline,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        while (started.Elapsed < timeout)
        {
            var current = _current;

            if (current is not null && current.Value != baseline)
            {
                return (current.Value, started.Elapsed);
            }

            await Task.Delay(2, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    public void Dispose() => _socket.Dispose();
}

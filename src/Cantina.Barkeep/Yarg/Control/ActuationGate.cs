// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Cantina.Barkeep.Yarg.Control;

/// <summary>
/// One synthetic-input sequence at a time, process-wide. Two cues actuating
/// concurrently interleave their keystrokes into YARG's search box — measured live on
/// 2026-08-30, when a double-tapped Play now left the box holding
/// "head mbeatnagl yhoeuarl thhead metal hea", matched nothing, and stranded a cue at
/// pending-players forever. Every sender of input — the cue, the player stand-in, the
/// score-screen advance — holds this gate for its whole sequence, pauses included:
/// keystroke pacing is part of the sequence, not a place to hand the keyboard over.
/// </summary>
public sealed class ActuationGate : IDisposable
{
    private readonly SemaphoreSlim _one = new(1, 1);

    public void Dispose() => _one.Dispose();

    public IDisposable Hold()
    {
        _one.Wait();
        return new Release(_one);
    }

    private sealed class Release(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}

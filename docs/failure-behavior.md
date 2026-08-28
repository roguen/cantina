# Readiness and failure behavior

This document is normative for how Barkeep decides a command can be attempted, and what
the iPad is told when it cannot. It exists because the theater PC is shared: Holocron and
YARG use the same machine, display path, receiver, and HDMI audio endpoint, and a remote
command must never look successful while YARG is hidden, paused, or unreachable.

Everything here traces to measurement (D-024, plus D-013, D-017, D-020, D-022). The
freshness vocabulary is [`live-state.md`](live-state.md)'s.

## What contention actually does — measured

| Event | Measured effect |
|---|---|
| Another app takes foreground mid-song | Gameplay **pauses**, with no key behind the transition (`PlayState 1 → 2` on the wire) |
| YARG regains foreground | **Nothing.** The pause stays. Focus regain does not resume |
| Resuming | Only the pause menu's `RESUME` entry — a **blind menu** whose cursor is invisible on the wire |
| Launching Holocron | It takes foreground on launch, so a launch mid-song silently pauses the game |
| Audio endpoint | Both processes ran concurrently without either failing; `MuteOnFocusLoss` is `false`. Audible behavior is not observable by Barkeep and is not promised |
| The datagram | **Never stops.** Full rate while backgrounded, paused, or hidden — 14 h continuously observed. Under fullscreen GPU contention the rate sagged to 74–81/s with no gaps, recovering instantly |

Two consequences shape everything below. Losing the screen does not lose observability —
the wire keeps reporting. And recovery from contention is a player action, because the
pause menu is exactly the kind of blind multi-step surface D-015 put outside Cantina's
scope.

## Readiness signals

Barkeep computes readiness from what it can observe without controlling anything:

| Signal | Source | Meaning |
|---|---|---|
| `processAlive` | process list | a single YARG process exists |
| `streamLive` | datagram age (live-state.md tiers) | the wire is current |
| `singleSource` | sender endpoint tracking (D-013/D-020) | exactly one YARG is broadcasting |
| `foreground` | `GetForegroundWindow` → pid | YARG owns the screen *right now* |
| `attentive` | scene + play state | not paused, not on a blind menu mid-song |

`foreground` is read, never taken. Barkeep may bring YARG forward only as the explicit,
user-initiated first step of a cue — never silently, because a focus change moves the
game's pause state on its own.

## The fail-closed rule

**A cue is attempted only when every signal above holds.** Anything less and the command
fails *before any input is sent*, with the failing signal named:

| Failure surfaced to the iPad | When |
|---|---|
| "YARG is not running" | `processAlive` false |
| "Another application is holding the YARG data port" | bind fails (named fault, D-013) |
| "Two YARG instances are broadcasting" | `singleSource` false — never guess which |
| "YARG is not observable (last seen Ns ago)" | `streamLive` degraded to `stale`/`dead` |
| "Another application has the screen" | `foreground` false — this is the Holocron case |
| "The game is paused; resume it on the pause menu" | paused mid-song, by whomever |

The last two are the honest shape of theater contention: Barkeep reports *what stands in
the way and who can fix it* (the players, with the controllers in their hands), and does
nothing. A sent keystroke is never evidence of success (D-015), and a keystroke sent at a
hidden or paused game goes somewhere unknowable (D-017 measured keys landing wherever
focus was).

**Pause attribution.** A `1 → 2` transition with YARG foreground is a player's pause. The
same transition in the same instant that `foreground` went false is contention, and is
reported as "paused because another application took the screen" — same wire bytes,
different honest sentence, distinguished by the foreground sample at the transition.

## What Barkeep may inspect, and the non-goals

Barkeep may read: the process list, the foreground window's pid and process name, the
datagram, and `currentSong.json`. Nothing else.

Non-goals, explicitly: Barkeep never inspects, signals, launches, focuses, or terminates
Holocron; never arbitrates the audio endpoint; never switches applications on its own;
and never drives the pause menu. The entire coordination contract with Holocron is
**observation of who has the screen** — there is no IPC, no lockfile, no protocol. If the
theater wants YARG visible, a person makes it visible; Barkeep's job is to say so plainly
when it is not.

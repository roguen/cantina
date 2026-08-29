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
| `inputDeliverable` | integrity level, session id, input desktop | Windows would not silently discard injected input |
| `streamLive` | datagram age (live-state.md tiers) | the wire is current |
| `singleSource` | sender endpoint tracking (D-013/D-020) | exactly one YARG is broadcasting |
| `foreground` | `GetForegroundWindow` → pid | YARG owns the screen *right now* |
| `attentive` | scene + play state | not paused, not on a blind menu mid-song |

`inputDeliverable` is checked **before** anything is sent, and it exists because every way
Windows blocks injected input is silent: `SendInput` returns success, the event count comes
back right, and nothing arrives. Three conditions produce that shape.

- **A locked workstation.** The input desktop becomes Winlogon's, and injected events land
  on a desktop YARG is not on. Detected by `OpenInputDesktop` failing.
- **A session boundary.** Input does not cross Windows sessions. This is why
  `architecture.md` refuses to assume a service deployment without evidence.
- **An integrity mismatch.** User Interface Privilege Isolation stops a lower-integrity
  process posting input to a higher-integrity one, and refuses it without reporting it.

Measured on the theater PC on 2026-08-28: YARG runs at **Medium integrity in session 1**,
the same as Barkeep, which is why D-014's proven path works at all. None of the three
conditions can be observed *after* a cue — a discarded keystroke and a delivered one look
identical from here — and this project has already recorded one wrong conclusion from
exactly that shape, where every failed typing run had 100% acceptance (D-017). So the check
happens first, and an unreadable token is reported as unknown rather than assumed equal.

`foreground` is read, never taken *silently*. It is also the only signal evaluated
**during** actuation rather than before it, and the sequencing is deliberate:
a cue is user-initiated, so it is permitted to bring YARG forward as its explicit first
step, and only then does it require YARG to actually hold the screen. Refusing before that
step would make every cue fail whenever YARG merely sat in the background, which is its
normal state.

So the honest reading of the table row is: **if the cue cannot obtain the screen, it
refuses and names who has it** — the Holocron case — and it never sends input to a window
it does not own. What it does not do is refuse pre-emptively, because Cantina cannot tell
an application that is actively presenting from one that merely happens to be focused.
(`YargCueService.Gate` therefore checks every signal except `foreground`, which `Actuate`
checks; a reader comparing the two should know that is the design, not a gap.)

## The fail-closed rule

**A cue is attempted only when every signal above holds.** Anything less and the command
fails *before any input is sent*, with the failing signal named:

| Failure surfaced to the iPad | When |
|---|---|
| "YARG is not running" | `processAlive` false |
| "The workstation is locked; injected input reaches the secure desktop" | input desktop unavailable |
| "YARG runs in Windows session N and Barkeep in M" | session boundary |
| "YARG runs at a higher integrity level than Barkeep" | UIPI would discard the input |
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

# Live state

This document is normative for what Barkeep promises the iPad about the running game.
Every promise here traces to a captured decision: D-010, D-012, D-017, D-018, D-020. A
field with no capture behind it is not promised.

## Sources, and the trust order

| Source | Carries | Cadence |
|---|---|---|
| UDP datagram (`docs/yarg-interface.md`) | scene, play state, BPM, section, note/vocal activity | ~90.7 Hz |
| `currentSong.json` beside YARG's settings | song identity: title, artist, content hash, path | on load/clear; clears ~86 ms after scene change |
| Barkeep's own state | setlist, cursor, pending commands, outcomes | authoritative |

When sources disagree, Barkeep reports the disagreement as `ambiguous` rather than
choosing silently.

## Normalized fields promised to the client

| Field | From | Values |
|---|---|---|
| `scene` | datagram offset 6 | `unknown \| menu \| gameplay \| score \| calibration \| practice` |
| `playState` | datagram offset 7 | `noSong \| playing \| paused` (D-012; read as a byte, never a boolean) |
| `song` | `currentSong.json`, latched | `{ title, artist, hash } \| null` |
| `songSource` | derivation | `observed \| cued-by-barkeep \| unknown` |
| `receivedAt` | listener clock | timestamp of the newest accepted datagram |
| `freshness` | derivation | `live \| stale \| dead` (below) |

`song` is **latched**: it is captured when `currentSong.json` populates and carried through
the score screen, because the file clears ~86 ms after the scene changes (D-010). The raw
file being empty does not null the latched value until the next `menu` dwell — five
continuous seconds of `menu`, the same threshold the advance-observation rule below uses
for a human screen.

## Fields the wire does not carry, and their disposition

| Desired | Disposition |
|---|---|
| Playback position / progress | **Deferred to the upstream hook** (M4/M5). Never derived from BPM and beat pulses — D-010 rejected dead-reckoning because there is no length, no seek signal, and no reconciliation. |
| Score / stars at song end | Not on the wire. Screen-only. Deferred; not promised. |
| Which menu screen is open | Structurally unavailable — `scene=menu` covers the start menu, song list, settings, and instrument setup alike (D-015). Barkeep never claims to know. Honest failure reporting exists because of this gap. |
| Player count / instruments | Partially observable (note bitmask activity implies an active instrument during gameplay) but not promised; instrument setup is the players' domain (D-015). |
| Setlist contents inside YARG | Not observable anywhere (D-018 run). Barkeep's own setlist is the authority; YARG's internal queue is never queried, only fed. |

## Freshness

The datagram arrives at ~90.7 Hz, so silence is meaningful quickly:

- `live` — newest datagram younger than 500 ms (≈45 missed frames).
- `stale` — younger than 5 s. The UI shows the last state, visibly dimmed.
- `dead` — older than 5 s, or the listener reports a socket fault. The UI says the game
  is not observable, and *why* if Barkeep knows (port conflict is a named fault, D-013).

The client never renders a `stale` or `dead` state as current. UI copy stays honest: "YARG
not observed for Ns" is the truth; an empty stage picture is not.

Transient blips are absorbed before promising: the datagram stream showed a 538 ms gap in
an otherwise healthy run (D-018), so `live→stale` transitions debounce at 1 s before being
pushed to the client. The score-screen restart flicker (D-010's empty-window debounce)
remains as specified in `architecture.md`.

## Multi-sender defence

The stream is a **LAN broadcast** (D-020): any YARG on any host reaches the listener.
Barkeep binds with `SO_REUSEADDR` (D-013), tracks the sender endpoint, and if datagrams
arrive from more than one endpoint it reports `ambiguous` with both endpoints named,
rather than interleaving two games into a state belonging to neither. This defence exists
because interleaving produced a withdrawn finding once already.

## Observing an advance

However a setlist advance is initiated — by the players, or by Barkeep if
[#39](https://github.com/roguen/cantina/issues/39) decides so — the observation contract
is the same, built on the D-018 measurement:

1. `scene=score` is entered with a latched `song` S1.
2. An advance is **observed** when `scene` transitions `score → gameplay` and
   `currentSong.json` populates with hash ≠ S1's within 15 s of the transition
   (D-017's load-verification window). The advance to hash = S1 is a *restart*, not an
   advance; both are reported distinctly.
3. A `score → menu` transition with a menu dwell over 5 s is **not** an advance; a human
   screen is up. Barkeep reports the setlist as waiting on players.
4. YARG never advances on its own (D-018: 180 s observed, no transition), so an observed
   advance always has a cause. If Barkeep did not send the keypress and no players are
   expected, that is surfaced as unexpected rather than silently accepted.

## What this contract refuses to do

- Guess position, progress, or time remaining.
- Render sent input as success. Only observed outcomes are reported (D-015, D-017).
- Collapse `score → menu` into "advanced" or "stuck". The third outcome is first-class
  (D-018's `ADVANCES-TO-HUMAN-SCREEN`).
- Present a stale projection as live.

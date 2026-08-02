# YARG interface

This document is normative for what Cantina may assume about stock YARG.

**Status: confirmed by capture.** The layout below was captured from YARG 0.15 stable on the
theater PC on 2026-08-01 using [`spikes/Cantina.Spikes.YargObserve`](../spikes/README.md).
Four runs, three of them 300 seconds, accepted over 80,000 datagrams with **zero
rejections** — so the layout parses real traffic rather than merely matching a reading of
someone else's parser.

Every field in the datagram is now either confirmed or explicitly marked unpopulated.

Sources: captures on the target machine; [YALCY](https://github.com/YARC-Official/YALCY)
`YALCY/Udp/UdpIntake.cs` and `UdpIntake.Enums.cs` (LGPL-3.0-or-later, the reference
consumer, which supplied the original field names); the
[YARG DMX wiki page](https://wiki.yarg.in/wiki/DMX). Photonics is GPL and was used only to
corroborate the operator setup; its code is not an implementation source.

## What YARG offers

There are **two** observation surfaces, not one. The datagram carries live state; a pair of
files on disk carries the song identity the datagram omits. Cantina needs both.

### 1. The UDP state datagram

A fixed-layout state snapshot. It is **not** an event stream: every datagram carries the
whole state, so a consumer needs latest-wins semantics and a staleness timeout, not event
ordering or replay.

| Property | Captured value |
|---|---|
| Enabled by | `Settings > All Settings > Experimental > Enable UDP Data Stream`, stored as `DataStreamEnable` in `settings.json`. Off by default. |
| Port | **36107** |
| Destination | **`255.255.255.255`** — global broadcast, read from `IP_PKTINFO`, not inferred |
| Source | The theater PC's LAN address, ephemeral source port |
| Datagram version | **3** |
| Length | **47 bytes**, always |
| Rate | **~90.7 datagrams/second**, steady across five-minute runs |

#### Not to be confused with RB3E

Rock Band 3 Enhanced uses a different protocol on **port 21070**, and in YALCY it is an
*output* — `Rb3eTalker` broadcasts to `255.255.255.255:21070`. YALCY's README line about an
"RB3E datastream" describes that separate integration. Cantina listens only for YARG's own
datagram, identified by its header magic.

#### Wire layout

Header magic is the four bytes `0x59415247`, ASCII `YARG`, read as a little-endian
`uint32`. Offsets are zero-based.

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0 | 4 | Header magic | `0x59415247`; reject the datagram if it differs |
| 4 | 1 | `DatagramVersion` | **3** on this build |
| 5 | 1 | `Platform` | observed `1` = Windows |
| 6 | 1 | **`CurrentScene`** | Unknown 0, **Menu 1**, **Gameplay 2**, **Score 3**, Calibration 4, Practice 5 |
| 7 | 1 | **`PlayState`** | **0 no song, 1 playing, 2 paused** — captured; a three-state enum, not a boolean |
| 8 | 1 | `VenueSize` | NoVenue, Small, Large |
| 9 | 4 | `BeatsPerMinute` | IEEE 754 single |
| 13 | 1 | `SongSection` | only None 0, Chorus 2, Verse 5 |
| 14 | 1 | `GuitarNotes` | bitmask: Open 1, Green 2, Red 4, Yellow 8, Blue 16, Orange 32 |
| 15 | 1 | `BassNotes` | same bitmask as guitar |
| 16 | 1 | `DrumsNotes` | Kick 1, Red 2, Yellow 4, Blue 8, Green 16, YellowCym 32, BlueCym 64, GreenCym 128 |
| 17 | 1 | `KeysNotes` | guitar bitmask without Open |
| 18 | 4 | `VocalsNote` | single; -1 unpitched, 0 none |
| 22 | 4 | `Harmony0Note` | single |
| 26 | 4 | `Harmony1Note` | single |
| 30 | 4 | `Harmony2Note` | single |
| 34 | 1 | `LightingCue` | `CueByte` ordinal — **not** the DMX values |
| 35 | 1 | `PostProcessing` | |
| 36 | 1 | `FogState` | boolean |
| 37 | 1 | `StrobeState` | |
| 38 | 1 | `Beat` | Off 0, Measure 1, Strong 2, Weak 3 |
| 39 | 1 | `Keyframe` | Off 0, First 27, Next 28, Previous 29 |
| 40 | 1 | `BonusEffect` | boolean |
| 41 | 1 | `AutoGen` | boolean |
| 42 | 1 | `Spotlight` | performer bitmask |
| 43 | 1 | `Singalong` | performer bitmask |
| 44 | 1 | `CameraCutConstraint` | flags |
| 45 | 1 | `CameraCutPriority` | Normal, Directed |
| 46 | 1 | `CameraCutSubject` | large enum, `Random` always last |

The datagram ends at byte 46. Bytes 47 and beyond exist only at datagram version 4 or later.

#### Version 3 means no star power

Version 4 adds a `uint16` player count at byte 47 followed by two bytes per player. **YARG
0.15 stable emits version 3**, so that tail is absent and per-player star power is not
available at all. Every captured datagram was 47 bytes with a player count of zero.

Parse defensively regardless: accept the 47-byte prefix, read version-gated tails only when
both the version and the length allow it, and reject rather than guess.

#### The two cue enumerations differ, and the capture proves it

The UDP `LightingCue` byte carries a `CueByte` ordinal. Captures show `30` at the main menu
and `31` on the score screen, matching `Menu = 30` and `Score = 31` in the ordinal table.
The DMX/sACN cue channel documented on the wiki uses spaced values instead, where
`Menu = 10` and `Score = 20`.

Reading the wiki table and applying it to the UDP byte produces plausible, wrong answers.

### 2. `currentSong.json` and `currentSong.txt`

These sit beside `settings.json` in YARG's per-channel directory
(`%USERPROFILE%\AppData\LocalLow\YARC\YARG\<channel>\`, currently `release`). **They carry
the song identity the datagram does not.**

`currentSong.json` runs to roughly 1,970 characters and includes:

| Field | Use to Cantina |
|---|---|
| `Hash.HashBytes` | **Stable song identity.** Maps to Barkeep's index without fuzzy title matching. |
| `ActualLocation` | Absolute path to the song folder |
| `SortBasedLocation` | Sort-order path |
| `SubType` | Song source classification |

`currentSong.txt` is a single line of human-readable metadata: title, artist, album, genre,
year, source pack, and charter.

Both are **empty when no song is loaded** and populated while one is loaded.

This is why the upstream ask is far smaller than it first appeared: song identity is
available on stock YARG today by watching a file. See D-010.

### 3. sACN DMX output

YARG separately emits DMX over the network using sACN. Cantina neither consumes nor produces
it. It is documented here only so the cue-value distinction above is traceable, and because
it shares the theater with consumers Cantina must not disturb.

### 4. Control: nothing

Stock YARG exposes no remote control API, no IPC, no command socket, and no documented way
to select a song from outside the process. This is the gap Cantina exists to fill, and it is
why the control path stays behind one replaceable adapter. Issues
[#3](https://github.com/roguen/cantina/issues/3) and
[#4](https://github.com/roguen/cantina/issues/4) own proving an input path and deterministic
selection.

## Timing and ordering

Captured, and load-bearing for setlist logic:

- **On song start, the file leads the scene.** `currentSong` populated 37 ms to 400 ms
  *before* `CurrentScene` became `Gameplay`. Song identity is known before gameplay begins.
- **On song end, the scene leads the file.** `CurrentScene` became `Score`, and `currentSong`
  cleared **86 ms later**.
- **A song restart clears and rewrites the files.** A 256 ms window of empty content was
  observed, followed by the same song reappearing.

Three consequences:

1. **Never read `currentSong` at the moment `Score` appears.** That races the clear. Barkeep
   must cache identity from when the file populated and carry it through the score screen.
2. **Debounce empty.** An empty read is not "no song." A naive watcher reports "nothing
   playing" for a quarter second on every restart, and the iPad flickers.
3. **Decimate before the client.** ~90 datagrams/second is far more than a remote needs.
   Barkeep should push scene and identity transitions immediately and rate-limit continuous
   fields to roughly 5–10 Hz.

## What YARG does not offer

| Wanted | Available? | Consequence |
|---|---|---|
| Score-screen detection | **Yes** — `CurrentScene = 3`, captured | Auto-advance trigger is proven |
| Gameplay/menu detection | **Yes** — captured `Menu → Gameplay → Score` | |
| Pause detection | **Yes** — `PlayState = 2`, captured | Distinguishes paused from playing, which the scene byte cannot |
| Current song identity | **Yes**, via `currentSong.json` | File watch, not the datagram |
| Song metadata | **Yes**, via `currentSong.txt` | |
| **Playback position** | **No** | No progress indicator may be shown |
| **Song length** | **No** | Cannot be derived |
| **Score value** | **No** | |
| **Per-player star power** | **No** on this build | Version 4 only; 0.15 emits version 3 |
| Named song sections | **No** | Only None, Chorus, Verse |

**Playback position is now the only structural gap.** It exists on no surface found. BPM and
a beat pulse are present, but with no song length, no seek signal, and no reconciliation,
dead reckoning would produce an indicator that silently drifts. Cantina does not ship one
until an upstream interface supplies real position.

### Present but unpopulated

The DMX wiki lists sing-alongs, spotlights, and camera cuts as not yet implemented, while
the datagram still carries bytes for all three. A byte existing at a known offset is not
evidence that it holds a meaningful value. Issue
[#12](https://github.com/roguen/cantina/issues/12) must distinguish *proven*, *missing*,
*unknown*, *stale*, and *present but unpopulated*; only the first may reach the iPad as
fact.

## Byte 7 is a three-state play state, not a boolean

Captured across two deliberate pause-and-unpause cycles during one song:

```
  2.509  Score -> Gameplay    0x00 -> 0x01   song starts
 43.525                       0x01 -> 0x02   paused
 53.112                       0x02 -> 0x01   unpaused
 85.890                       0x01 -> 0x02   paused
 96.357                       0x02 -> 0x01   unpaused
248.785  Gameplay -> Score    0x01 -> 0x00   song ends
```

`0` no song, `1` playing, `2` paused.

**Read it as a byte, never as `!= 0`.** An earlier revision of this document claimed the
captures contradicted YALCY's `PauseState` name. That was wrong, and the error was ours:
YALCY reads this offset as a byte, and Cantina's first parser coerced it to a boolean, which
collapsed Playing and Paused into a single `true` and made the field appear stuck for the
whole of gameplay. The upstream name is accurate.

`PlayState` is also a **better song-active signal than the scene byte**, because
`CurrentScene = Gameplay` cannot distinguish a running song from a paused one.

## Coexistence

Barkeep runs on the same host as YARG. A lighting consumer may run there too. YALCY binds
with a bare `new UdpClient(36107)` and sets no address-reuse option, so two processes on one
host contending for that port is a real failure mode, distinct from the Holocron
audio-endpoint contention in issue [#8](https://github.com/roguen/cantina/issues/8).

Cantina sets `SO_REUSEADDR` on its listener. **That is necessary but not sufficient.**
Captured on the theater PC on 2026-08-01 with live YARG traffic, two processes, both
startup orders:

| First listener | Second listener | Result |
|---|---|---|
| no `SO_REUSEADDR` | `SO_REUSEADDR` | second bind fails, `AccessDenied` |
| `SO_REUSEADDR` | no `SO_REUSEADDR` | second bind fails, `AddressAlreadyInUse` |
| `SO_REUSEADDR` | `SO_REUSEADDR` | **both bind, both receive every datagram** (90.7/s and 90.5/s) |

**Coexistence requires `SO_REUSEADDR` on _both_ listeners.** Startup order does not help:
whichever process binds second fails. The two error codes differ because .NET on Windows
requests exclusive use when the option is absent, so the conflict is detected from either
direction.

**YALCY does not set it.** It binds with a bare `new UdpClient(36107)`. So Barkeep and YALCY
cannot both run on the theater PC today, in either order, and no change confined to Cantina
can fix it. Photonics binds through Node's `dgram`, whose reuse behavior is unverified.

Two things follow. First, the failure is **loud, not silent**: the bind throws, so Barkeep
detects the conflict immediately and must report it as a specific, actionable condition —
another application holds the YARG data port — rather than presenting as an empty or frozen
live state. Second, this is a **small, tractable upstream contribution**: one socket option
in YALCY would make coexistence work. That is a far smaller ask than a YARG hook, and YALCY
is LGPL like Cantina.

Cantina is a passive consumer and never transmits on this port.

Because the traffic is broadcast rather than unicast, consumers on *different* hosts do not
contend at all. The contention is same-host only, and moving a lighting controller to
another machine on the LAN sidesteps it entirely.

**Still unproven:** these results come from a second instance of Cantina's own listener
reproducing YALCY's bind, not from running YALCY or Photonics themselves; neither is
installed on the theater PC. Firewall-enabled behavior is also untested. Issue
[#11](https://github.com/roguen/cantina/issues/11) stays open for those.

## Evidence handling

Capture transcripts contain local network addresses and song titles, so `spikes/captures/`
and `*.capture.txt` are git-ignored. Findings are summarized here; raw transcripts are
committed only after review, per the repository's standing rules.

# YARG interface

This document is normative for what Cantina may assume about stock YARG.

**Status: provisional.** Everything below is read from YARG's documentation and from
YALCY's LGPL parser, not yet from packets captured on the theater PC. Issue
[#2](https://github.com/roguen/cantina/issues/2) must confirm it against YARG 0.15
stable before any of it becomes a closed claim. Where capture disagrees with this
document, capture wins and this document is corrected in the same commit.

Sources: [YALCY](https://github.com/YARC-Official/YALCY) `YALCY/Udp/UdpIntake.cs` and
`YALCY/Udp/UdpIntake.Enums.cs` on `master` (LGPL-3.0-or-later, the reference consumer),
the [YARG DMX wiki page](https://wiki.yarg.in/wiki/DMX), and
[Photonics](https://photonics.rocks/quickstart-guide/) documentation as independent
corroboration of the operator setup. Photonics is GPL and is evidence only; its code is
not an implementation source.

## What YARG offers

### Observation: a UDP state datagram

YARG broadcasts a fixed-layout state snapshot over UDP. It is **not** an event stream:
every datagram carries the whole state, so a consumer needs latest-wins semantics and a
staleness timeout, not event ordering or replay.

- **Enabled by the operator**, not by default: `Settings > All Settings > Experimental >
  Enable UDP Data Stream`. Photonics documents this for both stable and nightly YARG,
  which is why Cantina expects 0.15 stable to have it. Confirm on the target PC.
- **Default port 36107.** This is YALCY's default listen port and is configurable there,
  so it is a default rather than a constant.
- **Broadcast, not unicast.** No consumer configures a destination address anywhere, and
  Photonics is documented running on a separate machine, so YARG must broadcast. Confirm
  the exact destination during capture.

#### Not to be confused with RB3E

Rock Band 3 Enhanced uses a different protocol on **port 21070**, and in YALCY it is an
*output* — `Rb3eTalker` broadcasts to `255.255.255.255:21070`. YALCY's README line about
an "RB3E datastream" describes that separate integration. Cantina listens only for
YARG's own datagram, identified by its header magic.

#### Wire layout

Header magic is the four bytes `0x59415247`, ASCII `YARG`, read as a little-endian
`uint32`. Offsets are zero-based from the start of the datagram.

| Offset | Size | Field | Notes |
|---|---|---|---|
| 0 | 4 | Header magic | `0x59415247`; reject the datagram if it differs |
| 4 | 1 | `DatagramVersion` | see versioning below |
| 5 | 1 | `Platform` | Unknown, Windows, Linux, Mac |
| 6 | 1 | **`CurrentScene`** | Unknown 0, Menu 1, Gameplay 2, Score 3, Calibration 4, Practice 5 |
| 7 | 1 | **`PauseState`** | |
| 8 | 1 | `VenueSize` | NoVenue, Small, Large |
| 9 | 4 | `BeatsPerMinute` | IEEE 754 single |
| 13 | 1 | `SongSection` | only None 0, Chorus 2, Verse 5 |
| 14 | 1 | `GuitarNotes` | bitmask: Open 1, Green 2, Red 4, Yellow 8, Blue 16, Orange 32 |
| 15 | 1 | `BassNotes` | same bitmask as guitar |
| 16 | 1 | `DrumsNotes` | bitmask: Kick 1, Red 2, Yellow 4, Blue 8, Green 16, YellowCym 32, BlueCym 64, GreenCym 128 |
| 17 | 1 | `KeysNotes` | same bitmask as guitar, without Open |
| 18 | 4 | `VocalsNote` | single; MIDI-style pitch, -1 unpitched, 0 none |
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
| 47 | 2 | `PlayerStarPowerCount` | `uint16`, datagram version ≥ 4 only |
| 49 | 2×N | Per player | one byte star-power amount, one byte active flag |

Legacy datagrams end at offset 46 for a length of 47 bytes. Version 4 and later are
49 bytes plus two per player.

#### Versioning

`DatagramVersion` is a real compatibility gate: version 3 added camera cuts, version 4
added player star power. YALCY accepts anything at least 47 bytes long and reads star
power only when the version is 4 or greater. Cantina parses the same way: accept the
legacy prefix, read version-gated tails only when both the version and the length allow
it, and reject rather than guess on an unknown shorter-than-expected datagram. Which
version YARG 0.15 stable emits is unknown until capture.

#### The two cue enumerations differ

The UDP `LightingCue` byte carries a `CueByte` ordinal, counting from `Default = 0`,
with `Menu`, `Score`, and `NoCue` last. The DMX/sACN cue channel documented on the wiki
uses spaced values instead — `NoCue 0, Menu 10, Score 20, Intro 30`, and so on. They are
different encodings of a related idea. Reading the wiki table and applying it to the UDP
byte produces plausible, wrong answers.

### Observation: sACN DMX output

YARG separately emits DMX over the network using sACN, replicating the Rock Band Stage
Kit's strobe, fogger, and 32-LED array plus advanced channels. Cantina does not consume
or produce this. It is documented here only so that the cue-value distinction above is
traceable, and because it shares the theater with consumers Cantina must not disturb.

### Control: nothing

Stock YARG exposes no remote control API, no IPC, no command socket, and no documented
way to select a song from outside the process. This is the gap Cantina exists to fill,
and it is why the control path stays behind one replaceable adapter. Issues
[#3](https://github.com/roguen/cantina/issues/3) and
[#4](https://github.com/roguen/cantina/issues/4) own proving an input path and
deterministic selection against stock YARG.

## What YARG does not offer

These are the load-bearing absences. Each one constrains a capability the kickoff brief
assumed was available.

| Wanted | Present? | Consequence |
|---|---|---|
| Score-screen detection | **Yes**, `CurrentScene = 3` | Auto-advance keys off the scene byte |
| Pause detection | **Yes**, offset 7 | Free |
| Current song identity | **No** | Barkeep knows only what it cued itself |
| Playback position | **No** | No progress indicator may be shown |
| Song length | **No** | Cannot be derived |
| Score value | **No** | Star power only, and only at version ≥ 4 |
| Named song sections | **No** | Only None, Chorus, Verse |

**Song identity** is the sharpest gap. If someone picks a song at the theater PC with a
guitar controller, Cantina cannot learn what is playing. The iPad reports it as unknown.
Cantina never infers song identity from BPM, section, or note patterns.

**Playback position** does not exist in any form. BPM and a beat pulse are present, but
with no song length, no seek signal, and no way to reconcile drift, dead reckoning would
produce an indicator that silently lies. Cantina does not ship one until an upstream
interface supplies real position. This is recorded as D-010.

Together these two absences are the concrete, evidence-backed argument for the upstream
YARG contribution, which is why D-010 moved that work into M4 and M5 rather than leaving
it unscheduled.

### Present but unpopulated

The DMX wiki lists sing-alongs, spotlights, and camera cuts as not yet implemented,
while the datagram still carries bytes for all three. A byte existing at a known offset
is not evidence that it holds a meaningful value. Issue
[#12](https://github.com/roguen/cantina/issues/12) must distinguish *proven*, *missing*,
*unknown*, *stale*, and *present but unpopulated*; only the first may reach the iPad as
fact.

## Coexistence

Barkeep runs on the same host as YARG. A lighting consumer may run there too. YALCY
binds with a bare `new UdpClient(36107)` and sets no address-reuse option, so two
processes on one host contending for that port is a real failure mode, distinct from the
Holocron audio-endpoint contention in issue
[#8](https://github.com/roguen/cantina/issues/8).

Cantina sets `SO_REUSEADDR` on its listener and must prove that an already-running YALCY
or Photonics still receives every datagram afterwards. Issue
[#11](https://github.com/roguen/cantina/issues/11) owns that two-listener test. Cantina
is a passive consumer and never transmits on this port.

## Capture requirements

Issue #2 closes when a capture on the theater PC records, at minimum:

1. that the Experimental UDP Data Stream setting exists in the installed 0.15 build;
2. the observed `DatagramVersion`, datagram length, and packet rate;
3. the destination address, confirming broadcast against unicast;
4. a scene transition sequence of Menu → Gameplay → Score across one complete song;
5. behavior when a second listener is already bound to 36107.

Captures are summarized here as findings. Raw packet captures are not committed, per the
repository's standing rules.

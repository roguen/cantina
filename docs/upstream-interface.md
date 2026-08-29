# The upstream YARG interface Cantina would ask for

Status: **a proposal, and nothing in Cantina depends on it.** Every item here is something
stock YARG does not offer today, written down because the gap was measured rather than
assumed. Cantina ships working against stock YARG 0.15 and will keep doing so; this
document exists so that if any of it is ever contributed upstream, the ask is already
argued and sized.

This closes the M4 item "specify the smallest upstream YARG interface the measured control
gaps require" and the M5 item "specify the upstream observation interface for playback
position". It specifies; it does not implement.

## The rules this list obeys

**Nothing in the product may become conditional on any of it.** Cantina's adapter parses
what arrives, tolerates absence, and reports honestly (`docs/live-state.md`). A field that
appears is a field the client may show; a field that does not appear is a field the client
says nothing about. That is already how every observation surface is treated, and it is why
this list can be written without holding the product hostage to it.

**Each ask is justified by a measurement, not by a wish.** Every entry names the decision
that measured the gap and states what Cantina does today instead. If an entry cannot say
what it costs today, it does not belong here.

**Ordered by what it costs YARG, cheapest first.** The most valuable ask is not the first
one; the *smallest* one is. A one-byte addition that removes an entire class of hazard
should be asked for before a new command channel that needs an authorisation model.

**Additive and version-gated, always.** The datagram states its own version and length. Any
field proposed here belongs in a new version's tail, so a consumer parsing an older version
is unaffected. Cantina's parser handles the version it is given and does not assume the
newest.

## 1. A keyboard route to the song search field

**Not a protocol change at all — a UI fix.** This is the cheapest thing on this list and it
removes the single most fragile dependency Cantina has.

*Measured:* D-017. Song selection works — `unforgiven` narrowed 652 songs to one, and the
song loaded — but **only after a pointer click on the search field**. Typing does not focus
it. Tab does not focus it. Twenty of twenty synthesised key events were accepted by Windows
and reached nothing, and earlier search text survived forty backspaces untouched. The same
input sequence succeeds or fails depending only on whether the click happened.

*What it costs today:* the control path aims a click at a **screen coordinate**. On this
theater that was `(1968, 161)` at 3840×2160 — evidence, not a constant. It depends on
window position, resolution, and YARG's layout, and **a YARG update that moves the search
box breaks song selection silently**. It is the only part of Cantina's control path that
cannot be verified before it is used.

*The ask:* let the search field take focus from the keyboard — a focus-on-open, a `/`
accelerator, or Tab reaching it. Any of the three removes the coordinate dependency
entirely and Cantina's actuator becomes keyboard-only, which D-014 already proved works
from a background process without taking foreground.

*Cantina's side:* the click stays in `Win32YargActuator` behind `IYargActuator` and is
deleted the day a keyboard route exists. Issue
[#4](https://github.com/roguen/cantina/issues/4) is open for exactly this.

## 2. Which menu screen is open — one byte

*Measured:* D-015. `CurrentScene` reports `Menu` for the start menu, the song list,
settings, and instrument setup alike. On 2026-08-27 an input spike reported "no state
change" for an Enter that had just navigated the start menu into the song list, and its
summary concluded the key had been ignored. The key had landed perfectly. The datagram
simply cannot see menu transitions.

*What it costs today:* three things, and they are not small.

- **Barkeep cannot confirm the song list is open before typing.** Selection is issued
  blind and verified afterwards by reading back what loaded. Verify-by-outcome is good
  design and stays either way, but it is currently load-bearing rather than a safety net.
- **The pause menu is a blind surface.** `SETLIST PAUSED` offers RESUME / RESTART /
  SETTINGS / BACK TO LIBRARY, cursor position is invisible on the wire, and stray Escapes
  have left the cursor on BACK TO LIBRARY, where one blind Enter destroys a paused setlist.
  Cantina's answer is to never press Enter there at all and to hand recovery to the players
  (D-024). That is the right answer given the blindness, and it would stop being necessary.
- **Menu cursors are sticky and invisible.** The start menu remembers PRACTICE if that is
  where you last were, so one blind Enter opens the wrong mode.

*The ask:* one byte in the datagram tail naming the current menu screen — start menu, music
library, song list, instrument setup, pause menu, settings, score. It needs no new
transport, no authorisation, and no new failure mode: a consumer that does not understand
the value ignores it.

*Value per byte, this is the best ask on the list.* It converts three classes of "Cantina
must not attempt this because it cannot see" into ordinary, checkable preconditions.

## 3. Playback position and song length

*Measured:* D-010, from captures totalling over 80,000 datagrams with zero rejections. The
47-byte version-3 datagram carries scene, venue size, BPM, song section, note bitmasks,
vocal and harmony pitches, and lighting and camera fields. It carries **no playback
position and no song length**, and neither does `currentSong.json`. This is the only field
Cantina wants that no stock surface exposes anywhere.

*What it costs today:* the iPad shows no progress and no time remaining, and says nothing
about either. D-010 rejected dead-reckoning from BPM and beat pulses because there is no
length, no seek signal, and no reconciliation — the indicator would drift and misreport
with no way for the client to detect that it had. A wrong progress bar is worse than none,
so there is none.

*The ask:* two unsigned 32-bit values in the datagram tail — position in milliseconds and
song length in milliseconds — with position defined as the audio playback position and both
zero outside gameplay. Eight bytes.

*Why milliseconds and not a float of seconds:* the datagram is a state snapshot at ~90.7 Hz
with latest-wins semantics, so an integer that cannot accumulate representation error
across a five-minute song is the cheaper contract for every consumer.

*Cantina's side:* `docs/live-state.md` gains a `positionMilliseconds` and
`songLengthMilliseconds` under the same freshness rules as everything else, and the client
renders progress only while the datagram is `live`. Absent fields keep the current
behaviour, which is silence.

## 4. Select a song by content hash

This is the largest ask and the only one that needs a new surface rather than a new field.
It is listed last on purpose.

*Measured:* D-017 and D-025 together. **YARG's search is fuzzy**: `unforgiven` returned 1
of 652, but `detonation` returned **9**, including titles that do not contain the string. A
query resolves to whatever YARG ranked first, which Cantina neither controls nor predicts.
Separately, YARG's library screen shows **652 songs against 447 song folders on disk** from
its single configured source — unreconciled, recorded in D-025, and the reason a typed query
can match something Cantina cannot see or name.

*What it costs today:* selection is a request, not an actuation. Barkeep types a query,
reads back `currentSong.json`, and reports which song *actually* loaded — which is honest
and works, but means the kickoff brief's "cue a chosen song" is delivered as *request and
verify* rather than as a guarantee. Nine metadata groups in this library cannot be
distinguished by any query at all.

*The ask:* a way to ask YARG to load a specific song by the content hash it already
publishes in `currentSong.json`. The smallest useful form is a local, loopback-only request
carrying one hash, valid only while the game is in a menu, answered with accepted or
refused-and-why.

*What makes this the expensive one:* it needs an authorisation story (who may send it), a
legality story (what happens if a song is playing, or if the hash is unknown), and a
failure story — three things the observation asks need none of. Cantina would rather have
items 1 to 3 than this one, and item 1 alone removes the fragility that motivates it most.

## What Cantina deliberately does not ask for

- **Song identity in the datagram.** `currentSong.json` already states it, with a stable
  content hash and the song's path. D-010 originally concluded an upstream hook was needed
  here; the captures corrected that before the entry merged. Asking for it now would be
  asking for something YARG already gives.
- **Score or stars.** Screen-only, deferred, and never promised (`docs/live-state.md`).
  Cantina has no use for it that would justify the ask.
- **Setlist introspection or a score-screen advance.** D-018 measured that YARG's setlist
  does not auto-advance — 180 s on the score screen with no transition — and that `CONTINUE`
  advances it in ~366 ms straight to gameplay. Whether *Cantina* should press that key is
  [#39](https://github.com/roguen/cantina/issues/39), an open scope question for the owner.
  Until it is answered there is nothing coherent to ask upstream for, and asking anyway
  would be designing a feature through a protocol request.
- **Anything that reads or writes YARG's private state.** `songcache.bin`, settings
  internals, and Geomitron Bridge's IPC and database are all off limits by standing rule.
  An upstream interface is the alternative to that, not a step toward it.

## The one contribution that does not depend on YARG

**YALCY: set `SO_REUSEADDR` on the UDP intake.** One socket option. Without it no second
consumer can share the host with YALCY in either startup order (D-013). YALCY is
LGPL-3.0-or-later like Cantina, so the change is directly contributable and depends on
nothing Cantina ships first. Barkeep already sets it, which is why two Barkeep listeners
each receive every datagram (D-020).

## How Cantina's adapter must behave if any of this lands

1. **Parse by stated version and length.** The header carries both. A longer datagram than
   expected is not an error; the tail beyond what this build understands is ignored.
2. **Never require a field.** Absence keeps today's behaviour, which is to say nothing
   rather than to guess.
3. **Keep verify-by-outcome.** Even a hash-addressed selection is confirmed by reading back
   what loaded. A command that was accepted is not a song that is playing, and D-015's rule
   that a sent instruction is never evidence of success does not relax because the
   instruction got better.
4. **One parser.** Any new field lands in `src/Cantina.YargSession` and nowhere else; the
   server and the spikes share that one copy (D-012).

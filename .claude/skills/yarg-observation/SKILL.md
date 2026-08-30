---
name: yarg-observation
description: Run an unattended spike against stock YARG on the theater PC — observe its state, drive its menus, and reach a sound verdict without a human at the machine. Use whenever a Cantina question needs evidence from the running game: song selection, input injection, setlist behaviour, scene transitions, or anything in issues #2, #3, #4, #7, #11, or #12. Also use before recording any decision that claims something about how YARG behaves.
---

# Observing and driving stock YARG

This project has recorded a confident **wrong** answer twice. Both times the spike was
sound and the *conclusion* was not, because something the tool could not see was doing the
work. Read "The three oracles" before believing any result.

Everything here was measured on the theater PC. Where a number appears, it is evidence
from a specific run, not a constant — re-measure rather than inherit it.

## The three oracles, and what each cannot see

| Oracle | Sees | Blind to |
|---|---|---|
| UDP datagram, `255.255.255.255:36107`, ~90.7 Hz | `CurrentScene` (Menu/Gameplay/Score), byte 7 `PlayState` (0 none, 1 playing, 2 paused) | **Which** song. Playback position. **Which menu screen.** Any input provenance. |
| `currentSong.json` beside YARG's settings | Song identity, path, content hash | Nothing while no song is loaded. Clears ~86 ms after the scene changes (D-010). |
| The screen, via `spikes/observe-screen.ps1` | Everything a player sees | Nothing — but it is a picture, so it must be *transcribed* into the record, never cited as a filename. |

**The datagram reports `Menu` for the start menu, the song list, settings, and instrument
setup alike.** A key that moves between menu screens lands perfectly and produces no state
change. On 2026-08-27 the input spike reported "no state change" for an Enter that had just
navigated the start menu into the song list, and its summary called the key ignored. If the
expected effect does not cross a scene boundary, **the datagram cannot judge it — screenshot
instead.**

`currentSong.json` is **zero-length** when no song is loaded, not absent. Code that treats
absent and empty as one value is already wrong on this machine. Read `"HashBytes"`, not
`"Hash"` — the shape is `{"Hash":{"HashBytes":"…"}}`, so searching the outer key returns the
inner key's *name*, which is constant across songs and silently suppresses identity changes.

## Tools

| | |
|---|---|
| `src/Cantina.YargSession` | **The shared parser, latching, and freshness logic.** Barkeep and the spikes both reference it; never fork it (D-012's boolean trap is what a second copy causes). |
| `src/Cantina.Barkeep/Yarg/Control` | The production cue pipeline: `IYargActuator` (the seam), `Win32YargActuator` (the proven primitives), `YargCueService` (gate + actuate + verify). |
| `tools/Cantina.SelfTest` | **The acceptance harness. Start here.** `run all` for journal/live/readiness; `run cue` for the full unattended loop; `run confirmdiag` to reproduce the confirm loop in isolation. |
| `spikes/Cantina.Spikes.YargObserve` | Listener and capture. Read-only. Still the fastest way to see raw wire state. |
| `spikes/Cantina.Spikes.YargInput` | Sends input. `--focus-yarg` runs it unattended. Useful for one-off menu driving. |
| `spikes/Cantina.Spikes.YargSetlist` | Watch-only verdict harness. Links no input APIs at all. |
| `spikes/observe-screen.ps1` | Screen capture. Read-only. Writes to the gitignored `spikes/captures/`. |

**Before writing a new spike, check whether the acceptance harness already answers it.**
Most YARG questions now have a suite, and a suite reports PASS/FAIL/INCONCLUSIVE with a
named cause instead of leaving you to interpret a transcript.

Captures are **gitignored on purpose** — they show the local song library. The repository
carries the spike sources and the Decision Log conclusions; the captures are local and
reproducible.

## Safety rules that are not optional

1. **Never take foreground during a measurement.** `PauseOnFocusLoss` is `true`: any app
   taking focus mid-song pauses the game with no key behind it (measured, D-024). Focus
   regain does **not** resume — recovery is the pause menu's RESUME entry, a blind menu.
   Focusing *before* a measurement starts is fine and is what `--focus-yarg` does.
   `MuteOnFocusLoss` is `false`, and the datagram keeps flowing at full rate while YARG is
   backgrounded, paused, or hidden behind a fullscreen app — under GPU contention the rate
   sags (74–81/s observed under Holocron) but never gaps.
2. **Never send input during an observation window.** The datagram carries no input
   provenance, so a dismissed score screen and a self-advancing one are byte-identical. A
   watch harness should link no `SendInput`, `keybd_event`, `mouse_event`, or
   `SetForegroundWindow`, so its innocence is a property of the assembly rather than a claim
   in a log.
3. **Screen capture is safe.** `CopyFromScreen` does not change focus and does not advance
   `GetLastInputInfo`.
4. **One YARG instance, and one datagram sender.** These are different checks. The stream is
   a **global broadcast**, so any YARG on the LAN reaches the listener; two senders decode as
   rapid `Score → Gameplay` churn, which is exactly the signature of an auto-advance. The
   two-instance case already invalidated one run.

## Driving the menus

**Scan Songs** (D-030): the Music Library's MORE OPTIONS control at (1340, 2064) on
3840×2160 opens a popup whose third entry is SCAN SONGS at (1903, 939) — both pointer
clicks, both coordinates evidence rather than constants. The scan has **no completion
signal on any observable surface**; bound it by time and read the header's song count off
a screenshot if you need proof it ran. A completed scan is also how the stale-cache
652-vs-447 mystery died: the count reads what is on disk afterwards.

Proven sequence, D-017 and D-018:

1. **Launch** `C:\YARC\YARG Installs\<guid>\installation\YARG.exe` if it is not running. It
   has exited between sessions before, and it changes the display to 3840×2160.
2. **Focus** with `--focus-yarg`, which verifies via `GetForegroundWindow` rather than
   trusting `SetForegroundWindow`'s return value.
3. **Enter** at the start menu opens the Music Library. No scene change — screenshot to
   confirm.
4. **Click the search box.** This is required and there is no keyboard route. Typing does not
   focus it and Tab does not focus it. Without the click, characters are accepted by Windows
   and reach nothing, and earlier search text survives 40 backspaces untouched.
5. **Type the query**, then **Enter** selects the top match, then **Enter** again plays it.
6. **Instrument setup** is per-player: one confirmation each. It will refuse politely if a
   configured player has no input device assigned.
7. **The score screen takes one key.** `CONTINUE` advances a setlist in ~366 ms straight to
   gameplay, skipping instrument setup.

The click target was at **(1968, 161) on 3840×2160**. That is evidence, not a constant — it
depends on resolution and on YARG's layout, and a YARG update that moves the box breaks
selection silently. Re-derive it from a screenshot rather than pasting it.

**YARG's search is fuzzy.** `unforgiven` returned 1 of 652; `detonation` returned 9,
including titles that do not contain the string. A query resolves to whatever YARG ranked
first, which Cantina neither controls nor predicts. Always read back `currentSong.json` to
learn what actually loaded.

## Confounds that have actually fired here

Check these before trusting a result. Each one has either invalidated a run or was caught
just in time.

- **The on-screen counter is a star counter, not a set count.** It reads `130 / 3260` on a
  652-song library, scaling at 5 per song. It never shows setlist membership.
- **A one-song setlist is byte-identical to a setlist that will not advance.** Verify arming
  *before* the observation window opens, not afterwards. The score screen offering
  `END SETLIST`, and the next song actually loading on `CONTINUE`, are what rescued the run
  of 2026-08-27 — after the fact.
- **`settings.json` carries `"PlayAShowTimeout": 10.0`.** Any observation window shorter than
  a large multiple of this proves nothing. `NoFail: 1` means a song plays to the end with
  nobody playing, which is what makes unattended runs possible at all.
- **`PauseOnDeviceDisconnect: true`** — a controller dropping pauses the game, which reads as
  "stuck".
- **A stuck key.** `SendKeyPress` has no `try/finally` around its hold, so a staging process
  killed mid-hold leaves a key logically down, autorepeating into a UI where hold is a
  first-class gesture. Sweep key state before opening a window.
- **A controller.** XInput updates neither `GetLastInputInfo` nor a keyboard hook, and a
  controller is the normal way a score screen gets dismissed on this PC. Baseline
  `dwPacketNumber` per slot and watch it.
- **A firewall dialog.** Binding the UDP socket raised a Windows Defender prompt for the
  harness itself, which sat on screen through a measurement. It did not take foreground, but
  it could have. Decline such prompts — never grant firewall access.
- **Windows accepting an injection is not YARG receiving it.** Count `SendInput`'s return
  value. Every failed typing run so far had 100% acceptance; the variable was focus. Three
  further conditions swallow input just as silently — a locked workstation, a session
  boundary, and an integrity mismatch (UIPI). Barkeep now checks all three before sending
  anything (D-027), and `run readiness` reports the answer for this host.
- **`currentSong.json` populates during the LOAD SCREEN**, up to ~2 s before gameplay
  datagrams begin. Anything that latches identity can therefore latch during a menu dwell
  and have it cleared a moment later. The tracker handles this (the clear resets the
  change-detection hash so the next read re-latches), but any new reader must not assume
  file-populated implies gameplay-started. This defect was invisible to every unit test
  and was caught only by the full loop against the real game.
- **YARG shows 652 songs; the disk holds 447 song folders.** Cantina indexes the 447
  (D-025) but YARG's fuzzy search runs over its own 652, so a typed query can match
  something Cantina cannot see or name. Unreconciled, recorded in D-025's final paragraph,
  and the reason verify-by-outcome is load-bearing rather than a nicety: the cue reads back
  what actually loaded instead of assuming the search hit what the index expected. There is
  also one charted folder with no `song.ini` (448 note files against 447 inis), invisible
  to the index — a small instance of the same gap.
- **Menu cursors are sticky, invisible state.** The start menu remembers PRACTICE if that
  is where you last were, and one blind Enter then opens the wrong mode. Sticky cursors
  exist on the start menu, the pause menu, and instrument setup. Screenshot before any
  Enter whose target you have not just verified.
- **Escape moves the pause-menu cursor.** The pause menu (`SETLIST PAUSED`: RESUME /
  RESTART / SETTINGS / BACK TO LIBRARY) is a blind menu — cursor position is invisible on
  the wire, Escape does not resume, and stray Escapes left the cursor on BACK TO LIBRARY
  once, where a blind Enter would have destroyed the paused setlist. Never press Enter on
  the pause menu without transcribing a screenshot first.

## Reaching a verdict

Model the outcome space on `tools/Cantina.SelfTest` (or `Cantina.Spikes.YargSetlist` for
a watch-only measurement):

- Name the **bounded** negative — "no advance within 180 s", never "never".
- Keep a **third outcome** where one exists. "Left the score screen into a menu" is neither
  auto-advance nor stuck, and collapsing it would be a materially wrong product answer.
- **`INCONCLUSIVE` is first-class and should be common**, with a named cause and the stage
  reached. A gate that never fires is too loose. Report what was measured separately from
  what was inferred.

## Running the product against the real game

Barkeep now implements what the spikes proved. To exercise it end to end:

```bash
dotnet run --project src/Cantina.Barkeep --configuration Release
```

It binds loopback `5273` by default and serves `/api/health`, `/api/live`, `/ws/live`,
`/api/songs`, `/api/library/rescan`, `/api/setlist`, `/api/setlist/commands`, `/api/cue`,
`/api/cue/current`, and the D-026 access surface (`/api/onboarding`, `/onboarding`,
`/cantina-theater-ca.cer`, `/api/pairing/window`, `/api/pair`, `/api/devices`,
`/api/live/ticket`). `Network:Mode=Lan` adds the LAN listeners; see
[`docs/lan-transport.md`](../../../docs/lan-transport.md). The library scan reads the same
`SongFolders` YARG's settings name (D-025), so anything searchable is cueable.

For the client, `npm run dev` in `src/cantina-client` proxies `/api` and `/ws` to
Barkeep. The full regression before any pull request is in
[`docs/development.md`](../../../docs/development.md); the target-PC acceptance run is
`Cantina.SelfTest`.

**The cue pipeline sends input and can start a real song.** Treat running it the way you
would treat any change to a live system: know what state the theater is in first, and
leave it parked politely — the `cue` suite pauses what it started, and so should you.

## When the answer is "stock YARG cannot"

Some gaps are structural, and the honest end of a spike is naming one rather than working
around it. `docs/upstream-interface.md` collects them: what Cantina cannot do, the
measurement that proved it, what Cantina does instead today, and the smallest upstream
change that would close it. If a spike finds a new one, add it there in that shape — and
keep the product working without it.

## Recording the result

Records live in the repository (D-016) and change through the same pull request as the code.

| | |
|---|---|
| `project/Decision-Log.md` | A new `D-0NN` entry. **Append-only** — a reversal supersedes, never rewrites. |
| `project/Time-Log.md` | One entry per session. **Append-only.** |
| `project/Roadmap.md` | Tick or annotate the affected milestone item. |
| `docs/` | Only if a normative contract actually changed. |

State the cost of a finding as plainly as the finding. D-017 proved selection works *and*
that it now needs a pointer click at a screen coordinate, which is a real charge against
D-014.

Git practice is the `force-barrier` skill: branch before the first edit, verify the branch
again before committing, open the pull request, and **do not merge** — that is the owner's
call unless he has said otherwise this session.

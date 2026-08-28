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
| `spikes/Cantina.Spikes.YargObserve` | Listener and capture. Read-only. |
| `spikes/Cantina.Spikes.YargInput` | Sends input. `--focus-yarg` runs it unattended. |
| `spikes/Cantina.Spikes.YargSetlist` | Watch-only verdict harness. Links no input APIs at all. |
| `spikes/observe-screen.ps1` | Screen capture. Read-only. Writes to the gitignored `spikes/captures/`. |

Captures are **gitignored on purpose** — they show the local song library. The repository
carries the spike sources and the Decision Log conclusions; the captures are local and
reproducible.

## Safety rules that are not optional

1. **Never take foreground during a measurement.** `PauseOnFocusLoss` is `true`. Taking
   focus pauses the game; giving it back *resumes* the game — a state change with no key
   behind it. Focusing *before* a measurement starts is fine and is what `--focus-yarg`
   does.
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
  value. Every failed typing run so far had 100% acceptance; the variable was focus.

## Reaching a verdict

Model the outcome space on `Cantina.Spikes.YargSetlist`:

- Name the **bounded** negative — "no advance within 180 s", never "never".
- Keep a **third outcome** where one exists. "Left the score screen into a menu" is neither
  auto-advance nor stuck, and collapsing it would be a materially wrong product answer.
- **`INCONCLUSIVE` is first-class and should be common**, with a named cause and the stage
  reached. A gate that never fires is too loose. Report what was measured separately from
  what was inferred.

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

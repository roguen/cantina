# Spikes

Proof-of-concept code that answers an M1 question with evidence. A spike is allowed to be
rough, but it is not allowed to be dishonest: it must report what it actually observed,
including nothing.

Spikes never modify YARG, its settings, or its data. They observe, and they say plainly
when they cannot.

## `Cantina.Spikes.YargObserve` — issue [#2](https://github.com/roguen/cantina/issues/2)

Confirms YARG's UDP data stream against the contract in
[`../docs/yarg-interface.md`](../docs/yarg-interface.md). That document was originally
derived by reading YALCY's LGPL parser rather than by capturing packets; this spike's
captures replaced that reading, and the document now records itself as confirmed by
capture. Where a future run disagrees with it, the capture wins and the document is
corrected.

It answers five things in one run:

1. Does YARG 0.15 stable emit at all, once the setting is on?
2. What datagram version, length, and packet rate?
3. Broadcast or unicast? Read from the datagram's real destination address via `IP_PKTINFO`,
   not inferred.
4. Does `CurrentScene` transition `Menu -> Gameplay -> Score` across one song, making it a
   trustworthy auto-advance trigger?
5. Does `currentSong.json` populate during play? If it does, it supplies the song identity
   the datagram omits, and the case for an upstream observation hook weakens considerably.

### Before running

Enable the stream **in the game**, not by editing files: `Settings > All Settings >
Experimental > Enable UDP Data Stream`. YARG owns `settings.json` and may rewrite it on
exit, so editing it underneath a running game is unreliable.

### Run

```bash
dotnet run --project spikes/Cantina.Spikes.YargObserve -- --seconds 300 --out spikes/captures/run-01.capture.txt
```

Then, in YARG: sit at the menu, start a song, play it to the end, and let the score screen
appear. Stop the spike with Ctrl+C, or let `--seconds` expire.

Exit code is 0 if at least one datagram was accepted, 1 if none arrived, 2 on bad usage.

### Marking the timeline

**Press Enter during a run to record a mark.** Anything typed before Enter becomes the
mark's label. A mark freezes the whole 47-byte datagram at that instant, and the end-of-run
summary diffs consecutive marks byte by byte.

This exists because a byte that never moves is otherwise ambiguous: it cannot be told apart
from an operator who forgot to perform the action. Marks on both sides of an action turn a
non-event into evidence.

### Procedure: settle byte 7 (open part of #2)

Byte 7 is named `PauseState` upstream, but captures contradict the name — it read non-zero
for all of gameplay and zero at menu and score, the opposite of a pause flag for a song
nobody paused. This run settles it:

1. Start the spike, then start a song and let it play normally.
2. Type `before pause` and press Enter.
3. **Pause the game.** Wait a few seconds.
4. Type `paused` and press Enter.
5. **Unpause.** Wait a few seconds.
6. Type `resumed` and press Enter.
7. Let the song finish, or stop with Ctrl+C.

Read `DIFFS BETWEEN CONSECUTIVE MARKS`:

- **Byte 7 changes across the pause** → the name is right and only the polarity was
  confusing.
- **Byte 7 does not change, but some other offset does** → that offset is the real pause
  signal, and byte 7 means something else.
- **No byte changes at all** → stock YARG does not broadcast pause, which is itself the
  finding, and the iPad must never claim to know.

`BYTE ACTIVITY` supports this: it separates offsets that moved from offsets frozen for the
whole run, so a candidate flag is easy to spot.

### Procedure: listener coexistence (#11)

`--no-reuse` binds the way YALCY does — a bare `new UdpClient(port)` with no options — so a
second instance can stand in for a lighting consumer without installing one.

Run two instances against live YARG traffic and compare accepted counts, in both orders:

```bash
# terminal 1, the stand-in, started first
dotnet run --project spikes/Cantina.Spikes.YargObserve -- --no-reuse --seconds 30

# terminal 2, Cantina's real binding
dotnet run --project spikes/Cantina.Spikes.YargObserve -- --seconds 20
```

Captured result on the theater PC, 2026-08-01:

| First | Second | Result |
|---|---|---|
| no reuse | reuse | second bind fails, `AccessDenied` |
| reuse | no reuse | second bind fails, `AddressAlreadyInUse` |
| reuse | reuse | both bind, both receive every datagram |

Coexistence needs `SO_REUSEADDR` on **both** sides, and startup order does not help. See
`docs/yarg-interface.md` and D-013.

This stands in for YALCY rather than being YALCY. Running the real application, and testing
with the firewall enabled, are still open in
[#11](https://github.com/roguen/cantina/issues/11).

### Reading the result

The summary block is the deliverable. `destinations` answers broadcast versus unicast.
`scene order` should read `Menu -> Gameplay -> Score`. `currentSong populated` reports
whether song identity is observable.

If nothing arrives, the likely causes in order are: the setting is off, YARG is not
running, or the Windows firewall is dropping inbound UDP on 36107.

## `Cantina.Spikes.YargInput` — issue [#3](https://github.com/roguen/cantina/issues/3)

**This spike sends input.** That is the opposite safety boundary from the observer above,
which is why it is a separate program rather than a flag.

It answers one question: does stock YARG accept synthetic keyboard input? The oracle is
YARG's own datagram — a key that lands changes scene or play state, a key that does not
changes nothing. That is what makes the run decisive instead of watching a projector and
guessing.

### Why it never takes foreground

YARG's `PauseOnFocusLoss` is **true**. A tool that stole focus to deliver a key would pause
the game and then measure its own side effect. So the operator keeps YARG focused and this
process injects from the background — which is exactly the constraint Barkeep will live
under. It also verifies YARG actually held focus at the moment of sending, so a null result
cannot be confused with a key delivered somewhere else.

### Why not a virtual controller first

The kickoff brief expected ViGEmBus to rank first. It is **archived** — last pushed
November 2023 — so it fails issue #3's own bar of a maintainable installation and
redistribution story. A kernel-mode driver does not go near the theater PC unless
`SendInput` is proven to fail.

### Run

```bash
dotnet run --project spikes/Cantina.Spikes.YargInput -- --key escape --wait 8
```

Start a song first, so the screen has an unambiguous response to the key. Focus YARG during
the countdown and then keep hands off the keyboard: a real key press would confound the
result.

```
  --key <name>   escape, enter, space, backspace, up, down, left, right
  --wait <n>     seconds to focus YARG before sending (default 8)
  --timeout <n>  seconds to wait for a state change (default 3)
  --dry-run      do everything except send
```

Exit 0 if a state change was observed, 1 if not, 2 on bad usage.

### Reading the result

A `STATE CHANGED` line with a latency proves synthetic input reached YARG. `NO STATE CHANGE`
means either YARG ignored the key or the key does nothing on that screen — try a screen
where its effect is unambiguous before concluding `SendInput` is rejected.

`SendInput accepted N of 2` below 2 is a third, distinct outcome: Windows refused the
injection itself, which usually means an integrity-level mismatch rather than anything about
YARG.

### Evidence handling

Transcripts contain local IP addresses and song titles, so `spikes/captures/` and
`*.capture.txt` are git-ignored. Summarize findings into `docs/yarg-interface.md` and the
issue; commit a transcript only after reading it.

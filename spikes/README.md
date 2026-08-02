# Spikes

Proof-of-concept code that answers an M1 question with evidence. A spike is allowed to be
rough, but it is not allowed to be dishonest: it must report what it actually observed,
including nothing.

Spikes never modify YARG, its settings, or its data. They observe, and they say plainly
when they cannot.

## `Cantina.Spikes.YargObserve` — issue [#2](https://github.com/roguen/cantina/issues/2)

Confirms YARG's UDP data stream against the provisional contract in
[`../docs/yarg-interface.md`](../docs/yarg-interface.md), which was derived by reading
YALCY's LGPL parser rather than by capturing packets. Where this spike disagrees with that
document, the capture wins and the document is corrected.

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

### Reading the result

The summary block is the deliverable. `destinations` answers broadcast versus unicast.
`scene order` should read `Menu -> Gameplay -> Score`. `currentSong populated` reports
whether song identity is observable.

If nothing arrives, the likely causes in order are: the setting is off, YARG is not
running, or the Windows firewall is dropping inbound UDP on 36107.

### Evidence handling

Transcripts contain local IP addresses and song titles, so `spikes/captures/` and
`*.capture.txt` are git-ignored. Summarize findings into `docs/yarg-interface.md` and the
issue; commit a transcript only after reading it.

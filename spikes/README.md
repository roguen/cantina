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

### Reading the result

The summary block is the deliverable. `destinations` answers broadcast versus unicast.
`scene order` should read `Menu -> Gameplay -> Score`. `currentSong populated` is the
finding that may reshape D-010.

If nothing arrives, the likely causes in order are: the setting is off, YARG is not
running, or the Windows firewall is dropping inbound UDP on 36107.

### Evidence handling

Transcripts contain local IP addresses and song titles, so `spikes/captures/` and
`*.capture.txt` are git-ignored. Summarize findings into `docs/yarg-interface.md` and the
issue; commit a transcript only after reading it.

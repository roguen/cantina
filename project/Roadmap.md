# Roadmap

Milestones close only when every exit criterion is supported by repository or
target-environment evidence.

## M0 · Foundations

- [x] Initial repository scaffold, LGPL-3.0-or-later license, and locked dependencies
- [x] Normative `docs/` boundary established
- [x] Decision Log begins with PWA over native
- [x] Issues opened for all seven kickoff questions
- [x] Meaningful local server and client checks
- [x] CI green on `main` — run
  [30717785137](https://github.com/roguen/cantina/actions/runs/30717785137), tracked by
  closed issue [#1](https://github.com/roguen/cantina/issues/1)
- [ ] Self-contained artifact proven on Windows 10 Pro 22H2 —
  [#9](https://github.com/roguen/cantina/issues/9)
- [ ] Public-release gate — the repository was published on 2026-08-01 (D-011) before
  this gate's audit items were worked, so they are now retrospective —
  [#10](https://github.com/roguen/cantina/issues/10)
- [ ] Living pages migrated from the temporary `project/` fallback to the wiki, now
  unblocked by publication — [#13](https://github.com/roguen/cantina/issues/13)
- [ ] Protected `main` enforced by GitHub rather than a bypassable client-side hook, now
  unblocked by publication — [#14](https://github.com/roguen/cantina/issues/14)

## M1 · Spike results

- [x] Capture and specify YARG 0.15's UDP stream —
  [#2](https://github.com/roguen/cantina/issues/2). Wire contract, broadcast destination,
  rate, the `Menu → Gameplay → Score` transition, and the byte 7 play state are captured
  and specified. Listener coexistence stays with
  [#11](https://github.com/roguen/cantina/issues/11)
- [ ] Prove a reliable stock-YARG input path —
  [#3](https://github.com/roguen/cantina/issues/3)
- [ ] Prove or reject deterministic song selection —
  [#4](https://github.com/roguen/cantina/issues/4)
- [ ] Prove coexistence with the theater lighting consumer —
  [#11](https://github.com/roguen/cantina/issues/11)
- [ ] Choose and prove the Geomitron Bridge acquisition boundary, completed-arrival
  detection, YARG refresh, and exact-song handoff —
  [#17](https://github.com/roguen/cantina/issues/17)
- [x] Deterministic application-policy theater harness and stable CI regression gate —
  [#19](https://github.com/roguen/cantina/issues/19)
- [x] Confirm the wire contract in
  [`docs/yarg-interface.md`](../docs/yarg-interface.md) against captured packets, and
  commit the spike under `spikes/`
- [ ] Record a stock-YARG go/no-go decision

## M2 · Library

- [ ] Choose the authoritative metadata source —
  [#5](https://github.com/roguen/cantina/issues/5)
- [ ] Resilient incremental indexing with explicit skip reasons
- [ ] Reconcile stable Geomitron Bridge `.sng` arrivals exactly once and map provider
  identity to Cantina song identity — [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Full-text search across title, artist, album, and charter
- [ ] Album-art selection, serving, and cache invalidation

## M3 · iPad client

- [ ] Resolve discovery, pairing, HTTPS/WSS, and firewall design —
  [#6](https://github.com/roguen/cantina/issues/6)
- [ ] Installable Home Screen experience and automatic reconnection
- [ ] One-handed browse and search with persistent connection state
- [ ] Show honest acquisition, validation, refresh, and play-next progress; direct
  Geomitron Bridge search/download stays disabled until a supported external contract
  exists — [#17](https://github.com/roguen/cantina/issues/17)

## M4 · Control

- [ ] Cue a selected song through one replaceable control interface
- [ ] Refresh stock YARG, prove the exact imported song is visible, and fulfill
  play-next without interrupting an active song —
  [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Report bounded success and honest failure on the iPad
- [ ] Define Holocron and theater-contention behavior —
  [#8](https://github.com/roguen/cantina/issues/8)
- [ ] Specify the smallest upstream YARG interface the measured control gaps require,
  behind the existing replaceable adapter (D-010)

## M5 · Queue and state

- [ ] Decide setlist durability and restart recovery —
  [#7](https://github.com/roguen/cantina/issues/7)
- [ ] Define proven, missing, unknown, stale, and present-but-unpopulated live-state
  fields — [#12](https://github.com/roguen/cantina/issues/12)
- [ ] Show only evidence-backed live state: scene, play state, and beat from the datagram,
  song identity and metadata from `currentSong.json`. No playback-position indicator ships
  without an upstream source (D-010, D-012)
- [ ] Decimate the ~90 Hz datagram stream and debounce the transient empty window that a
  song restart produces in `currentSong` (D-010)
- [ ] Specify the upstream observation interface for **playback position**, the only
  field no stock YARG surface exposes (D-010)
- [ ] Safe score-screen auto-advance on the `CurrentScene` byte, with a cancel window
- [ ] Recover acquisition and play intent without duplicate install, setlist insertion,
  refresh, or late cue — [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Record measured end-to-end latency

## Beyond

Submit the interface specified in M4 and M5 to YARG upstream, and carry it through
review. D-010 moved the *specification* work into M4 and M5 because capability 4 cannot
be met without it; what remains here is the contribution itself, which depends on
upstream timelines Cantina does not control.

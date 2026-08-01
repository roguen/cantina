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
- [ ] Living pages migrated from the temporary fallback to the wiki —
  [#13](https://github.com/roguen/cantina/issues/13)
- [ ] Protected `main` enforced by GitHub —
  [#14](https://github.com/roguen/cantina/issues/14)
- [ ] Eventual public-release gate —
  [#10](https://github.com/roguen/cantina/issues/10)

## M1 · Spike results

- [ ] Capture and specify YARG 0.15's UDP stream —
  [#2](https://github.com/roguen/cantina/issues/2)
- [ ] Prove a reliable stock-YARG input path —
  [#3](https://github.com/roguen/cantina/issues/3)
- [ ] Prove or reject deterministic song selection —
  [#4](https://github.com/roguen/cantina/issues/4)
- [ ] Prove coexistence with the theater lighting consumer —
  [#11](https://github.com/roguen/cantina/issues/11)
- [ ] Choose and prove the Geomitron Bridge acquisition boundary, completed-arrival
  detection, YARG refresh, and exact-song handoff —
  [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Commit spike proofs and `docs/yarg-interface.md`
- [ ] Record a stock-YARG go/no-go decision

## M2 · Library

- [ ] Choose the authoritative metadata source —
  [#5](https://github.com/roguen/cantina/issues/5)
- [ ] Resilient incremental indexing with explicit skip reasons
- [ ] Reconcile stable Bridge `.sng` arrivals exactly once and map provider identity to
  Cantina song identity — [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Full-text search across title, artist, album, and charter
- [ ] Album-art selection, serving, and cache invalidation

## M3 · iPad client

- [ ] Resolve discovery, pairing, HTTPS/WSS, and firewall design —
  [#6](https://github.com/roguen/cantina/issues/6)
- [ ] Installable Home Screen experience and automatic reconnection
- [ ] One-handed browse and search with persistent connection state
- [ ] Show honest acquisition, validation, refresh, and play-next progress; direct
  Bridge search/download stays disabled until a supported external contract exists —
  [#17](https://github.com/roguen/cantina/issues/17)

## M4 · Control

- [ ] Cue a selected song through one replaceable control interface
- [ ] Refresh stock YARG, prove the exact imported song is visible, and fulfill
  play-next without interrupting an active song —
  [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Report bounded success and honest failure on the iPad
- [ ] Define Holocron and theater-contention behavior —
  [#8](https://github.com/roguen/cantina/issues/8)

## M5 · Queue and state

- [ ] Decide setlist durability and restart recovery —
  [#7](https://github.com/roguen/cantina/issues/7)
- [ ] Define proven, missing, unknown, and stale live-state fields —
  [#12](https://github.com/roguen/cantina/issues/12)
- [ ] Safe score-screen auto-advance with a cancel window
- [ ] Recover acquisition and play intent without duplicate install, setlist insertion,
  refresh, or late cue — [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Record measured end-to-end latency

## Beyond

Propose the smallest interface upstream to YARG only after working M1–M5 evidence shows
what stock YARG cannot provide.

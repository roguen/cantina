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
  [#9](https://github.com/roguen/cantina/issues/9). **Technically proven** on this host
  (D-019): the CI artifact launches with no runtime dependency, `/api/health` answers, it
  binds loopback only and refuses the LAN address, and its version carries the `main` SHA.
  Two things keep this open — .NET 10 does **not** list Windows 10 22H2 as supported, which
  is an accepted risk inherited from the brief rather than a fixable task; and **clean
  shutdown is unproven**, because a headless Barkeep has no console for Ctrl+C and no window
  for `taskkill`, so only a forced kill worked
- [x] Public-release gate — the repository was published on 2026-08-01 (D-011) before
  this gate's audit items were worked, so they ran retrospectively and completed on
  2026-08-28 (D-021) — [#10](https://github.com/roguen/cantina/issues/10). History is
  clean of secrets, song content, and GPL contamination across all refs including
  pre-rewrite ones; the platform now enforces secret scanning, push protection, private
  vulnerability reporting, Dependabot, and an Actions allowlist with required SHA
  pinning. The known residual: the 2026-08-02 rewrite's pre-rewrite commits remain
  reachable through advertised pull-request refs, accepted because the content is only
  the macOS bootstrap host table
- [x] Home for the living records settled — [#13](https://github.com/roguen/cantina/issues/13)
  closed as decided against. D-016 keeps them in `project/` permanently and rejects the wiki,
  which has no pull requests and no `Regression gate`
- [x] Protected `main` enforced by GitHub rather than a bypassable client-side hook —
  [#14](https://github.com/roguen/cantina/issues/14). Enabled on 2026-08-01 (Time Log
  session 005) and verified against the GitHub API: `Regression gate` required, strict
  up-to-date branches, `enforce_admins` on, force pushes and deletions refused,
  conversation resolution required

## M1 · Spike results

- [x] Capture and specify YARG 0.15's UDP stream —
  [#2](https://github.com/roguen/cantina/issues/2). Wire contract, broadcast destination,
  rate, the `Menu → Gameplay → Score` transition, and the byte 7 play state are captured
  and specified. Listener coexistence stays with
  [#11](https://github.com/roguen/cantina/issues/11)
- [ ] Prove a reliable stock-YARG input path —
  [#3](https://github.com/roguen/cantina/issues/3). `SendInput` with scan codes is proven to
  reach stock YARG from a background process (D-014), so no virtual controller and no kernel
  driver are needed. Still open: elevation mismatch, lock and logoff, held and repeated
  input, and visible bounded failure
- [ ] Prove or reject deterministic song selection —
  [#4](https://github.com/roguen/cantina/issues/4). Selection by typed query is **proven on
  the theater PC** (D-017): a query narrowed 652 songs to one and Enter selected it. The
  cost is that the search field cannot be focused from the keyboard, so the path needs a
  pointer click at a screen coordinate. Still open: a keyboard-only focus route, the
  metadata ambiguity in #33 that no query can resolve, and discovering the click target
  rather than hard-coding it
- [ ] Prove coexistence with the theater lighting consumer. Firewall and restart behaviour
  are now measured (D-020): reception works with the firewall enabled on all three profiles
  and **no allow rule**, two `SO_REUSEADDR` listeners each take every datagram, and a
  force-killed listener leaves no socket residue. What remains needs **YALCY or Photonics
  themselves**, neither of which is installed —
  [#11](https://github.com/roguen/cantina/issues/11). Socket semantics are captured on the
  theater PC: coexistence needs `SO_REUSEADDR` on both listeners, and YALCY does not set it
  (D-013). Still open: running the actual lighting application, and firewall-enabled
  behavior
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
- [ ] Report bounded success and honest failure on the iPad. The observation half is
  implemented: `src/Cantina.YargSession` (parser, latching, freshness, named faults) with
  `GET /api/live` serving the D-022 contract, validated on this host against live traffic
  and against YARG being absent
- [x] Define Holocron and theater-contention behavior —
  [#8](https://github.com/roguen/cantina/issues/8), closed by D-024 with the contention
  measured on this host. `docs/failure-behavior.md` is the normative contract: five
  observable readiness signals, cues fail closed with the failing signal named, pause
  attribution by the foreground sample at the transition, and players own recovery
  because the pause menu is a blind surface
- [ ] Specify the smallest upstream YARG interface the measured control gaps require,
  behind the existing replaceable adapter (D-010)

## M5 · Queue and state

- [ ] Decide setlist durability and restart recovery —
  [#7](https://github.com/roguen/cantina/issues/7). **Semantics decided (D-023)**:
  write-ahead at mutation time because D-019 proved shutdown hooks will not run;
  `ambiguous` recovery that never re-executes; JSON-lines journal + compacted snapshot,
  database rejected at theater scale. The issue stays open until the implementation
  passes the fixed crash matrix on this host
- [x] Define proven, missing, unknown, stale, and present-but-unpopulated live-state
  fields — [#12](https://github.com/roguen/cantina/issues/12), closed by D-022.
  `docs/live-state.md` is the normative contract: two trust-ordered sources, latched song
  identity, three-tier freshness with debounce, multi-sender `ambiguous`, and the
  advance-observation rule left neutral on #39
- [ ] Show only evidence-backed live state: scene, play state, and beat from the datagram,
  song identity and metadata from `currentSong.json`. No playback-position indicator ships
  without an upstream source (D-010, D-012)
- [ ] Decimate the ~90 Hz datagram stream and debounce the transient empty window that a
  song restart produces in `currentSong` (D-010)
- [ ] Specify the upstream observation interface for **playback position**, the only
  field no stock YARG surface exposes (D-010)
- [ ] Safe score-screen auto-advance on the `CurrentScene` byte, with a cancel window.
  **Now grounded in measurement (D-018): YARG does not auto-advance.** It waits on the score
  screen indefinitely — 180 s observed with no transition — and `CONTINUE` advances it in
  366 ms straight to gameplay, skipping instrument setup. So this bullet is about Cantina
  supplying an advance YARG lacks, not about racing one it already does.
  [#39](https://github.com/roguen/cantina/issues/39) settles whether Cantina presses that
  key or the players do; D-015 rejected it as "multi-step", which the measurement shows it
  is not
- [ ] Recover acquisition and play intent without duplicate install, setlist insertion,
  refresh, or late cue — [#17](https://github.com/roguen/cantina/issues/17)
- [ ] Record measured end-to-end latency

## Beyond

Two upstream contributions, in increasing order of difficulty.

**YALCY: set `SO_REUSEADDR` on the UDP intake.** One socket option. Without it, no second
consumer can share the host with YALCY in either startup order (D-013). YALCY is
LGPL-3.0-or-later like Cantina, so the change is directly contributable and does not depend
on anything Cantina ships first.

**YARG: broadcast playback position.** The interface specified in M4 and M5. D-010 moved the
*specification* work into those milestones because capability 4 cannot be met without it;
what remains here is the contribution itself, which depends on upstream timelines Cantina
does not control. Position is the only field no stock YARG surface exposes.

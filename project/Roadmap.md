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
- [x] Record a stock-YARG go/no-go decision — **go**, recorded as D-028 with the three
  things stock YARG cannot do named rather than worked around: no playback position, no
  menu-screen identity, and no keyboard route to the search field. The entry also names what
  would make it a no-go later — the click coordinate is the one element of the control path
  that cannot be verified before use, so a YARG update that moves the search box breaks
  selection silently. That is why a keyboard focus route is the first ask in
  `docs/upstream-interface.md`, ahead of the more valuable position field

## M2 · Library

- [x] Choose the authoritative metadata source —
  [#5](https://github.com/roguen/cantina/issues/5), closed by D-025: the filesystem
  (song.ini in the folders YARG itself reads), joined to observation by folder path, with
  YARG hashes learned rather than computed. Measured: 447/447 indexed in 89 ms, zero skips
- [x] Resilient indexing with explicit skip reasons — every unindexable folder reported
  by name (ini-unreadable, ini-missing-name-or-artist, duplicate-location,
  sng-metadata-not-yet-implemented, directory-missing); full rescan on demand at 89 ms
  for the whole library, so incremental machinery is deliberately absent until scale
  demands it
- [ ] Reconcile stable Geomitron Bridge `.sng` arrivals exactly once and map provider
  identity to Cantina song identity — [#17](https://github.com/roguen/cantina/issues/17)
- [x] Search across title, artist, album, and charter — `GET /api/songs`, plain
  substring ranked title > artist > album > charter (deliberately not fuzzy; D-017
  measured what fuzzy costs)
- [ ] Album-art selection, serving, and cache invalidation

## M3 · iPad client

- [x] Resolve discovery, pairing, HTTPS/WSS, and firewall design —
  [#6](https://github.com/roguen/cantina/issues/6), decided and implemented in D-026.
  `docs/lan-transport.md` is the normative contract: one explicit interface, a theater
  certificate authority signing a 397-day server certificate so rotation never touches the
  iPad, loopback-only pairing windows because physical presence is the authority, hashed
  bearer tokens with no cookies anywhere, single-use socket tickets, and a least-scope
  firewall rule Barkeep prints and never runs. `Cantina.SelfTest run lan` passed 8 of 8 on
  this host against a real LAN binding. Two things stay unproven and are named as such:
  whether iPadOS resolves this host by mDNS, and whether a second device can reach the
  ports without the firewall rule
- [x] Installable Home Screen experience and automatic reconnection — Barkeep serves the
  built client from `wwwroot` over the D-026 TLS binding, so the iPad installs the app from
  the theater PC itself and gets a secure context. The client holds its device token,
  renders a pairing screen when Barkeep says it has none, and buys a fresh single-use
  ticket for every socket connection, so waking from sleep reconnects without ceremony and
  without replaying a command. Still needs an actual iPad for the Add to Home Screen tap
- [x] One-handed browse and search with persistent connection state — the client renders
  the live stage banner from /ws/live with automatic reconnection and honest staleness
  copy, debounced search over /api/songs, per-song Cue and Add, the setlist with its
  cursor, and cue status including the fail-closed refusals in D-024 wording. Verified in
  a real browser against the live paused theater: the banner read "Paused: The
  Unforgiven" and a Cue click rendered "the game is paused; resume it on the pause menu"
- [ ] Show honest acquisition, validation, refresh, and play-next progress; direct
  Geomitron Bridge search/download stays disabled until a supported external contract
  exists — [#17](https://github.com/roguen/cantina/issues/17)

## M4 · Control

- [x] Cue a selected song through one replaceable control interface — implemented as
  `YargCueService` behind `IYargActuator`: the D-024 five-signal gate refuses with the
  failing signal named, the D-017 sequence actuates, the outcome is journaled two-phase
  (D-023) and resolved only when gameplay is observed with the requested hash. Proven
  unattended on this host by the SelfTest `cue` suite: staged, cued, players stood in,
  verified by outcome in 15 s
- [x] Refresh stock YARG, prove the exact imported song is visible, and fulfill
  play-next without interrupting an active song —
  [#17](https://github.com/roguen/cantina/issues/17), proven end to end on this host on
  2026-08-29 (D-030): a real Geomitron Bridge `.sng` download stabilized, validated against
  the real format, indexed (448 = YARG's own count, resolving D-025's 652 mystery as a
  stale cache), Scan Songs driven by pointer click, inserted next in the durable setlist,
  and cued to SELECT INSTRUMENT with the cue honestly `pending-players`. Folder arrivals
  and a repeatable SelfTest suite remain open on the issue
- [x] Report bounded success and honest failure on the iPad — the observation half is
  served two ways, `GET /api/live` and the decimated, change-driven `/ws/live` push feed per
  docs/live-state.md, and the command half (`/api/cue`, `/api/setlist`) answers with named
  refusals and honest pending states. The iPad renders all of it, verified in a real browser
  against the live paused theater: the banner read "Paused: The Unforgiven" and a Cue click
  rendered "the game is paused; resume it on the pause menu". Previously recorded:
  implemented: `src/Cantina.YargSession` (parser, latching, freshness, named faults) with
  `GET /api/live` serving the D-022 contract, validated on this host against live traffic
  and against YARG being absent
- [x] Define Holocron and theater-contention behavior —
  [#8](https://github.com/roguen/cantina/issues/8), closed by D-024 with the contention
  measured on this host. `docs/failure-behavior.md` is the normative contract: five
  observable readiness signals, cues fail closed with the failing signal named, pause
  attribution by the foreground sample at the transition, and players own recovery
  because the pause menu is a blind surface
- [x] Specify the smallest upstream YARG interface the measured control gaps require,
  behind the existing replaceable adapter (D-010) — `docs/upstream-interface.md`, ordered by
  what each ask costs YARG rather than by what Cantina wants most. The cheapest is not a
  protocol change at all: a keyboard route to the song search field would delete the screen
  coordinate D-017 left in the control path. Nothing in the product is conditional on any
  of it

## M5 · Queue and state

- [x] Score-screen auto-advance (#39, decided 2026-08-30: Cantina presses CONTINUE as
  well as the players) — D-034: players-first grace, one bounded press, then the
  ordinary cue pipeline for the next entry, cursor moved only on confirmed load. Armed
  from the iPad, off at startup. Target-PC acceptance run still owed for the
  which-menu-does-Score-dismiss-to assumption

- [x] Decide setlist durability and restart recovery —
  [#7](https://github.com/roguen/cantina/issues/7), closed. Semantics decided (D-023),
  implemented as `SetlistJournal` in Barkeep with `/api/setlist`, and the crash matrix
  **passed on this host with real process kills** via `tools/Cantina.SelfTest`: five
  racing kills (one landing in the intent-to-outcome window, recovered as exactly one
  ambiguous), crash-after-acknowledge fully durable, corrupt snapshot quarantined,
  restart intact. The cue-command ambiguity confirmation flow lands with the M4 cue
  pipeline under architecture.md's existing never-blindly-re-execute rule
- [x] Define proven, missing, unknown, stale, and present-but-unpopulated live-state
  fields — [#12](https://github.com/roguen/cantina/issues/12), closed by D-022.
  `docs/live-state.md` is the normative contract: two trust-ordered sources, latched song
  identity, three-tier freshness with debounce, multi-sender `ambiguous`, and the
  advance-observation rule left neutral on #39
- [x] Show only evidence-backed live state: scene, play state, and beat from the datagram,
  song identity and metadata from `currentSong.json`. No playback-position indicator ships
  without an upstream source (D-010, D-012) — `docs/live-state.md` is the contract, the
  tracker and `/api/live` implement it, and the client renders it. Position is specified as
  an upstream ask (`docs/upstream-interface.md` §3) and ships nowhere
- [x] Decimate the ~90 Hz datagram stream and debounce the transient empty window that a
  song restart produces in `currentSong` (D-010) — `LiveStateSocket` polls the tracker every
  250 ms and pushes only when a rendered field changed, plus a 5 s heartbeat; the tracker
  debounces freshness demotions by 1 s and re-latches after the clear. Measured on this
  host: delivered state is **p50 2.3 ms, p95 8.5 ms** old when it reaches the client
- [x] Specify the upstream observation interface for **playback position**, the only
  field no stock YARG surface exposes (D-010) — `docs/upstream-interface.md` §3: two
  unsigned 32-bit millisecond values in the datagram tail, integers rather than floats
  because a ~90.7 Hz latest-wins snapshot should not accumulate representation error across
  a five-minute song
- [ ] Safe score-screen auto-advance on the `CurrentScene` byte, with a cancel window.
  **Now grounded in measurement (D-018): YARG does not auto-advance.** It waits on the score
  screen indefinitely — 180 s observed with no transition — and `CONTINUE` advances it in
  366 ms straight to gameplay, skipping instrument setup. So this bullet is about Cantina
  supplying an advance YARG lacks, not about racing one it already does.
  [#39](https://github.com/roguen/cantina/issues/39) settles whether Cantina presses that
  key or the players do; D-015 rejected it as "multi-step", which the measurement shows it
  is not
- [x] Recover acquisition and play intent without duplicate install, setlist insertion,
  refresh, or late cue — the acquisition journal (D-030): lease before work, receipt at
  outcome, completed imports never rerun, crashed imports re-claimable because every step
  is idempotent, failed imports retried once per sweep, ambiguous ones held for eyes.
  Deterministic coverage in `AcquisitionTests`; the crash matrix with real kills remains
  future work on [#17](https://github.com/roguen/cantina/issues/17)
- [x] Record measured end-to-end latency — `Cantina.SelfTest run latency`, on the theater
  PC with YARG broadcasting. Steady state, first request reported separately because it
  carries JIT and connection setup: **search over 447 songs p50 0.5 ms / p95 4.1 ms**
  (first 27 ms); **setlist command round trip, including D-023's write-ahead flush to disk,
  p50 1.3 ms / p95 2.8 ms** (first 50 ms); **delivered-state age p50 2.3 ms / p95 8.5 ms**.
  Change latency is *derived, not measured* — bounded by the socket's 250 ms poll plus that
  age, so ≤ 263 ms at p95 — because measuring it directly needs a scene change, which needs
  input the suite does not send

## Beyond

Two upstream contributions, in increasing order of difficulty.

**YALCY: set `SO_REUSEADDR` on the UDP intake.** One socket option. Without it, no second
consumer can share the host with YALCY in either startup order (D-013). YALCY is
LGPL-3.0-or-later like Cantina, so the change is directly contributable and does not depend
on anything Cantina ships first.

**YARG: the asks in `docs/upstream-interface.md`.** The specification work is done and
lives there, ordered by what each ask costs YARG. What remains here is the contribution
itself, which depends on upstream timelines Cantina does not control. Position is the only
field no stock YARG surface exposes, but it is the third item rather than the first: a
keyboard route to the search field is a UI fix with no protocol at all, and it deletes the
screen coordinate that is currently the most fragile thing in Cantina's control path.

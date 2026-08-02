# Time Log

Entries are append-only and factual. Duration is recorded as unknown when the session
start was not captured.

## 2026-08-01 · Bootstrap session 001

- Recorded: 2026-08-01 15:39 CDT
- Duration: not captured
- Authenticated GitHub CLI as `roguen` and cloned the empty private repository.
- Reviewed the kickoff brief, YARG/YALCY network surfaces, Photonics as corroborating
  evidence, and Holocron's documentation conventions.
- Recorded decisions D-001 through D-006.
- Created M0–M5 milestones, repository labels, and issues #1–#14, including one direct
  issue for every kickoff-brief section 6 question.
- Added the LGPL-3.0-or-later license, normative documentation, contribution/security
  guidance, ASP.NET Core Barkeep host, React/TypeScript client, lockfiles, tests, and
  SHA-pinned CI.
- Local result: server format/build succeeded with zero warnings; two server tests and
  one client test passed; client lint/build passed; npm reported zero vulnerabilities;
  locked self-contained `win-x64` cross-publish succeeded.
- Pushed bootstrap commit `8e6c268` to `main`.
- Initial GitHub result: Ubuntu and Windows server jobs passed; the client job rejected
  an incomplete cross-platform npm lock graph; the artifact job was skipped.
- GitHub kept the wiki disabled and rejected protected-branch enforcement for the
  private repository on its current plan. Opened #13 and #14 and began the documented
  fallback on branch `codex/m0-records-and-ci`.
- Pull request #15 merged as `d9ef37d` after its push and pull-request CI runs passed.
- Main run 30717785137 passed the client, Ubuntu server, Windows server, and locked
  Windows publish jobs and uploaded a 49,884,381-byte `barkeep-win-x64` artifact.
- A post-merge branch-protection request returned HTTP 403 with GitHub's instruction to
  upgrade to Pro or make the repository public; the repository remained private.

## 2026-08-01 · Bridge requirement session 002

- Recorded: 2026-08-01 16:18 CDT
- Duration: not captured
- Inspected Geomitron Bridge 3.4.0, latest release 3.4.5, and upstream default-branch
  source for licensing, supported launch surfaces, configuration, download lifecycle,
  and YARG interoperability.
- Confirmed that Bridge exposes no supported external CLI, API, deep link, or OS-level
  IPC and that completed chart folders or `.sng` files are its viable handoff boundary.
- Recorded D-007, added the normative Bridge integration contract, and extended the
  architecture, security model, glossary, environment notes, and roadmap.
- Created issue [#17](https://github.com/roguen/cantina/issues/17) and cross-linked its
  refresh, exact-selection, identity, LAN-security, and recovery requirements into
  issues #3–#7.

## 2026-08-01 · Test harness session 003

- Recorded: 2026-08-01 16:39 CDT
- Duration: not captured
- Reconfirmed that GitHub rejects branch protection and repository rulesets for the
  private repository on its current plan; kept issue #14 open and did not change
  repository visibility.
- Created issue [#19](https://github.com/roguen/cantina/issues/19) and branch
  `codex/test-harness` before implementation.
- Added a deterministic semantic theater harness, application coordinator seams,
  scenario regression tests, a tracked direct-main push guard, and a stable CI
  `Regression gate` across server, client, repository-policy, and Windows artifact
  jobs.
- Recorded D-008 and documented the harness contract, safety boundary, workflow, and
  target-PC evidence limit.
- Independent review found and resolved concurrency-hang, claim-finalization, refresh
  ambiguity, cross-platform output, and locked-restore defects; the final review
  reported no remaining P0, P1, or P2 findings.
- Local result: locked restore and format passed; the Release build completed with zero
  warnings and errors; 20 server tests and all 14 harness scenarios passed; LF-only
  JSON validation passed; one client test, lint, and production build passed with zero
  npm vulnerabilities; the direct-main guard and workflow YAML checks passed; and the
  locked self-contained `win-x64` publish succeeded.

## 2026-08-01 · Windows working host and naming session 004

- Recorded: 2026-08-01
- Duration: not captured
- Reviewed the kickoff brief against the existing repository and reported that it
  described a greenfield state that no longer existed; M0 was already complete.
- Read YALCY's `UdpIntake` parser, the YARG DMX and Advanced wiki pages, and Photonics'
  setup documentation to establish the YARG network surface.
- Findings: YARG's datagram is its own format, header magic `0x59415247` on default port
  36107, and is not RB3E, which uses port 21070 and is an output in YALCY. It is a
  fixed-layout state snapshot of 49 bytes plus two per player. It carries an explicit
  scene byte, a better auto-advance trigger than the lighting cue, and it carries no
  song identity, no playback position, and no score value. The UDP and DMX cue
  enumerations use different values.
- Owner kept the existing `main`, made the repository public, and accepted the naming
  remediation.
- Recorded D-009 for Geomitron Bridge naming, D-010 for live-state scope and moving the
  upstream interface into M4 and M5, and D-011 for publication, superseding D-006.
- Added `docs/yarg-interface.md` as a provisional wire contract, explicitly pending
  capture in issue [#2](https://github.com/roguen/cantina/issues/2).
- Renamed `docs/bridge-integration.md` to `docs/geomitron-bridge-integration.md` and
  removed bare "Bridge" from current documents. Append-only Decision Log and Time Log
  history was left unchanged.
- Renamed harness fixture identities to `GeomitronBridge-00N.sng` and
  `geomitron-bridge-handoff`.
- Adopted the Windows 10 Pro 22H2 theater PC as the working host and installed .NET SDK
  10.0.302 and Node.js 24.18.1; the host previously carried only the .NET 8 runtime.
- YARG, YARC Launcher, and Geomitron Bridge are not installed on this host, so no M1
  spike could be run.
- Local result on Windows: locked restore, format verification, and a zero-warning,
  zero-error Release build passed; 20 server tests passed; all 14 harness scenarios
  passed; the client audited 64 packages with zero vulnerabilities and passed its test,
  lint, and production build.

## 2026-08-01 · YARG capture session 005

- Recorded: 2026-08-01
- Duration: not captured
- Enabled branch protection on `main`: required `Regression gate` check with strict
  up-to-date branches, pull request required with zero approvals, `enforce_admins` on,
  force pushes and deletions blocked, conversation resolution required.
- Corrected an earlier error: YARG and YARC Launcher are installed on this host. A prior
  filtered directory listing rendered blank rows and was misread as no match.
- Located YARG's per-channel directory, `settings.json` with `DataStreamEnable`, the song
  cache, and the `currentSong` files. Configured song sources are an existing Clone Hero
  library.
- Built `spikes/Cantina.Spikes.YargObserve` and opened
  [#22](https://github.com/roguen/cantina/pull/22). Fixed two defects found in use: the
  transcript path crashed when its git-ignored parent directory was absent, and the
  console summary truncated `currentSong.json` below its full length.
- Opened [#23](https://github.com/roguen/cantina/issues/23) for launching, restarting, and
  monitoring the YARG process, which the kickoff brief did not scope.
- Captured three runs against YARG 0.15 stable, two of 300 seconds. Accepted 54,000+
  datagrams with zero rejections.
- Findings: version-3, 47-byte datagram broadcast to `255.255.255.255:36107` at ~90.7 Hz;
  no per-player star power at this version; `Menu → Gameplay → Score` observed, proving
  the auto-advance trigger; cue 30 at menu and 31 at score, confirming the UDP ordinal
  table against the different DMX values; `currentSong.json` populates with a stable
  content hash and path, leading the scene by 37–400 ms on start and clearing 86 ms after
  `Score`; a song restart produces a 256 ms empty window.
- Revised D-010 before merge rather than superseding it after. Its premise that an
  upstream hook was needed for song identity was disproved; playback position is now the
  only structural gap.
- Rewrote `docs/yarg-interface.md` from provisional to capture-backed.
- Unresolved and holding [#2](https://github.com/roguen/cantina/issues/2) open: byte 7,
  named `PauseState` upstream, read `true` for all 234 seconds of gameplay and `false` at
  menu and score. A deliberate pause and unpause during gameplay is still required.

## 2026-08-01 · Byte 7 resolution session 006

- Recorded: 2026-08-01
- Duration: not captured
- Merged [#21](https://github.com/roguen/cantina/pull/21) and
  [#22](https://github.com/roguen/cantina/pull/22), returning `main` to a clean state with
  branch protection enforced and the full regression green locally and in CI.
- Extended the spike so the byte 7 question could be decided at all: raw unnamed byte
  reporting, operator marks that freeze and diff the whole datagram, and per-offset change
  tracking across all 47 bytes. Merged as [#24](https://github.com/roguen/cantina/pull/24).
- Withdrew the operator-mark instruction before the run. Marking requires focusing the
  terminal, and alt-tabbing out of a fullscreen game can itself pause it, which would have
  confounded the measurement.
- Captured a fourth run with two deliberate pause and unpause cycles.
- **Byte 7 resolved as a three-state play state**: `0` no song, `1` playing, `2` paused.
  Transitions were clean at both pauses and both unpauses.
- Corrected a self-inflicted error. The earlier claim that captures contradicted YALCY's
  `PauseState` name was wrong: YALCY reads the offset as a byte, and this project's parser
  coerced it to `byte != 0`, collapsing Playing and Paused. A Cantina parsing defect had
  been documented as an upstream quirk.
- Recorded D-012, updated `docs/yarg-interface.md`, and closed
  [#2](https://github.com/roguen/cantina/issues/2). Listener coexistence remains open in
  [#11](https://github.com/roguen/cantina/issues/11).
- Local result: zero-warning Release build, clean format, 20 server tests, 14 harness
  scenarios, and a live run rendering `play=NoSong` on the score screen.

## 2026-08-01 · Listener coexistence session 007

- Recorded: 2026-08-01
- Duration: not captured
- Merged [#25](https://github.com/roguen/cantina/pull/25); issue
  [#2](https://github.com/roguen/cantina/issues/2) closed.
- Confirmed no lighting application is installed on the theater PC: no YALCY, Photonics,
  Lightjams, or QLC+. YARG's own settings show `StageKitEnabled` true and `DMXEnabled`
  false.
- Added `--no-reuse`, which reproduces YALCY's bare `new UdpClient(port)` bind so a second
  instance can stand in for a lighting consumer, and made a failed bind report a named
  error instead of crashing.
- Ran three two-process tests against live YARG traffic, both startup orders: no-reuse
  first then reuse fails with `AccessDenied`; reuse first then no-reuse fails with
  `AddressAlreadyInUse`; both with `SO_REUSEADDR` bind and both receive every datagram at
  about 90.6/s.
- Finding: coexistence requires `SO_REUSEADDR` on both listeners, startup order is not a
  workaround, and YALCY does not set it. Barkeep and YALCY cannot currently share the
  theater PC, and no change confined to Cantina can fix it.
- The failure is loud rather than silent; the bind throws, so Barkeep can name the fault.
- Recorded D-013 and added a second, far more tractable upstream target: one socket option
  in YALCY, which is LGPL like Cantina.
- Issue [#11](https://github.com/roguen/cantina/issues/11) stays open. The stand-in
  reproduces YALCY's bind but is not YALCY, and firewall-enabled behavior is untested.

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
- Pushed the bootstrap commit to `main`.
- Initial GitHub result: Ubuntu and Windows server jobs passed; the client job rejected
  an incomplete cross-platform npm lock graph; the artifact job was skipped.
- GitHub kept the wiki disabled and rejected protected-branch enforcement for the
  private repository on its current plan. Opened #13 and #14 and began the documented
  fallback on branch `codex/m0-records-and-ci`.
- Pull request #15 merged after its push and pull-request CI runs passed.
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

## 2026-08-02 · Host-reference cleanup and history rewrite session 008

- Recorded: 2026-08-02
- Duration: not captured
- Audited the repository for references to the owner's employer and to non-target
  development hosts. Found no occurrence of the employer name in any file or any commit,
  and confirmed every commit author and committer identity is a personal GitHub noreply
  address.
- Removed non-target host references from `AGENTS.md`, `project/Environment.md`, and
  `project/Working-Agreement.md`, merged as pull request
  [#28](https://github.com/roguen/cantina/pull/28).
- Deleted four merged branches that were still present on the remote:
  `codex/bridge-acquisition-contract`, `codex/m0-closeout`, `codex/m0-records-and-ci`, and
  `codex/test-harness`. These were independently reachable refs carrying superseded
  content; only `main` now exists on the remote.
- Rewrote all 30 commits with `git filter-branch`, restricted to `*.md` so dependency
  lockfiles could not be altered. Verified afterwards that no reachable blob retains the
  removed strings and that `package-lock.json` is byte-identical, preserving the
  cross-platform lock graph and its 25 platform-binary entries.
- Purged `refs/original`, expired the reflog, and ran `git gc --prune=now` before pushing.
- Branch protection was removed for the force push and restored immediately afterwards;
  the restored settings were re-read and confirmed. The tracked pre-push hook was bypassed
  once with `--no-verify`, which a history rewrite cannot avoid.
- Kept a full pre-rewrite bundle of every ref outside the repository as a recovery point.
- Repaired references to commit hashes that the rewrite invalidated, and corrected the
  working agreement, which still described branch protection as unavailable.
- Limitation recorded honestly: superseded commits remain retrievable by hash from GitHub
  until it garbage-collects them, and merged pull requests still reference them. Removing
  them completely would require contacting GitHub Support, which is out of scope.

## 2026-08-02 · Control input session 009

- Recorded: 2026-08-02
- Duration: not captured
- Confirmed ViGEmBus is archived, last pushed November 2023, so a virtual controller fails
  issue #3's requirement of a maintainable installation and redistribution story. Tested
  `SendInput` first on that basis, reversing the kickoff brief's expected ranking.
- Found `PauseOnFocusLoss` set to true in YARG's settings, which forbids any control path
  that takes foreground.
- Built the input spike with the datagram as its oracle, so a key that lands is proven by a
  state transition rather than judged by eye.
- A first run returned no state change for Escape. That result was later withdrawn: two
  YARG instances were broadcasting to the same port from different source ports, and the
  oracle was interleaving two games into a state belonging to neither.
- The settle guard added before that run is the only reason the false result was never
  recorded as a finding. Two reporting defects were fixed alongside: the summary asserted
  that injections had been accepted even when every key was skipped and nothing was sent,
  and a failed settle reported nothing about what it had seen moving.
- After closing the extra instance, Escape moved the game from `Playing` to `Paused`,
  injected from a background process while YARG held foreground. Enter had no effect during
  gameplay, which is expected for a menu key.
- Recorded D-014. `SendInput` with scan codes is the control mechanism; no kernel driver, no
  elevation, no third-party dependency.
- End-to-end latency remains unmeasured. The reported `0 ms` is taken after a 60 ms key hold
  and only shows the change was visible on the first poll; M5 owns the real figure.
- Issue [#3](https://github.com/roguen/cantina/issues/3) stays open for elevation mismatch,
  lock and logoff, held and repeated input, and visible bounded failure.

## 2026-08-14 · Record correction session 010

- Recorded: 2026-08-14
- Duration: not captured
- Merged pull request [#36](https://github.com/roguen/cantina/pull/36), recording D-015,
  and verified the post-merge `main` run green across all six jobs.
- Audited every living record and normative document against observable host state and
  committed evidence, then verified each candidate finding independently before acting.
  Opened issue [#37](https://github.com/roguen/cantina/issues/37).
- **Supersedes session 004**, which recorded that "YARG, YARC Launcher, and Geomitron
  Bridge are not installed on this host, so no M1 spike could be run." All three were
  already installed when that line was written. Session 005 corrected the YARG and YARC
  Launcher half; the Geomitron Bridge third was never corrected anywhere until now.
  `C:\Program Files\Bridge\Bridge.exe` reports `ProductName` `Bridge`, `CompanyName` `Geo`,
  version `3.4.5.0`.
- The installed Geomitron Bridge is **3.4.5**, not the brief's 3.4.0. Recorded in
  `Environment.md`; issue [#17](https://github.com/roguen/cantina/issues/17) owns pinning
  the tested behavior. Cantina has not exercised it, so the version is observed, not
  tested.
- Corrected `docs/architecture.md`, which stated that Barkeep "knows the current song only
  when it cued that song itself, reports it as unknown otherwise" — the exact position
  D-010 rejected once the capture found `currentSong.json`. The same file already said the
  opposite six lines earlier. A normative contract contradicting both itself and a merged
  decision is the most serious of this session's findings.
- Corrected four descriptions of `docs/yarg-interface.md` as provisional. It has recorded
  itself as confirmed by capture since D-010.
- Verified branch protection is live on `main` against the GitHub API and checked the M0
  Roadmap box; issue [#14](https://github.com/roguen/cantina/issues/14) is resolvable.
- Confirmed the wiki is still uninitialised — `git ls-remote` on `cantina.wiki.git` returns
  "Repository not found" — so issue [#13](https://github.com/roguen/cantina/issues/13)
  stays open and `project/` remains the correct fallback. The stale part was the *reason*
  given for it, which cited a private-repository limitation that D-011 removed.
- Left Roadmap M5's score-screen auto-advance bullet unchanged. It is ambiguous against
  D-015 rather than provably false, and settling it is a scope decision. Recorded in #37.
- The two open hands-on spikes were not run: whether YARG's setlist auto-advances, and
  whether typed text reaches the song-list search field under `--vk --type-only`. Both need
  an operator at the machine, and the second is only observable on screen.
- Merged pull request [#38](https://github.com/roguen/cantina/pull/38) and verified the
  post-merge `main` run green across all six jobs. Closed issue
  [#14](https://github.com/roguen/cantina/issues/14), recording the enforced protection
  settings as verified against the GitHub API.
- Opened issue [#39](https://github.com/roguen/cantina/issues/39) for the score-screen scope
  question. It had been raised inside #37, which the merge auto-closed — a defect in how
  that pull request was worded, since a record-correction change should not have closed an
  issue still holding an open question.
- Retitled and rewrote issues #10 and #13, both of which still asserted the repository was
  private. #10's audit items are retrospective rather than preventive now: anything the
  history audit finds must be treated as already disclosed and rotated, not merely removed.
- Recorded **D-016**: the living records stay in `project/` permanently and the wiki
  migration is rejected. The deciding argument is that records in the repository are
  reviewed through the same pull request and `Regression gate` as the code they describe,
  which is exactly how this session's two most serious findings were caught. A wiki has no
  pull requests and no required checks, so the append-only rule would rest on discipline
  alone. Issue #13 closed as decided against; Roadmap M0 loses that gate.

## 2026-08-27 · Unattended selection spike session 011

- Recorded: 2026-08-27
- Duration: not captured
- Built the observation harness that makes a spike runnable with nobody at the machine:
  `--focus-yarg` brings YARG forward and verifies it by reading `GetForegroundWindow`, and
  `spikes/observe-screen.ps1` captures the screen, which is the only oracle for anything
  the datagram cannot express. YARG was launched from this session; it had exited since the
  previous one.
- **Answered #4's narrowed question.** Selection by typed query works: `unforgiven` narrowed
  652 songs to one, and Enter selected *The Unforgiven* by Metallica. Recorded as **D-017**.
- **The 2026-08-03 diagnosis was wrong.** That run blamed the injection shape and built
  `--vk` on the theory that named keys and text characters travel different paths. The real
  cause is that YARG's search field cannot be focused from the keyboard. With a pointer
  click first, the identical injection succeeds; without it, the identical injection fails
  and earlier text survives 40 backspaces untouched. Tab does not focus it either.
- Windows accepted **every** injection in **every** run, including the failures, which is
  what made the earlier conclusion detectable as unsound rather than merely unlucky.
- Two reporting defects fixed, both of which produced a confident wrong answer during this
  session before being corrected. Select mode discarded `SendInput`'s return value, so a
  silent search box could not be told apart from a refused injection. And the no-change
  summary asserted YARG was ignoring input, when the datagram simply cannot see a move
  inside one scene — Enter navigated the start menu into the song list while the summary
  called it ignored.
- Corrected the session date. `date` reported 2026-08-14 earlier in this working session and
  2026-08-27 now; the records written under the earlier reading landed on 2026-08-15 and are
  left as written, since they were accurate when recorded.
- **Answered the setlist auto-advance question and recorded D-018.** YARG does not
  auto-advance: 180 s on the score screen with no transition, 39,799 datagrams, one sender,
  no input of any kind. `CONTINUE` then advanced it in 366 ms straight to gameplay with the
  second queued song, skipping instrument setup.
- Built `spikes/Cantina.Spikes.YargSetlist` for it. The design was attacked by adversarial
  review *before* being built rather than after, which is the reverse of this project's two
  previous wrong conclusions. That review earned its place three times over: it identified
  that the on-screen counter read as a setlist size is a **star counter** (652 x 5 = 3260);
  that `settings.json` carries `PlayAShowTimeout: 10.0`, so a short observation window would
  have proved nothing; and that the datagram is a **global broadcast**, so any YARG on the
  LAN reaches the listener and two senders decode as exactly the Score/Gameplay churn an
  auto-advance produces.
- The harness links no `SendInput`, `keybd_event`, `mouse_event`, or `SetForegroundWindow`,
  so its non-interference is a property of the assembly rather than a claim in a log.
- Three defects found, two of them in the run itself. The harness read `"Hash"` from
  `currentSong.json`, whose shape is `{"Hash":{"HashBytes":...}}`, so it returned the inner
  key's *name* — constant across songs, which would have silently suppressed the
  identity-change line the measurement depends on. The maximum datagram gap was 538 ms
  against a 250 ms bar. And binding the UDP socket raised a Windows Defender Firewall prompt
  for the harness that sat on screen throughout; it never took foreground, but under
  `PauseOnFocusLoss` a dialog that can steal focus is a contamination risk.
- The run was initially **unsound and was rescued after the fact**. A one-song setlist
  produces an identical never-advancing score screen, and arming was never verified before
  the window opened. `END SETLIST` on the score screen and the second song actually loading
  on `CONTINUE` are what closed it. A repeat must verify arming first.
- Confirmed D-015's account of instrument setup: per-player, one confirmation each, and it
  warned that a configured player had no input device assigned.

## 2026-08-28 · Handoff structure and Windows artifact session 012

- Recorded: 2026-08-28
- Duration: not captured
- Merged pull request [#41](https://github.com/roguen/cantina/pull/41) carrying D-017 and
  D-018; post-merge `main` green on all six jobs.
- Built the two things that were making every session start from scratch:
  `.claude/skills/yarg-observation/` in the repository, and a `cantina-agent/` sibling folder
  outside it holding operating rules and live state. The split is deliberate — durable
  reviewable knowledge stays under the same pull request as the code (D-016), while
  per-session state that goes stale within the hour stays out of the diff (`force-bond`).
- **Ran issue #9's smoke test and recorded D-019.** The `barkeep-win-x64` artifact from
  `main` at `11096cc` launches on this host with no runtime dependency, answers
  `/api/health`, binds `127.0.0.1` and `::1` only, refuses `192.168.68.144`, and carries the
  `main` SHA in its version string.
- **The policy half disagrees with the technical half**, which is why #9 asked for them
  separately. .NET 10's supported-OS table lists Windows 10 client as 21H2 (E), 21H2 (IoT),
  1809 (E) and 1607 (E); **22H2 does not appear**, and the policy states that OS versions out
  of support by the publisher are not tested or supported. The only ESU carve-out named is
  Windows Server 2012/2012 R2. Changing the .NET version fixes nothing, because the
  constraint is the OS, so this is an accepted risk inherited from the brief.
- **Clean shutdown is not proven, and that is a finding.** The artifact advertises Ctrl+C,
  but headless it has no console to receive one and no window for `taskkill` without `/F`.
  Only a forced kill worked. Nothing leaked — the process exited and port 5273 released —
  but a Barkeep that can only be killed bears on setlist durability (#7) and process
  supervision (#23).
- Merged pull requests [#42](https://github.com/roguen/cantina/pull/42) and
  [#43](https://github.com/roguen/cantina/pull/43); #43 needed a branch update first because
  protection requires branches to be current. Post-merge `main` green on all six jobs.
- **Advanced issue #11 and recorded D-020.** Two of its untested checklist items are now
  measured. With the firewall enabled on all three profiles and **no allow rule for any
  Cantina or YARG binary**, two `SO_REUSEADDR` listeners each accepted ~2650 datagrams with
  zero rejections at ~90.8/s. YARG sends from `192.168.68.144:61374` to `255.255.255.255`,
  so the traffic is a LAN broadcast rather than loopback.
- Force-killing one listener left the other completely unaffected — 3962 datagrams across
  the sequence, zero rejected — and a fresh listener rebound in 0.029 s while the survivor
  still held the port. An ungraceful kill leaves no socket residue.
- That makes the Windows Defender prompt seen during the D-018 run a **needless** risk as
  well as a contamination risk: the answer is always to decline, because reception works
  without a rule. It also narrows D-019's clean-shutdown gap — what is at risk on a forced
  stop is application state, not the socket, which puts the rest of that problem in #7.
- #11 stays open for the one item that needs software this host does not have: running
  against YALCY or Photonics themselves. D-013's conclusion is unchanged.
- Merged pull request [#44](https://github.com/roguen/cantina/pull/44). The owner set a
  standing instruction for the session: work autonomously with as little input as
  possible, which extends to running the branch, pull request, and merge loop directly.
- **Ran the retrospective public-release audit and recorded D-021**, closing issue #10.
  Five surfaces, every finding independently re-verified, two findings refuted in
  verification. History is clean of credentials, emails, song content, packet captures,
  and GPL text across all 227 blobs in all 66 commits, including the 29 pre-rewrite
  commits.
- **The 2026-08-02 history rewrite never removed its content from public reach.** GitHub
  still advertises `refs/pull/15/head`–`refs/pull/28/head`, so the pre-rewrite chain is
  enumerable without knowing a SHA. Verified line-by-line: the exposed content is only
  the macOS bootstrap host table. Accepted rather than escalated to GitHub Support, and
  the Working Agreement's rewrite section now records that a rewrite alone is
  presentation, not removal.
- Enabled the platform enforcement the repository's rules assumed: secret scanning, push
  protection, private vulnerability reporting (which `SECURITY.md` had been pointing at
  while it was disabled), Dependabot alerts and security updates, and an Actions policy
  of GitHub-owned only with required SHA pinning. Fixed the repository description that
  still called Barkeep a "bridge" against D-009.
- Repo fixes: SPDX headers on the last two source files, `COPYING.GPL` beside the LGPL
  `LICENSE`, `CODE_OF_CONDUCT.md` and `SUPPORT.md` written plainly for a one-maintainer
  project, CI push trigger filtered to `main` so PR branches build once, and the public
  artifact stops shipping `Cantina.Barkeep.pdb` and `appsettings.Development.json`.
- Merged pull request [#45](https://github.com/roguen/cantina/pull/45) and caught a
  self-inflicted verification error doing it: the first "post-merge green" I read was the
  *previous* merge's run, and the artifact size I compared was the pre-strip one. The
  actual run for `7b44358` was then waited on properly — green on all six jobs — and the
  new artifact was downloaded and verified by contents: 337 files, no `.pdb`, no
  `appsettings.Development.json`, `appsettings.json` intact.
- **Recorded D-022 and closed issue #12** by publishing `docs/live-state.md`: two
  trust-ordered sources with disagreements surfaced as `ambiguous`, latched song identity
  around the ~86 ms clear, three-tier freshness with a 1 s debounce sized by the observed
  538 ms healthy-run gap, multi-sender defence named after the interleaving that produced
  a withdrawn finding, and an advance-observation rule that stays neutral on #39.
- Merged pull request [#46](https://github.com/roguen/cantina/pull/46), post-merge run
  verified against the merge SHA this time rather than whatever run was newest — the
  procedural fix for the verification error caught earlier this session.
- **Recorded D-023**, the durability semantics for #7: write-ahead at mutation time
  because D-019 proved graceful shutdown does not exist on this host; recovery marks
  un-outcomed intents `ambiguous` and never re-executes; JSON-lines journal plus
  compacted snapshot with a database rejected at theater scale. The crash matrix that
  closes #7 is fixed in the entry and lands with the implementation. #23 inherits the
  deliberate-shutdown question with the correctness pressure removed.
- **Measured theater contention and recorded D-024, closing issue #8.** Another window
  taking foreground mid-song pauses the game with no key behind it; focus regain does
  not resume — the pause survived four verified regains, and three spike comments plus
  the observation skill had recorded the opposite mechanism as fact. All four corrected.
  Holocron launched fullscreen with audio takes foreground (silently pausing YARG), and
  both processes ran concurrently without either failing. The datagram never stopped:
  fourteen hours at full rate backgrounded and paused, sagging to 74–81/s with zero gaps
  under Holocron's GPU load.
- **A blind Enter nearly cost the setlist.** Stray Escapes had parked the pause-menu
  cursor on BACK TO LIBRARY; only the pre-confirm screenshot rule caught it. The pause
  menu is now a named confound in the observation skill, and D-024 assigns recovery to
  the players for exactly this reason.
- Published `docs/failure-behavior.md`: five readiness signals, fail-closed cues with the
  failing signal named to the iPad, and the coordination contract with Holocron reduced
  honestly to observing who has the screen.
- **Started M2 implementation: the YARG session listener is a real Barkeep component.**
  `src/Cantina.YargSession` is the dependency-light parser project architecture.md called
  for — the capture-proven `YargDatagram` promoted out of the observe spike, with both
  spikes now referencing the shared copy so no second parser can drift (D-012's trap).
  Added `CurrentSongDocument` (nested `HashBytes`, empty-is-a-value), `LatchedSong`, and
  `YargSessionTracker` implementing D-022: latched identity with the 5 s menu-dwell clear,
  Live/Stale/Dead freshness with the 1 s demotion debounce, multi-sender and port-conflict
  named faults. Barkeep hosts a `SO_REUSEADDR` UDP listener (bind failure = named fault,
  not retried, per D-013), a 25 ms `currentSong.json` poller, and `GET /api/live`.
- Seventeen new deterministic tests; 38 total plus the 14 harness scenarios, format and
  locked restore clean. Integration tests disable the real I/O services so the pipeline
  under test receives only fed bytes (D-008).
- Smoke-tested on this host against reality, in both directions: with YARG absent the
  endpoint reported `Dead`/`NoDatagrams` honestly — YARG had exited on its own again,
  which turned the smoke test into an accidental validation of the named-fault path — and
  with YARG relaunched it reported `Live`/`None` with 4,122 datagrams accepted, zero
  rejected, tracking boot (`Unknown`) into `Menu`.
- The theater PC's LAN address changed overnight — the sender is now a different
  192.168.68.x than every prior capture. D-021's rule against recording the literal
  address proved itself within a day; the multi-sender defence correctly treats this as
  one sender, since the set is per-run.
- **Implemented the D-023 journal and closed issue #7.** `SetlistJournal` in Barkeep:
  write-ahead JSON-lines with flush-to-disk before acknowledgement, compacted snapshot
  with atomic replace, torn-tail and corrupt-snapshot quarantine, ambiguous recovery that
  is itself journaled so a second crash replays identically, and idempotency by
  client-supplied command id surviving compaction. Surfaced as `GET /api/setlist` and
  `POST /api/setlist/commands`, with replays answered from the journal as 200s.
- **Built `tools/Cantina.SelfTest`, the target-PC acceptance harness the owner asked
  for.** Suites: `journal` (the D-023 crash matrix with real process kills — a child
  races appends and is killed mid-flight), `live` (Cantina.YargSession against the real
  broadcast), `readiness` (the D-024 signals, read-only). House verdicts: PASS, FAIL, or
  INCONCLUSIVE with a named cause; the tool links no input APIs.
- **The crash matrix passed on this host.** Five racing kills at staggered offsets —
  one landed in the window between intent flush and outcome flush and recovered as
  exactly one ambiguous — plus crash-after-acknowledge (5 acknowledged, 5 recovered),
  corrupt-snapshot quarantine, and restart. The live suite passed with 273 datagrams and
  zero rejections; YARG had exited on its own again (third occurrence) and the first run
  said INCONCLUSIVE YARG-GONE rather than failing, which is the harness working.
- 47 tests total now, plus the 14 harness scenarios; format and locked restore clean.
- **Built the cue pipeline and proved it unattended, closing the loop the product exists
  for.** `YargCueService` behind `IYargActuator`: the D-024 five-signal gate in front of
  the D-017 sequence, journaled two-phase per D-023 — pending until gameplay is observed
  with the requested hash, superseded cues resolved ambiguous by name, mismatches failed
  naming what actually loaded. A cue cannot confirm synchronously because instrument
  setup belongs to the players (D-015) and `currentSong.json` populates only at gameplay,
  so `pending-players` is a first-class state and a poller confirms.
- **The SelfTest `cue` suite paid for the whole harness in one afternoon.** Three
  unattended runs, each miss diagnosed by screenshot and probe rather than assumption:
  run 1 landed in PRACTICE because the start-menu cursor is invisible sticky state (the
  suite now stages from a screen-verified start menu); run 2 and 3 exposed a real tracker
  bug — `currentSong.json` populates during the load screen, ~2 s before gameplay
  datagrams, so the latch landed inside a stale menu dwell, was cleared, and the leftover
  change-detection hash blocked every re-latch of the same song. That is the
  replay-the-same-track case a theater hits nightly, invisible to every unit test, caught
  only by the full loop against the real game. Fixed with a regression test; run 4:
  **PASS in 15 seconds** — staged, cued "The Unforgiven" by search, players stood in,
  gameplay observed with the exact requested hash, song paused in cleanup.
- 58 tests plus the 14 harness scenarios; the `cue` suite is deliberately not part of
  `run all` because it sends input and starts a real song.
- Added the `/ws/live` WebSocket push feed: the same live-state projection, decimated
  from the 90.7 Hz wire to change-driven frames (scene, play state, song, freshness,
  fault) plus a 5 s heartbeat, per docs/live-state.md's decimation rule. Covered end to
  end by a deterministic test feeding the tracker and reading frames. 59 tests total.
- **Recorded D-025 and closed issue #5.** The filesystem is the metadata source — the
  same `SongFolders` YARG's settings name — with the folder path as the join key between
  index and observation, and YARG's content hash learned from `currentSong.json` rather
  than computed. `SongIndex` + `GET /api/songs` + `POST /api/library/rescan`; search is
  plain substring, ranked, deliberately not fuzzy. Live result on this host: 447 of 447
  folders indexed in 89 ms with zero skips, and the learned-hash join fired within a
  minute of startup because YARG sat paused on The Unforgiven. 66 tests total.
- **Built the iPad client's working core (M3).** The live stage banner from `/ws/live`
  with reconnect and honest staleness copy, debounced search over `/api/songs` with
  charter color tags stripped at display, per-song Cue and Add-to-setlist, the setlist
  view with cursor and recovery notice, and cue status following `pending-players` to
  resolution. Verified in a real browser against the live theater: the banner showed
  "Paused: The Unforgiven — Metallica" from the wire, search narrowed 447 to 1, and a
  Cue click rendered the fail-closed refusal in D-024's exact wording — "the game is
  paused; resume it on the pause menu". 5 client tests; lint and build clean.
- **Read the next instance in and updated the skills.** Added
  `.claude/skills/cantina-codebase/`: what each project owns, which decision each
  component implements, the regression loop, and the three build facts that only fail in
  CI (solution entry, `RuntimeIdentifiers` for the locked publish restore, committed
  lockfile). Updated `yarg-observation` with the day's new hazards — `currentSong.json`
  populating during the load screen ~2 s before gameplay datagrams, and sticky invisible
  menu cursors — plus how to run the product against the real game. Corrected `AGENTS.md`,
  which still described a project with no YARG integration and pointed durability at the
  now-closed #7.
- **Spun up a fresh instance to audit the handoff, and it found ten gaps.** Given only the
  onboarding path and no session context, it reported back correctly on the project's
  state and next action — and then found: `cantina-agent/AGENTS.md` naming "D-001 to
  D-018" against a log at D-025 and omitting the new `cantina-codebase` skill;
  `current.md` listing issue #5 as not-started three lines above the entry recording its
  closure; the 652-vs-447 library discrepancy stated as two different facts in two
  documents and reconciled only in D-025's last paragraph; `docs/development.md` never
  learning that `Cantina.SelfTest` exists; the five readiness signals of
  `failure-behavior.md` against four checked in `Gate()`; `Acquisition/` present, unwired,
  and unmapped; no expected test count anywhere; a missing `npm ci`; an incomplete
  endpoint list; and a merge-authority rule ("unless he has asked, **in this session**")
  that a fresh instance cannot satisfy by construction. All fixed.
- **One of those was my own unverified claim.** An earlier edit to `docs/development.md`
  silently failed to match its anchor and I reported it applied from the script's echo
  rather than from the file. The edit scripts now throw on a missing anchor, and every fix
  in this change was verified by grep afterwards rather than by its own success message.
- **The state block in `current.md` is now generated, not written.** `refresh-state.js`
  emits HEAD and branch, `origin/main`, open PRs, **branches not merged with no PR open**,
  open issues, and the decision count. Every stale fact the audit found was one a command
  gets right and prose gets wrong; the project's own rule against second copies applies to
  its own handoff. The generator immediately flagged the unmerged branch this very change
  was sitting on.

## 2026-08-28 · LAN transport and pairing session 013

- Recorded: 2026-08-28 20:00 PDT
- Duration: not captured
- Took the #6 unit: Barkeep leaving loopback. Measured the host before designing anything —
  `HOME-GRIFFEN-PC` at `192.168.68.54/24` by DHCP with no reservation, and a Tailscale
  interface alongside the Ethernet one. That second address is the argument the design
  needed: `IPAddress.Any` would have published the theater control surface to a network the
  operator is not standing on.
- Implemented D-026: explicit single-interface binding, a theater certificate authority
  signing a 397-day server certificate, loopback-only pairing windows with a single-use
  code, hashed bearer tokens, single-use WebSocket tickets, one access middleware deciding
  origin/transport/credential in that order, host filtering computed from the real binding,
  partitioned rate limits, and the least-scope firewall rule printed but never run.
- Added 22 server tests (66 → 88) and a `Cantina.SelfTest run lan` suite. Ran Barkeep on
  the LAN on this host and the suite **passed 8 of 8** against real listeners and a real
  TLS handshake the client validated by name and by chain against the served authority,
  with the machine's own root store taking no part in the decision.
- **The mDNS measurement was wrong and the screen is what caught it.** A query for this
  host's `.local` name drew zero answers in six seconds, which read as a finding. A
  screenshot taken for an unrelated pre-check showed a Windows Defender dialog reporting it
  had blocked PowerShell on all public and private networks — so the probe had been
  measuring the firewall, not mDNS. The trap is the one this project keeps meeting:
  something the tool could not see was doing the work. Recorded in D-026 as
  `INCONCLUSIVE` with the cause named, and the design depends on mDNS for nothing.
- **Left alone deliberately:** the firewall dialog is still on screen. "Allow access" and
  "Cancel" sit about a hundred pixels apart at 3840×2160, and a mis-click would have
  granted firewall access, which is the owner's decision and not one worth a coordinate
  gamble. YARG was found at `QUICKPLAY PAUSED` with the cursor on `BACK TO LIBRARY` — the
  blind-menu state the observation skill warns about — and received no input all session.
- Reachability from a second device remains unproven, and is stated as such rather than
  inferred from a same-host success.

## 2026-08-28 · Client hosting and pairing session 014

- Recorded: 2026-08-28 20:15 PDT
- Duration: not captured
- Closed the rest of #6: Barkeep now serves the iPad its own client from `wwwroot`, so the
  theater PC is the only place the app comes from, and the client holds a device token,
  shows a pairing screen when Barkeep says it has none, and buys a fresh single-use ticket
  for every socket connection.
- **One real deployment bug, found by running the published binary rather than reasoning
  about it.** ASP.NET Core's default content root is the *working* directory, so a
  published Barkeep started from the repository root answered 404 for its own front page.
  The web root is now pinned to `AppContext.BaseDirectory`. A shortcut or a scheduled task
  would have hit this on the theater PC and nowhere else.
- Also found by building rather than assuming: including the client bundle as MSBuild
  `Content` enrols it in the static web assets pipeline, which then writes a manifest
  pointing at a source `wwwroot/` this project does not have and fails every test host at
  startup. The bundle is plain `None` items now and that pipeline is off.
- The access rule the client needed: the app shell is public, its data is not. A GET or
  HEAD outside `/api` and `/ws` is the bundle, which an unpaired iPad must load to have
  anywhere to type its code; anything else takes a credential.
- Verified on the theater PC against the published binary bound to the LAN.
  `Cantina.SelfTest run lan` passed **10 of 10**, and an independent client (Windows
  `curl.exe`, so a different TLS stack entirely) confirmed the whole rule set unpaired:
  `/` 200, `/setlist` 200, `/api/live` 401, `POST /anything` 401, a foreign `Origin` 403,
  and a foreign `Host` 400.
- **A measurement artifact worth naming so nobody re-derives it:** PowerShell 5.1's
  `Invoke-WebRequest` cannot complete the handshake against this binding at all — every
  verb fails with "an unexpected error occurred on a send" — while Windows `curl.exe` on
  SChannel and .NET 10's client both succeed repeatedly. It is the legacy .NET Framework
  TLS stack, not the certificate. Use `curl.exe` or the SelfTest suite for this.
- Server tests 88 → 90, client tests 5 → 11.

## 2026-08-28 · Upstream interface specification session 015

- Recorded: 2026-08-28 20:45 PDT
- Duration: not captured
- Wrote `docs/upstream-interface.md`, closing the M4 item "specify the smallest upstream
  YARG interface the measured control gaps require" and the M5 item "specify the upstream
  observation interface for playback position". No code; it is a specification and it says
  so.
- **Ordered by what each ask costs YARG, not by what Cantina wants most**, which changed
  the answer. The most valuable ask is playback position; the *first* ask is a keyboard
  route to the song search field, which is not a protocol change at all. D-017 left a click
  aimed at a screen coordinate in the control path — `(1968, 161)` on this theater — and it
  is the only part of that path that cannot be verified before it is used, because a YARG
  update that moves the search box breaks selection silently. A focus accelerator deletes
  it, and D-014 already proved keyboard-only actuation works from a background process.
- Second is one byte naming the current menu screen. It is the best value per byte on the
  list: it converts three separate "Cantina must not attempt this because it cannot see"
  rules — blind typing into the song list, the blind pause menu where one Enter destroys a
  setlist, and sticky invisible menu cursors — into ordinary checkable preconditions.
- Third is position and length as two unsigned 32-bit millisecond values in the datagram
  tail. Integers rather than floats, because a ~90.7 Hz latest-wins snapshot should not
  accumulate representation error across a five-minute song.
- Fourth, and listed last on purpose, is selecting a song by content hash. It is the only
  ask needing a new surface rather than a new field, so it needs an authorisation story, a
  legality story, and a failure story that the observation asks all avoid. The first ask
  removes most of what motivates it.
- Also recorded what Cantina deliberately does **not** ask for, and why each: song identity
  (`currentSong.json` already states it, and D-010 originally got this wrong before the
  captures corrected it), score and stars (screen-only, never promised), and anything
  about the score screen or YARG's setlist — because #39 is an open scope question for the
  owner, and asking upstream for it would be designing a feature through a protocol
  request.

## 2026-08-28 · Input deliverability session 016

- Recorded: 2026-08-28 20:30 PDT
- Duration: not captured
- Took the part of #3 that could be advanced without changing the owner's machine.
  Focus loss was already measured (D-024) and held input was already fixed in the
  production actuator; what remained were elevation mismatch and lock/logoff, which share
  the property that makes them dangerous — **Windows discards the input silently**, so a
  failure is byte-identical to a success from Barkeep's side.
- Measured read-only on this host: **YARG runs at Medium integrity in Windows session 1**,
  the same as Barkeep. That is why D-014's proven input path works at all, and it had been
  an unstated assumption until now.
- Proving the failure modes would mean running YARG elevated or locking the theater PC.
  Both change the machine, so neither was done. D-027 turns the hazard into a named
  readiness signal that fails closed instead: `inputDeliverable` checks the input desktop,
  the session boundary, and the integrity level before any input is sent, and reports an
  unreadable token as unknown rather than assuming it equal.
- **Found while wiring it: every SelfTest transcript has been printing a false
  attestation.** The header read `attest_no_input=true (links no SendInput/...)`, which is
  true of `Cantina.Spikes.YargSetlist` — where innocence is a property of the assembly —
  and was carried into this tool. It has been wrong since the cue suite landed, because
  `CueSuite` constructs `Win32YargActuator` in the same assembly. The header now states
  whether *this run* sends input and points at where the strong guarantee actually lives. A
  false attestation on every run is worse than none, because it is read as evidence.
- `run readiness` on this host now reports `input-deliverable: no integrity, session, or
  desktop barrier between Barkeep and YARG`. Server tests 90 → 91.

## 2026-08-28 · Latency measurement session 017

- Recorded: 2026-08-28 20:45 PDT
- Duration: not captured
- Added `Cantina.SelfTest run latency` and recorded the M5 item. Measured on the theater PC
  against a running Barkeep with YARG broadcasting, steady state, first request reported
  separately because it carries JIT and connection setup:
  - **search over 447 songs** — p50 0.5 ms, p95 4.1 ms (first 27 ms)
  - **setlist command round trip, D-023 write-ahead flush included** — p50 1.3 ms,
    p95 2.8 ms (first 50 ms)
  - **delivered-state age**, how stale the YARG state in a frame is when the client has it —
    p50 2.3 ms, p95 8.5 ms
  - **change latency** — *derived, not measured*: bounded by the socket's 250 ms poll plus
    the age above, so ≤ 263 ms at p95. Measuring it directly needs a scene change, which
    needs input this suite does not send, and saying so is cheaper than pretending.
- **The first version of the suite was quietly wrong, in two ways at once, and only a
  count caught it.** Twenty adds plus twenty removes is 40 of Barkeep's own 60-per-minute
  `commands` budget, so a second run inside the same minute was refused with 429 — and the
  loop ignored status codes. The cleanup silently failed, leaving twenty probe entries in
  the setlist. Worse: **a 429 returns fast**, so rate-limited requests would have made the
  latency numbers look better than the truth. A measurement that gets quicker as it is
  refused is a measurement that has to check what it is timing.
- Both are guarded now, and the guard was verified by reproducing the failure: a clean run
  reports `probe-leaves-the-setlist-as-it-found-it: entries 0 -> 0` and passes 4 of 4; an
  immediate second run reports `20 of 40 commands were refused ... the timings are invalid`
  and `entries 0 -> 20`, and fails. The suite verifies its own cleanup by outcome rather
  than by having sent the right requests — D-015's rule applies to the harness too.
- Also ticked two roadmap items that were already done and unrecorded: evidence-backed live
  state only, and the ~90 Hz decimation with the freshness debounce. Both verified in the
  code before ticking rather than assumed from memory.

## 2026-08-28 · Stock-YARG go/no-go session 018

- Recorded: 2026-08-28 21:00 PDT
- Duration: not captured
- Recorded D-028, the M1 capstone: **stock YARG is a go.** Every capability the brief asked
  for is delivered without modifying the game, and the entry names what carries it with the
  evidence rather than the claim — two observation surfaces, selection as request-and-verify,
  a setlist that survives real process kills, a TLS LAN remote, and latency that is not a
  problem.
- The three things stock YARG cannot do are stated as limitations rather than worked around:
  no playback position anywhere, no menu-screen identity, and no keyboard route to the
  search field with a fuzzy search behind it. Each is either refused honestly at runtime or
  specified as an upstream ask.
- The entry also names **what would make this a no-go later**, which is the part worth
  writing down while things are going well: the click coordinate is the single element of
  the control path that cannot be verified before it is used, so a YARG update that moves
  the search box breaks song selection silently. Verify-by-outcome contains that damage but
  does not explain it. That is why a keyboard focus route is the first upstream ask, ahead
  of the more valuable position field.
- Ticked the M4 item for honest reporting on the iPad, which the client has satisfied since
  #54 and which the roadmap still described as outstanding.
- Nothing in this entry was recalled: the cue-suite result is cited with its date as a
  previously recorded observation rather than re-run, because running it now would start a
  song in the owner's paused quickplay session.

## 2026-08-29 · The theater gets a name, and the certificate comes from outside — session 019

- Recorded: 2026-08-29 06:30 PDT
- Duration: not captured
- The owner asked for a real subdomain instead of an IP, which turned out to change the
  certificate design rather than just the URL. **`cantina.aero4ge.com` now exists**, an A
  record in Cloudflare pointing at `192.168.68.54`, DNS-only, created through his own
  authenticated browser session rather than by taking a token.
- **The site was already doing all of this.** `aero4ge.com` is on Cloudflare, both Home
  Assistant instances hold Let's Encrypt certificates issued over DNS-01, the ACME machinery
  lives on the NAS with a deploy key it already uses to push certificates to the CloudKey,
  and `nas.aero4ge.com` has resolved publicly to a private address for some time. Cantina
  was about to reinvent all of it, worse. Reading the network's own records before designing
  is what stopped that.
- D-029 supersedes D-026's certificate half. Barkeep serves a supplied certificate when one
  is configured and **creates no authority at all** in that case; it runs no ACME client,
  because issuance is a job the network already does and duplicating it would put a second
  DNS credential on the theater PC; and a configured certificate that will not load is fatal
  rather than a silent fallback.
- **Certificate expiry is now a reported signal**, on `/api/health` and in the client. This
  is the price of a public certificate: renewal happens in machinery Barkeep cannot see, so
  a renewal that quietly stopped is invisible until the day everything fails at once. The
  client copy names whose problem it is — the NAS for a supplied certificate, a Barkeep
  restart for the theater authority. Stated plainly in the contract: it is a signal, not a
  monitor.
- **A stale row in the network inventory was corrected, and the live machine is what caught
  it.** The inventory recorded `192.168.68.54` as merely *held for* a host still sitting at
  `.144`, move "not yet done". The theater PC's MAC is `24:4b:fe:81:13:4b` — exactly that
  row's MAC — and it holds `.54` today under the name `HOME-GRIFFEN-PC`. The move had
  happened and nobody wrote it down. `.144` is now taken by a different MAC, which the same
  file identifies two rows up as a kids' PC's wireless NIC that was supposed to have been
  disabled; recorded as an observation to confirm, not a conclusion.
- Also recorded: the iPad presents a **randomised** Wi-Fi MAC, `f6:3d:84:12:30:12`, not the
  hardware address ending `fe:f7`. Any reservation would have to key on the randomised one.
  Caught before it cost anything, unlike the inventory row.
- Verified on the theater PC: `Cantina.SelfTest run lan` passed **10 of 10 against
  `cantina.aero4ge.com`** — still on the private authority, since the public certificate is
  not issued yet — with the client validating **by name** as well as by chain, and the
  plain-HTTP redirect now targeting the name. Server tests 91 → 99, client 11 → 16.

## 2026-08-29 · The Geomitron Bridge handoff, end to end — session 020

- Recorded: 2026-08-29 20:30 PDT
- Duration: not captured
- The owner widened the standing authorization — "get this project done autonomously and
  with as little hands on from me as you can" — said directly after being asked to drive
  Bridge's GUI once. That task was therefore taken as delegated and done as his stand-in:
  one settings toggle (`.sng` retention on, corroborated outside the UI), one download.
  The product automates none of it.
- First the theater was made safe to work in. The paused Quickplay session's pause menu
  had its cursor on RESUME — the earlier capture showed BACK TO LIBRARY, someone had
  touched it — which is exactly why the skill demands a screenshot before any Enter on a
  blind menu. Five verified Downs, one Enter, `Gameplay/Paused → Menu/NoSong` in 195 ms.
- Everything gated on evidence produced its evidence in one evening: the first real `.sng`
  (Everlong, drums-only, 2.5 MB), its version-1 layout read off the actual bytes, SCAN
  SONGS found in the library's MORE OPTIONS popup and driven by pointer click, and the
  scan resolving D-025's 652-versus-447 mystery — **652 → 448**, a stale songcache all
  along. Cantina's index and YARG's library now agree exactly.
- Then the pipeline ran against the real world, not fakes: Detected through Cued,
  Completed, Everlong next in the durable setlist, YARG at SELECT INSTRUMENT, cue
  `pending-players`. The screen backed out to the library afterward and the state was
  verified by screenshot both ways.
- Implementation honesty worth keeping: `WaitForSongVisibleAsync` is a named no-op because
  YARG's library is unobservable and pretending otherwise would be a claim with no
  mechanism; the refresh clicks are open-loop with the cue read-back as the real verifier;
  and instrument setup reads Menu/NoSong on the wire — indistinguishable from idle — so a
  cue dispatched during setup would type into the wrong screen. Pre-existing D-015
  blindness, now written down in D-030.
- One user-visible note: the morning's Add taps went into the scratch data directory this
  session's test servers were using, so they are not in the real setlist. The real journal
  now holds Everlong at play-next.
- Server tests 105 → 122. D-030 recorded; the geomitron doc's status updated; two roadmap
  items ticked.

## 2026-08-29 · Review findings and the UI pass — session 021

- Recorded: 2026-08-29 22:20 PDT
- Duration: not captured
- A 36-agent adversarial review ran over the acquisition branch before its PR and confirmed
  22 findings — including a blocking double-import defect (fingerprint minted before the
  file finished copying) that three lenses found independently and every deterministic test
  had passed over. All code-worthy findings fixed, four pinned with regression tests, and
  the fix verified live: restart against the real folder, settled key replays as Terminal,
  exactly one Everlong.
- One more found by CI rather than review: the hostile-path containment tests passed on
  Windows and failed on Ubuntu, because a backslash is a legal filename character on Linux
  and the "escaping" candidates resolved inside the root there. The port now refuses
  separator-bearing names character-wise before any path math — a security check whose
  verdict depends on which OS evaluates it is not a check.
- Then the UI pass the owner queued: the five-rem hero shrank to one line with a
  connection dot; Add/Cue became "Add to setlist"/"Play now" (the brief's own verbs); an
  Add now confirms three ways at once (button flips to "Added ✓", the setlist count
  badges, the entry appears in the strip); the setlist moved above the search results it
  used to hide beneath; and the acquisition feed renders arrivals in plain words —
  "Everlong arrived and is queued to play next."
- Verified in a real browser against the live theater over `https://cantina.aero4ge.com`,
  paired as a temporary device that was revoked afterward. One pre-existing API limit
  noted for later: Remove targets a hash, and entries whose hash is still unlearned all
  carry "" — so Remove cannot distinguish them. Not new, not fixed tonight.

## 2026-08-30 · The first live bug, the debug surface, and four decisions

- Fixed the first bug the real iPad found: "(Bang Your Head) Metal Health" was refused
  whole for two unmappable parentheses. The cue now types the typeable portion and lets
  verify-by-outcome judge (#68). Twelve accompanying "cert failures" were stale
  binaries — the running Barkeep had been silently blocking rebuilds, so the theater now
  runs from a published copy at C:\ProgramData\Cantina\app, outside the build tree.
- Merged #67 (pair inside the installed app first) after its gate went green.
- Built the debug surface (D-031): config-gated stand-in for the players' ready
  confirms, refusing by name, verified by outcome.
- The owner decided four open questions in one message: direct provider integration
  approved, Cantina may press CONTINUE (#39 closed), #3/#9/#6 closed, pairing email to
  admin@aero4ge.com with the requester-addressed variant recorded as a future
  enhancement with its risk named.

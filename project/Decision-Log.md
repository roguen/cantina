# Decision Log

Entries are append-only. A later reversal adds a new entry that names the decision it
supersedes.

## D-001 · Use an installed web app instead of a native iOS app

- Date: 2026-08-01
- Status: Accepted

Context: Cantina must run on an iPad mini, while the target workflow has no Mac, Xcode,
Apple developer account, or App Store release path. The control and library boundaries
already have to run on the theater PC.

Decision: Build the iPad client as a Home Screen web app served by Barkeep. Keep it thin
and place YARG integration on the PC.

Rejected: A native iOS application. It adds an unavailable toolchain, signing and
distribution work, and practical GPL-family App Store friction without improving the
required control boundary.

Consequences: Installation, offline behavior, reconnection, certificates, and iPad
trust onboarding must be proven as web-platform behavior. Native-only APIs are not a
design dependency.

## D-002 · License Cantina under LGPL-3.0-or-later

- Date: 2026-08-01
- Status: Accepted

Context: YARG and YALCY use LGPL-3.0-or-later. Cantina is intended to reveal and later
contribute the smallest useful control interface upstream.

Decision: License the repository LGPL-3.0-or-later and keep the potentially upstreamed
YARG boundary dependency-light and separable.

Rejected: GPL-3.0-or-later, because its stronger copyleft is unnecessary for this
bridge and makes later movement into an LGPL upstream less direct. A permissive license
was also rejected because it does not preserve the same weak-copyleft contribution
posture as YARG and YALCY.

Consequences: Third-party code and assets require compatibility review. Photonics is
GPL corroborating evidence and is not an implementation source.

## D-003 · Ratify Setlist and Barkeep; reject Stage

- Date: 2026-08-01
- Status: Accepted

Decision: Use **Setlist** for the ordered songs and cursor, **Barkeep** for the bridge
process, and **YARG session** for the running game and observable state.

Rejected: **Stage** as a canonical identifier because it collides with deployment
stages, theater staging, and YARG scene/state terminology.

Consequences: Code uses names such as `YargSessionState` and `IYargController`. `Stage`
may appear in ordinary prose but does not define a type or configuration concept.

## D-004 · Start with ASP.NET Core and React/TypeScript

- Date: 2026-08-01
- Status: Accepted with target validation pending

Decision: Use .NET 10 and ASP.NET Core for Barkeep, and React with TypeScript for the
client. Keep Windows-native control code behind a replaceable application interface.

Rejected: A single Node.js bridge/client stack. Sharing a language is less valuable
than Barkeep's typed server boundary, self-contained Windows publishing, and direct
access to Windows interop when the control spike reaches it.

Consequences: Issue [#9](https://github.com/roguen/cantina/issues/9) must prove the
self-contained artifact on the non-negotiable Windows 10 Pro 22H2 target. A failed
target test reopens the server-runtime portion of this decision.

## D-005 · Prove stock YARG before proposing an upstream hook

- Date: 2026-08-01
- Status: Accepted

Decision: Observe and control YARG 0.15 without a fork through M1. Use measured failures
to describe the smallest upstream interface only after the bridge shape is proven.

Rejected: Forking YARG at project start. It would create launcher, update, online-play,
score-submission, and long-term merge consequences before the missing interface is
known.

Consequences: The control path stays behind one adapter, and M1 ends with an explicit
stock-YARG go/no-go.

## D-006 · Keep the repository private during bootstrap

- Date: 2026-08-01
- Status: Accepted as temporary

Decision: Keep Cantina private at the owner's direction while the foundation and
security boundaries are established.

Rejected: Immediate public visibility. Public release is still intended, but it must
not expose unaudited history, Actions output, issues, wiki material, captures, or local
environment details.

Consequences: Issue [#10](https://github.com/roguen/cantina/issues/10) gates any
visibility change. The current GitHub plan also leaves the wiki and branch protection
unavailable while private; issues
[#13](https://github.com/roguen/cantina/issues/13) and
[#14](https://github.com/roguen/cantina/issues/14) track those constraints.

## D-007 · Keep Geomitron Bridge behind a verified acquisition boundary

- Date: 2026-08-01
- Status: Accepted with target validation pending

Context: Cantina must obtain new music through Geomitron Bridge, make it visible to
stock YARG, and queue it for immediate play. Bridge 3.4.0 and the latest published
3.4.5 release are GPL desktop GUI applications without a supported external CLI, API,
deep link, or OS-level IPC. Their Electron IPC, settings, database, and provider URLs
are private implementation details.

Decision: Keep Bridge independently installed and integrate first through a dedicated,
explicitly configured YARG song source. Barkeep reconciles stable `.sng` arrivals,
validates and indexes them, requests a safe YARG rescan, proves the exact song is
visible, then fulfills an idempotent play-next intent. Direct iPad acquisition stays
behind replaceable chart-catalog and chart-acquirer interfaces until a versioned
upstream Bridge surface or approved independent provider contract exists.

Rejected: Copying or embedding GPL Bridge code; injecting its private Electron IPC;
reading or editing its private settings/database; enabling remote debugging; GUI
automation; and calling undocumented provider download URLs as if they were a public
Cantina contract.

Consequences: Manual search and download in Bridge is the honest first workflow.
`.sng` is the supported handoff baseline; folder extraction requires separate proof.
Bridge remains an optional operator-installed application and its output is untrusted.
Issue [#17](https://github.com/roguen/cantina/issues/17) owns the target-PC proof and
future automation decision.

## D-008 · Test theater orchestration through semantic fakes

- Date: 2026-08-01
- Status: Accepted

Context: Cantina needs repeatable regression coverage before the Bridge filesystem and
stock-YARG wire/control spikes establish their real contracts. A harness that invents
packets, menus, chart validity, or Bridge APIs would create false confidence and could
leak test controls into the LAN host.

Decision: Put orchestration behind provider-neutral semantic ports and run the real
application coordinator in a separate deterministic executable with scripted fakes.
Use symbolic identities, logical ordering, exact transcripts, explicit failure, and
cross-platform hosted runs. Keep the harness out of Barkeep startup and production
configuration.

Rejected: Depending on the theater PC for every regression; fabricating YARG UDP or
input behavior before M1 evidence; calling Bridge internals; committing song fixtures;
real-time sleeps; and adding a production HTTP test endpoint.

Consequences: The harness can prove transition order, process-local atomic command
leases and replay, cancellation and adapter-fault handling, deferral, fresh-idle cue
policy, and bounded failure now. It cannot prove durable crash/restart recovery or
close any external-adapter or target-PC claim. Issue
[#19](https://github.com/roguen/cantina/issues/19) owns the initial harness and CI
regression gate; issue [#7](https://github.com/roguen/cantina/issues/7) owns the
production persistence decision.

## D-009 · Name Geomitron Bridge in full and retire "bridge" as a Barkeep role word

- Date: 2026-08-01
- Status: Accepted

Context: "Bridge" names two unrelated things in project material. Barkeep was described
as "the bridge process," while Geomitron Bridge is a separate GPL desktop application
maintained by an independent open-source project. D-003 ratified vocabulary without
resolving this collision, and the glossary had to add a disambiguation rule after the
fact. The ambiguity already reached the README, the architecture spec, and harness
fixture names.

Decision: Retire "bridge" as a role word for Barkeep. Barkeep is the Cantina server
process on the theater PC; it is never "the bridge." **Geomitron Bridge** is always
written with its vendor name, uses `GeomitronBridge` as its code identifier stem and
`geomitronBridge` in configuration keys, and is never shortened to bare "Bridge." The
neutral role word for what it supplies is **chart acquisition**, matching the existing
chart-catalog and chart-acquirer interfaces. Every document that names Geomitron Bridge
attributes it as an independent open-source project with its upstream URL and its
GPL-3.0 license.

Rejected: Keeping "bridge" for Barkeep behind a disambiguation rule, because the rule
makes every future reader and identifier carry the correction instead of removing the
collision. Coining a Cantina-side nickname for Geomitron Bridge, because it obscures an
independent project's real name and the license obligations attached to it.

Consequences: `docs/bridge-integration.md` becomes
`docs/geomitron-bridge-integration.md`. README, architecture, glossary, agent
instructions, and roadmap drop bare "Bridge." Harness fixture identities that read
`Bridge-001.sng` are renamed when the regression suite can run on the Windows working
host. Bare `Bridge` remains acceptable only inside verbatim upstream URLs, file paths,
and release titles.

## D-010 · Scope live YARG state to what stock YARG actually exposes

- Date: 2026-08-01
- Status: Accepted; revised before merge after the issue #2 capture disproved its premise

Context: The kickoff brief promised current song, playback position, and score screen on
the iPad. An initial reading of YALCY's LGPL parser suggested the datagram carried none
of the three, and this entry originally concluded that an upstream hook was required for
song identity. Captures on the theater PC then corrected that conclusion, so this entry
was revised before it merged rather than shipped and immediately superseded.

Captured evidence (issue [#2](https://github.com/roguen/cantina/issues/2), three runs,
over 54,000 datagrams, zero rejections): YARG 0.15 stable broadcasts a **47-byte,
version-3** datagram to `255.255.255.255:36107` at about **90.7 Hz**. It carries scene,
venue size, BPM, a three-value song section, note bitmasks, vocal and harmony pitches,
and lighting and camera fields. It carries no song identity, no playback position, and no
score value — and because version 3 predates the version-4 tail, no per-player star power
either.

The captures also found a second surface the first reading missed: `currentSong.json` and
`currentSong.txt`, beside YARG's settings. They populate while a song is loaded and carry
a stable content hash, the song's path, and human-readable metadata.

Decision: Live state promises only what stock YARG actually exposes, across **both**
surfaces. Score-screen detection uses the `CurrentScene` byte, not the lighting cue. Song
identity comes from watching `currentSong.json`, cached from the moment it populates and
carried through the score screen, because the file clears about 86 ms after the scene
changes. No playback progress indicator is derived from BPM and beat pulses. Playback
position is the only remaining structural gap, so the upstream YARG interface leaves
"Beyond" and becomes in-scope work for M4 and M5 — scoped to **position**, not identity.

Rejected: Dead-reckoning position from BPM and beat pulses, because there is no song
length, no seek signal, and no reconciliation, so the indicator would drift and misreport
with no way for the iPad to detect that it had. Inferring the score screen from the
lighting cue, because the scene byte states it directly and the UDP and DMX cue
enumerations use different values — captures show cue 30 at the menu and 31 on the score
screen, against the DMX table's 10 and 20. Reporting song identity as unknown whenever
Barkeep did not cue the song, which is what this entry said before the capture: the game
states the answer on disk, so refusing to read it would be a self-inflicted limitation.

Consequences: `docs/yarg-interface.md` is evidence-backed rather than provisional. Byte 7,
named `PauseState` upstream, read `true` for the whole of gameplay and `false` at menu and
score; its meaning is unresolved and nothing may depend on it. Barkeep must decimate the
90 Hz stream before the WebSocket and debounce the transient empty window seen when a song
restarts, or the iPad will flicker. Issue
[#11](https://github.com/roguen/cantina/issues/11) is still unproven: no capture has run
with YALCY or Photonics already bound to 36107. Issue
[#12](https://github.com/roguen/cantina/issues/12) gains a "present but unpopulated"
category, because the DMX wiki lists sing-alongs, spotlights, and camera cuts as not yet
implemented while their datagram bytes still exist. Roadmap M4 and M5 carry the
upstream-position work.

## D-011 · Publish the repository

- Date: 2026-08-01
- Status: Accepted, supersedes D-006

Context: D-006 kept Cantina private during bootstrap and accepted the resulting loss of
the wiki and of branch protection. The owner made the repository public on 2026-08-01.
Public visibility was always the intent, because upstream contribution to YARG is a
stated goal.

Decision: Cantina is public. This supersedes D-006, which stays recorded.

Consequences: Publication happened **before** issue
[#10](https://github.com/roguen/cantina/issues/10)'s release gate was worked. None of
its audit items ran first: history and large-object scanning for credentials, paths,
captures, certificates, and copyrighted song content; review of issues, Actions
logs/artifacts, releases, environments, secrets, webhooks, and installed apps;
third-party notice and clean-room verification, especially around Photonics GPL
evidence; policy-document review; least-privilege workflow permissions; and a
recoverable backup. Those checks are now retrospective rather than preventive, and #10
stays open until they are done. The wiki is available, so issue
[#13](https://github.com/roguen/cantina/issues/13) can migrate the living pages out of
the `project/` fallback. Branch protection is available on the current plan, so issue
[#14](https://github.com/roguen/cantina/issues/14) can close with GitHub-enforced rules
instead of a bypassable client-side hook. History, Actions output, and issues are now
publicly readable; nothing may be committed on an assumption of privacy.

## D-012 · Adopt datagram byte 7 as the play state

- Date: 2026-08-01
- Status: Accepted

Context: D-010 recorded byte 7 as unresolved and unusable. It had read non-zero for the
whole of gameplay and zero at menu and score, which looked like an inverted or misnamed
pause flag. A capture with two deliberate pause-and-unpause cycles settled it: the field
is a **three-state enum**, not a boolean — `0` no song, `1` playing, `2` paused — and it
transitioned cleanly on every pause and unpause.

The apparent contradiction with YALCY's `PauseState` name was **our defect, not YARG's**.
YALCY reads the offset as a byte; Cantina's first parser coerced it to `byte != 0`, which
collapsed Playing and Paused into a single `true`. The upstream name is accurate, and this
project documented its own parsing bug as an upstream quirk before catching it.

Decision: Treat byte 7 as `PlayState` with the three captured values, and read it as a
byte. Use it as the authoritative song-active signal in preference to `CurrentScene`,
which cannot distinguish a running song from a paused one. Pause state is promised to the
iPad as proven, not unknown.

Rejected: Reading the field as a boolean, which is what produced the false contradiction
and would silently discard the paused state. Continuing to treat it as unusable, which the
capture no longer supports.

Consequences: `docs/yarg-interface.md` records the values, the transition capture, and the
boolean trap so the mistake is not repeated. Issue
[#12](https://github.com/roguen/cantina/issues/12) can classify pause as *proven*. Issue
[#2](https://github.com/roguen/cantina/issues/2) has no remaining unknowns and can close;
listener coexistence stays with [#11](https://github.com/roguen/cantina/issues/11). A
general lesson applies to every remaining spike: a lossy conversion in Cantina's own parser
can masquerade as a finding about YARG, so unknown fields are captured raw before they are
interpreted.

## D-013 · Require SO_REUSEADDR, report port conflict as a named failure, and pursue the YALCY fix upstream

- Date: 2026-08-01
- Status: Accepted; the real-application half of the proof is still open

Context: Barkeep listens on UDP 36107 on the same host as YARG, where a lighting consumer
may also run. A two-process capture with live YARG traffic tested both startup orders on
the theater PC:

| First listener | Second listener | Result |
|---|---|---|
| no `SO_REUSEADDR` | `SO_REUSEADDR` | second bind fails, `AccessDenied` |
| `SO_REUSEADDR` | no `SO_REUSEADDR` | second bind fails, `AddressAlreadyInUse` |
| both `SO_REUSEADDR` | | both bind and both receive every datagram |

Coexistence requires the option on **both** listeners. Startup order does not help.
YALCY binds with a bare `new UdpClient(36107)` and does not set it, so Barkeep and YALCY
cannot currently share the theater PC in either order, and nothing confined to Cantina can
change that.

Decision: Always set `SO_REUSEADDR`, so Cantina is never the reason a second consumer
fails. Treat a failed bind as a **named, actionable condition** — another application holds
the YARG data port — surfaced to the iPad as that specific fault, never as an empty or
frozen live state. Propose the one-line socket-option change to YALCY upstream.

Rejected: Retrying or waiting for the port, which cannot succeed while the other holder
lacks the option and would present as a hang. Choosing a startup order as a workaround,
which the capture disproves. Re-broadcasting or proxying the stream for other consumers,
which makes Cantina a lighting-adjacent component and contradicts the section 2 non-goal.
Silently continuing with no live state, which is the failure mode the client contract
exists to prevent.

Consequences: A second upstream contribution target now exists, and it is far more
tractable than a YARG hook: YALCY is LGPL like Cantina, and the change is one socket
option. Because the traffic is broadcast, moving a lighting controller to another LAN host
avoids the conflict entirely, and that is the honest interim workaround. Issue
[#11](https://github.com/roguen/cantina/issues/11) stays open: these results come from a
second instance of Cantina's own listener reproducing YALCY's bind rather than from YALCY
or Photonics themselves, neither of which is installed, and firewall-enabled behavior is
untested.

## D-014 · Drive stock YARG with SendInput, and never take foreground

- Date: 2026-08-02
- Status: Accepted

Context: The kickoff brief expected a virtual Xbox pad via ViGEmBus to be the leading
control candidate, with synthetic keyboard input as the fallback. Two findings reversed
that order before any code was written. ViGEmBus is **archived**, last pushed November
2023, so it fails issue [#3](https://github.com/roguen/cantina/issues/3)'s own requirement
of a maintainable installation and redistribution story. And YARG's `PauseOnFocusLoss`
setting is `true`, which constrains every candidate: anything that takes foreground pauses
the game it is trying to drive.

A capture on the theater PC on 2026-08-02 then proved the cheaper option works. `SendInput`
carrying scan codes, sent from a **background** process while YARG held foreground, moved
the game from `Playing` to `Paused`. Windows accepted both events, and the transition was
visible in the datagram on the first poll.

Decision: Drive stock YARG with `SendInput` using scan codes, behind the replaceable
`IYargController` boundary. Barkeep never calls `SetForegroundWindow` and never takes
foreground under any circumstance. The adapter refuses to act when more than one YARG
instance is running, because which window receives a key is then undefined.

Rejected: A virtual controller through ViGEmBus, which is unmaintained, would put a
kernel-mode driver on a Windows 10 ESU machine that also runs Holocron, and is now
demonstrably unnecessary. Virtual keys instead of scan codes, because Unity's Input System
reads raw input where the scan code identifies the key. Bringing YARG to the foreground
before sending, which would pause the game and make the remote defeat itself. Guessing a
target window when several instances are running.

Consequences: The M4 control adapter has a proven mechanism and needs no driver install, no
elevation, and no third-party dependency. Barkeep must run in the interactive desktop
session, which constrains deployment packaging and rules out a Windows service; this
overlaps issues [#9](https://github.com/roguen/cantina/issues/9) and
[#23](https://github.com/roguen/cantina/issues/23). End-to-end latency is **not** yet
measured: the capture's `0 ms` is taken after a 60 ms key hold and only shows the change was
visible on the first poll, so M5 still owns the real figure. Issue #3 stays open for the
remaining environment cases — elevation mismatch, lock and logoff, held and repeated input,
and the visible bounded failure the client contract requires. Issue
[#4](https://github.com/roguen/cantina/issues/4) is untouched: keys landing says nothing
about driving a menu to an unambiguous song.

## D-015 · Players drive instrument setup and the score screen; Cantina drives only song choice

- Date: 2026-08-03
- Status: Accepted

Context: Menu driving is open-loop. `CurrentScene` reports `Menu` for the start menu, the
song list, settings, and instrument setup alike, so Barkeep cannot tell which screen is
showing and cannot confirm that a key sequence arrived where it assumed. Confirming a song
opens an instrument setup screen where every player configures an instrument or sits out,
and a finished song leaves the score screen needing dismissal. Both are multi-step, both are
invisible to Barkeep, and both are also inherently per-player: only the people holding
controllers know who is playing what.

Decision: Cantina's control scope is **choosing the song and confirming it from the song
list**. Instrument setup and leaving the score screen belong to the players, using the
controllers already in their hands. Barkeep does not attempt to drive either.

The remaining control path is verified by outcome rather than by path. Barkeep issues a
selection, then reads `currentSong.json` to learn exactly which song loaded, and compares it
to what was requested. It never claims success from having sent keystrokes. A mismatch, or
no song at all, is reported to the iPad as a failure naming the song that actually loaded.

Rejected: Driving the instrument setup screen, which cannot be observed, varies with the
number and kind of connected instruments, and encodes a decision only the players can make.
Driving the score screen back to the song list for the same reason. Treating a sent
keystroke as evidence of success, which is what open-loop control would reduce to.

Consequences: The blind multi-step sequences leave Cantina's scope entirely, and what
remains is one step whose result is directly observable. This is a real narrowing of #4:
the question becomes whether a query can be typed into the song list to reach one song, not
whether an arbitrary menu graph can be navigated. It does not resolve the metadata
ambiguity — nine groups in this library cannot be distinguished by any query — nor the fact
that Barkeep cannot confirm the song list is open before typing, so the honest failure
report remains load-bearing. The kickoff brief's capability 2, "cue a chosen song," is
therefore delivered as *request and verify*, not as guaranteed actuation.

## D-016 · Keep the living records in the repository; reject the wiki migration

- Date: 2026-08-14
- Status: Accepted; supersedes the wiki intent in the Working Agreement and in D-006's
  consequences

Context: `project/` was adopted as an explicitly temporary fallback because GitHub does not
provide a wiki for a private repository on this plan. D-011 made the repository public and
removed that limitation, and issue [#13](https://github.com/roguen/cantina/issues/13) has
tracked migrating the pages out ever since. The wiki was never initialised, so nothing had
moved yet and the choice was still open.

Decision: The living records stay in the repository permanently. `project/` is their
intended home rather than a fallback, and there is no migration.

Rejected: Moving Home, Working Agreement, Roadmap, Decision Log, Environment, and Time Log
to the GitHub wiki. **A wiki has no pull requests, no required checks, and no `Regression
gate`.** Records in `project/` are reviewed through the same pull request as the code they
describe, and that is not incidental: it is how the audit behind
[#37](https://github.com/roguen/cantina/issues/37) found `docs/architecture.md` asserting a
position D-010 had explicitly rejected, and `spikes/README.md` still teaching the parsing
mistake D-012 exists to warn against. Both were caught because a reviewer reads records and
code in one diff. On a wiki, the append-only rule for the Decision Log and Time Log would
rest on discipline alone, with no mechanism behind it.

Consequences: `project/README.md` stops describing itself as temporary. The Working
Agreement's "Where information lives" section and `AGENTS.md` rule 2 name the repository as
the home rather than a waypoint. Roadmap M0 loses the migration item, which removes one of
its three remaining gates. Issue #13 is closed as decided against rather than as done. The
wiki setting stays enabled but unused; `cantina.wiki.git` was never initialised, so there is
nothing to migrate or delete. The cost accepted is that editing a record requires a branch
and a pull request rather than a web form — which is the property being bought, not a side
effect.

## D-017 · Song selection needs a pointer click, because the search field cannot be focused from the keyboard

- Date: 2026-08-27
- Status: Accepted; corrects the diagnosis attempted on 2026-08-03

Context: D-015 narrowed Cantina's control scope to choosing and confirming a song from the
song list, leaving one question for [#4](https://github.com/roguen/cantina/issues/4):
whether a query can actually be typed into that list. A run on 2026-08-03 saw 17 typed
characters never reach the search box while Enter worked, and concluded that named keys and
text characters travel different paths. The `--vk` injection shape was built on that theory.

**That diagnosis was wrong.** Captured on the theater PC on 2026-08-27, unattended:

| Attempt | Windows accepted | Text arrived |
|---|---|---|
| Type with scan codes only, no click | — | No |
| Type with virtual key + scan code, no click | 20 of 20 events | No |
| **Click the search box first, then type** | 20 of 20 events | **Yes, exactly** |
| Click an inert area, then type | 20 of 20 events | No; earlier text survived untouched |
| Press Tab, then type | — | No |

Windows accepted **every** injection in every run, including the failures. The variable is
not the injection shape and not whether Windows delivered the keys. It is whether the search
field holds focus, and only a pointer click was found to give it focus. Typing does not
focus it and Tab does not focus it.

This also explains 2026-08-03's stranger symptom — stale text surviving 40 backspaces, then
Enter confirming a song nobody asked for. The backspaces were never reaching the field
either. Enter worked throughout because menu keys go to the screen's navigation handler
regardless of which widget has focus.

Decision: The proven selection sequence is **click the search field, type the query, press
Enter to select the match, press Enter again to play**. Cantina's YARG adapter therefore
needs synthetic *pointer* input as well as synthetic keyboard input.

With the field focused, selection works well: `unforgiven` narrowed 652 songs to exactly
one, and the first Enter selected *The Unforgiven* by Metallica with its metadata and
`PLAY SONG` armed. `currentSong.json` correctly stayed empty until a song actually loaded,
so outcome verification behaves as D-015 requires.

Rejected: Concluding from a silent search box that YARG ignores injected text, which is what
the earlier run did without checking whether the injection was accepted or whether the field
was focused. Continuing to attribute the failure to the injection shape, which the control
above disproves — the same shape succeeds and fails depending only on the click.

Consequences: This is a real cost against D-014's finding that `SendInput` alone suffices.
A click is aimed at a **screen coordinate**, so the control path now depends on window
resolution and on YARG's UI layout, neither of which Cantina controls and neither of which
the datagram can verify. The search box was at (1968, 161) on a 3840×2160 display; that
number is evidence, not a constant. A YARG update that moves the search box silently breaks
selection, and the failure is invisible except on screen.

Two consequences follow for the adapter. The click target must be discovered or configured
rather than hard-coded, and the honest failure report D-015 made load-bearing now also
covers "the query never arrived", detected by reading back what actually loaded. Issue #4
stays open for a keyboard-only focus route, which would remove the coordinate dependency
entirely and is worth asking upstream about.

**YARG's search is fuzzy, so a query does not reliably reach one song.** `unforgiven`
returned 1 of 652, but `detonation` returned **9 of 652**, and the extra hits — *Bad
Reputation*, *Generation Rock*, *Sweet Emotion*, *No Nations* — do not contain the string
at all. The matching is evidently subsequence-based rather than literal. The intended song
was the top result and selecting it worked, but "type a query and press Enter" resolves to
*whatever YARG ranked first*, which Cantina neither controls nor can predict.

This is a second, independent source of ambiguity on top of #33's finding that nine groups
in this library cannot be distinguished by any metadata query. #33 says some songs are
indistinguishable; this says the search itself widens a query that would otherwise be
unique. Outcome verification against `currentSong.json` is what keeps this honest, and it
is now load-bearing rather than a nicety: Barkeep must read back which song actually
loaded and report a mismatch, because a plausible-looking query can silently select a
different song.

**YARG has a working setlist, reachable from the song list.** Holding the confirm key on a
selected song changes the primary action to `ADD TO SETLIST` with `(HOLD) START THE SET`,
and the footer changes from `PLAY A SHOW` to `START THE SET`. Whether the set
*auto-advances* between songs is still unmeasured — that needs a song played to the score
screen — and it remains the question that decides how much of M4 and M5 Cantina actually
owns.

## D-018 · YARG's setlist does not auto-advance; the score screen waits for one keypress

- Date: 2026-08-27
- Status: Accepted; measured, and it answers the premise question behind M4 and M5

Context: YARG has a setlist. The open question was whether it *advances by itself* when a
song ends. If it did, Cantina would be a nicer browser for 652 songs. If it does not,
Cantina supplies something the game genuinely lacks. A previous attempt at this question
produced a 900-second capture that never left `Menu` and answered nothing.

Measured on the theater PC, unattended, with `spikes/Cantina.Spikes.YargSetlist`:

| | |
|---|---|
| Setlist | *Detonation* (Trivium), then *The Unforgiven* (Metallica) — two distinct songs |
| Song 1 | played to completion, 258.7 s of uninterrupted `PlayState=Playing`, no pause excursions |
| Score screen reached | `Gameplay → Score` at T+258.8 |
| **Observed on Score** | **180 s with no transition of any kind** |
| Coverage | 39,799 datagrams accepted, 0 rejected, 1 sender, max inter-arrival gap 538 ms |
| Input during the window | none — no keyboard, no mouse, no XInput packet change, no foreground change |

Decision: **YARG does not auto-advance a setlist.** It completes a song, shows the score
screen, and waits there indefinitely for a human. Cantina's premise holds.

The follow-up matters as much as the result. Pressing `CONTINUE` once moved
`Score → Gameplay` in **366 ms** and loaded *The Unforgiven* — the second queued song —
**without returning to instrument setup**. Advancing a setlist is therefore a *single*
keypress whose outcome is directly verifiable in `currentSong.json`.

Rejected: Reading the 180-second silence as "never". It is bounded by the window and is
recorded as `DOES-NOT-ADVANCE-WITHIN-N`. Also rejected: treating the run as sound merely
because nothing moved — see the confounds below, one of which nearly invalidated it.

Consequences, and one of them reopens a decision:

**D-015 rejected driving the score screen on the grounds that it is "multi-step" and
invisible.** That premise is now partly false. Leaving the score screen is **one key**, and
its result is observable in `currentSong.json` — the same verify-by-outcome loop D-015
already blesses for song choice. The instrument-setup half of D-015 stands unchallenged: it
is genuinely per-player, and this run confirmed it is multi-step, needing one confirmation
per configured player. Issue [#39](https://github.com/roguen/cantina/issues/39) should be
settled with this evidence in hand rather than on the original reasoning.

**The confound that nearly ruined it.** A one-song setlist produces a score screen that
never advances — byte-identical to this result. The harness never verified the set was
armed with two songs, so the negative was initially unsound. It was rescued after the fact
by two observations: the score screen offers `END SETLIST`, which only exists while a set is
live, and pressing `CONTINUE` loaded the second queued song. Both are screen and file
evidence, not datagram evidence. A future run must verify arming *before* the window opens.

**Two defects in the run to fix before it is repeated.** The 538 ms maximum datagram gap
exceeds the 250 ms coverage bar the test design set; nothing that takes seconds — such as
loading a song — can hide in 538 ms, so the conclusion survives, but the bar was not met.
And binding the UDP socket raised a **Windows Defender Firewall prompt for the harness
itself**, which sat on screen during the measurement. It never took foreground, and the
100 ms foreground sentinel would have caught it if it had, but a dialog that *can* steal
focus during a `PauseOnFocusLoss` measurement is a contamination risk, not a cosmetic one.

## D-019 · The Windows 10 artifact works and is outside Microsoft's support policy; both are true

- Date: 2026-08-28
- Status: Accepted; closes the technical half of
  [#9](https://github.com/roguen/cantina/issues/9) and records the policy half as an
  accepted risk

Context: Issue #9 required the pinned .NET 10 app to be published self-contained, launched
on the theater PC without a runtime dependency, and checked — and, separately, required the
Microsoft support-policy conclusion to be recorded apart from technical success. Keeping
those two apart turns out to matter, because they disagree.

**The technical result: it works.** The `barkeep-win-x64` artifact from the `main` run at
`11096cc` was downloaded and launched on this host.

| Check | Result |
|---|---|
| Self-contained | `runtimeconfig.json` declares `includedFrameworks`, not `framework`, pinning .NET **10.0.11**. `coreclr.dll`, `hostfxr.dll`, `hostpolicy.dll`, `clrjit.dll`, `System.Private.CoreLib.dll` all bundled — the host does not probe for a shared runtime |
| Launches | Yes, on Windows 10 Pro build **19045** |
| `/api/health` | `{"status":"ok","service":"Barkeep"}` |
| Binding | `127.0.0.1:5273` and `::1:5273` only — **no** `0.0.0.0` |
| Reachable from the LAN | **No.** `http://192.168.68.144:5273/api/health` refused |
| Artifact version | `0.1.0+11096cc7f0c68770fe41ed087cc4743ebcad2b37`, matching `main` HEAD |
| Port after exit | released |

**The policy result: not supported.** .NET 10's supported-OS table lists Windows 10 client
as `21H2 (E)`, `21H2 (IoT)`, `1809 (E)` and `1607 (E)` only. **Windows 10 22H2 does not
appear.** The policy is explicit: *"OS versions that are out of support by the OS publisher
are not tested or supported by .NET."* The one ESU carve-out named is Windows Server 2012
and 2012 R2 — there is no equivalent for Windows 10 Consumer ESU.

Decision: Ship on .NET 10 on Windows 10 Pro 22H2, and record that this is **outside
Microsoft's support policy** rather than implying the green smoke test settles it.

Rejected: Changing the .NET version. The constraint is the **operating system**, not the
runtime — the same "out of support OS" sentence governs every .NET version, so moving to
.NET 8 would buy nothing. Also rejected: reading the successful smoke test as satisfying
#9's policy item. It does not; the artifact running proves the artifact runs.

Consequences: #9's last checklist item, "if .NET 10 is unsuitable, record and implement the
supported target change", has **no in-scope remedy**. The brief fixes Windows 10 Pro 22H2
with no Windows 11 upgrade, so the only change that would restore supported status is one
the brief forbids. This is therefore an accepted risk inherited from a target constraint,
not an open engineering task. It should be revisited only if the owner reopens the OS
question.

**A gap found while testing: the artifact cannot be stopped cleanly out of band.** It
advertises "Press Ctrl+C to shut down", but run headless beside YARG — which is how it must
run — there is no console to deliver Ctrl+C to, and `taskkill` without `/F` refuses because
the process has no window. Only a forced kill worked, which skips graceful shutdown
entirely. Nothing leaked in this run: the process exited and port 5273 was released. But
"verify clean shutdown" in #9 is **not** satisfied, and a service that can only be killed
has consequences for setlist durability
([#7](https://github.com/roguen/cantina/issues/7)) and for process supervision
([#23](https://github.com/roguen/cantina/issues/23)). Barkeep needs a deliberate shutdown
path — an authenticated endpoint, a service host, or a job object — chosen with those
issues rather than assumed.

## D-020 · Barkeep needs no firewall rule, and an ungraceful kill leaves no socket residue

- Date: 2026-08-28
- Status: Accepted; closes two of issue
  [#11](https://github.com/roguen/cantina/issues/11)'s open checklist items. The
  real-lighting-application item stays open.

Context: D-013 proved that two listeners coexist on UDP 36107 only when **both** set
`SO_REUSEADDR`, and that YALCY does not. Two checklist items on #11 were left untested:
firewall-enabled behaviour, and process restart. Both are testable here without the lighting
application, and both carry deployment consequences that were being assumed rather than
known.

Measured on the theater PC against live YARG traffic:

| | |
|---|---|
| Firewall | **Enabled on all three profiles** — Domain, Private, Public |
| Rules for Cantina or YARG binaries | **None.** No allow rule exists for any of them |
| Two listeners, both `SO_REUSEADDR` | Both bound; accepted 2650 and 2649 datagrams, **0 rejected** each, at ~90.8/s |
| Sender → destination | `192.168.68.144:61374 → 255.255.255.255` |
| Force-kill one listener | Survivor unaffected: 3962 datagrams across the whole sequence, 0 rejected |
| Rebind after a force-kill | Immediate — bound at 0.029 s, first datagram at 0.038 s, while the other listener still held the port |

Decision: **Do not add a firewall rule for Barkeep, and do not ask the owner to approve
one.** Reception of YARG's stream works with the firewall fully enabled and no rule present,
because the traffic originates on the same host. A rule would be a permission request with
nothing behind it.

Rejected: Granting the Windows Defender prompt that appears when a listener first binds. It
was declined during the D-018 run and that run still accepted 39,799 datagrams, which is the
same result reproduced deliberately here. Also rejected: treating a forced kill of a listener
as a state needing cleanup — the socket is released immediately and a fresh process rebinds
while another listener holds the port.

Consequences: The `PauseOnFocusLoss` contamination risk that prompt represents (D-018) is now
also a *needless* one — the answer is always to decline. This narrows D-019's clean-shutdown
gap slightly: a force-killed Barkeep leaves no listener residue, so what remains at risk on
an ungraceful stop is application state, not the socket. Setlist durability
([#7](https://github.com/roguen/cantina/issues/7)) therefore owns the rest of that problem.

The sender being the **LAN address broadcasting to 255.255.255.255**, rather than loopback,
is worth stating plainly: YARG's game state is visible to every host on the LAN, by design
and not by Cantina's choice. D-013 already relies on this when it names moving a lighting
controller to another host as the honest interim workaround.

Issue #11 stays open for the item that genuinely needs software this host does not have:
running against **YALCY or Photonics themselves**. D-013's conclusion is unchanged — YALCY
binds without `SO_REUSEADDR`, so it cannot share the port in either order, and nothing
confined to Cantina can change that.

## D-021 · The public-release audit found no disclosure that requires action, and the platform now enforces what discipline alone was holding

- Date: 2026-08-28
- Status: Accepted; closes issue [#10](https://github.com/roguen/cantina/issues/10)

Context: The repository went public on 2026-08-01 (D-011) before its release-gate audit
ran, which inverted the audit's character: anything found is already disclosed, so the
job is remediation, not prevention. The audit swept five surfaces — full-history secrets,
full-history content, the GitHub-side surface, workflow permissions, and the public
face — with every finding independently re-verified before being accepted.

**What is clean, stated with its coverage.** No credential, token, key, certificate, or
connection string in any of the 227 unique blobs across all 66 commits on all refs,
including the 29 pre-rewrite commits still reachable through pull-request refs. No email
address in any blob; commit identities are all `users.noreply.github.com`. No absolute
path leaking a username. Every high-entropy string resolves to an npm integrity hash. No
song content, packet capture, or datagram dump anywhere in history. No GPL text or
GPL-derived code; the Photonics clean-room boundary holds. Zero Actions secrets, deploy
keys, webhooks, environments, or releases. The single workflow is least-privilege
(`contents: read`), SHA-pins every action, interpolates nothing untrusted, and uses no
self-hosted runner.

**The finding that matters: the 2026-08-02 history rewrite did not remove its content
from public reach.** GitHub still advertises `refs/pull/15/head` through
`refs/pull/28/head`, which point at the 29 pre-rewrite commits. They are enumerable with
`git ls-remote` — no known SHA required, which is strictly weaker than the "retrievable
by hash until garbage collection" caveat the Working Agreement recorded. The exposed
material was verified line-by-line: it is only the macOS bootstrap host table (macOS
26.5.2 on arm64, .NET SDK 10.0.301, Node 24.13.0) — no credential, name, or address.

Decision: **Accept the residual exposure and correct the record**, rather than ask
GitHub Support to delete the refs. The content does not warrant a support ticket; the
wrongness of the recorded procedure did warrant fixing, and the Working Agreement now
states that a rewrite alone is presentation, not removal. The owner can take the Support
route later; nothing decays.

**Enforcement gaps closed during the audit** — each was discipline with no mechanism
behind it, and each is now platform-enforced:

| Setting | Was | Now |
|---|---|---|
| Secret scanning + push protection | disabled | **enabled** |
| Private vulnerability reporting | disabled, while `SECURITY.md` pointed at it | **enabled** — the documented channel now exists |
| Dependabot alerts + security updates | disabled, with lockfile-pinned dependencies aging silently | **enabled** |
| Actions policy | any action allowed, pinning unenforced | **GitHub-owned only, SHA pinning required** |
| Repository description | called Barkeep a "bridge", violating D-009 | reworded |

Repository-side fixes in this change: SPDX headers on the last two source files, so all
30 carry `LGPL-3.0-or-later`; `COPYING.GPL` added because the LGPL incorporates the GPL
text by reference and shipped without it; `CODE_OF_CONDUCT.md` and `SUPPORT.md` written
plainly for a one-maintainer hobby project; CI's push trigger filtered to `main` so each
pull-request branch builds once, not twice; and the public artifact stops shipping
`Cantina.Barkeep.pdb` and `appsettings.Development.json`, which were never part of the
deployment.

Rejected: Rewriting history again to chase the LAN address (`192.168.68.144`) out of the
records. It is RFC1918, non-routable, and grants nothing; a rewrite is exactly the
operation this audit just proved does not remove content anyway. The address stays, and
future records simply prefer "the host's LAN address" where the literal value adds
nothing. Also rejected: adopting a boilerplate code of conduct with enforcement
machinery a one-maintainer project cannot honestly operate.

Consequences: Issue #10 closes. The one instance of real library metadata in history —
one song's title, artist, and charters in `spikes/library-ambiguity/README.md` — is
recorded as deliberate, reviewed evidence and stays. The audit's full verified findings
live in the issue #10 closeout comment.

## D-022 · Promise live state from two surfaces, latch identity, and report freshness honestly

- Date: 2026-08-28
- Status: Accepted; closes issue [#12](https://github.com/roguen/cantina/issues/12) by
  publishing [`docs/live-state.md`](../docs/live-state.md) as the normative contract

Context: Issue #12 asked which live-state fields Cantina may promise the iPad, what
happens to the fields the wire does not carry, and how freshness and unknowns are
represented. Every input it was waiting on now exists: the capture-backed wire contract
(D-010, D-012), the second surface `currentSong.json` and its ~86 ms clear (D-010), the
selection-verification loop (D-017), the auto-advance measurement (D-018), and the
LAN-broadcast fact with its multi-sender hazard (D-013, D-020).

Decision, in five commitments — the document carries the details:

1. **Two sources, trust ordered, disagreements surfaced as `ambiguous`** rather than
   resolved silently. The datagram carries scene and play state; `currentSong.json`
   carries identity; Barkeep's own state carries the setlist.
2. **Song identity is latched**, captured when `currentSong.json` populates and held
   through the score screen, because the file clears ~86 ms after the scene changes. The
   raw file being empty does not un-know the song.
3. **Freshness is a three-tier promise** — `live` under 500 ms, `stale` to 5 s, `dead`
   beyond — with a 1 s debounce before demoting, because a healthy run showed a 538 ms
   gap (D-018). The client never renders `stale` or `dead` as current, and `dead` names
   its cause when Barkeep knows it (port conflict is a named fault per D-013).
4. **Absent fields stay absent honestly.** Playback position is deferred to the upstream
   hook and never dead-reckoned from BPM (D-010's rejection stands). Score values,
   menu-screen identity, and YARG's internal queue are not promised at all.
5. **An advance is an observation, not an inference**: `score → gameplay` plus a
   different hash within 15 s. Same hash is a restart; `score → menu` over 5 s is
   players-at-the-screen, a first-class third outcome (D-018). Who initiates an advance
   is [#39](https://github.com/roguen/cantina/issues/39)'s decision and is deliberately
   not constrained here.

Rejected: Deriving progress from BPM and beat pulses (re-rejected; D-010). Promising
fields observable only on screen. Treating the wire as the single source when the game
states identity on disk. Resolving multi-sender interleaving by picking a sender, which
manufactured a withdrawn finding once (Time Log session 009) — `ambiguous` with both
endpoints named is the only honest report.

Consequences: Issue #12 closes. The M4/M5 upstream work inherits a precise scope:
position only, because identity is already served. The client contract in
`architecture.md` now points at the live-state document instead of restating it. Issue
[#8](https://github.com/roguen/cantina/issues/8)'s honest-failure copy has the state
vocabulary it needs (`ambiguous`, `stale`, `dead`, named faults) without inventing its
own.

## D-023 · Durability is write-ahead at mutation time, because graceful shutdown does not exist

- Date: 2026-08-28
- Status: Accepted as the durability semantics for issue
  [#7](https://github.com/roguen/cantina/issues/7); the issue stays open until the
  implementation passes the crash matrix on this host

Context: Barkeep owns the setlist, and #7 asks what survives iPad backgrounding, Barkeep
restart, YARG restart, reboot, and logoff. Three measurements now constrain the answer.
D-019: a headless Barkeep has no console for Ctrl+C and no window for `taskkill`, so
today it can only be killed — **durability cannot live in a shutdown hook, because the
hook will not run**. D-020: a killed listener leaves no socket residue and a fresh
process rebinds in ~30 ms — recovery is not racing the port. D-018: YARG holds the score
screen indefinitely — a restart mid-set has time, because the theater does not move
underneath it.

Decision:

- **Write-ahead, acknowledge-after.** A mutating request appends `{id, intent}` to a
  journal and flushes before Barkeep acts on YARG or replies to the iPad. The observed
  outcome — `done | failed | ambiguous` — is appended when known. Durability is a
  property of the acknowledgement, never of process exit.
- **What survives what:** the setlist and cursor survive everything (journal); an
  in-flight command survives as `ambiguous` if its outcome was never appended; the live
  YARG projection survives nothing and is rebuilt from the stream in under a second.
  Client state remains a discardable projection (architecture.md).
- **Recovery never re-executes.** An intent without an outcome becomes `ambiguous` on
  start, verified against `currentSong.json` and the datagram before anything is
  re-attempted, and surfaced to the iPad for confirmation. This is the existing
  architecture.md rule, now with its storage story attached.
- **Idempotency by client-supplied command id.** A duplicate id is answered from the
  journal, not re-run.
- **Storage is a JSON-lines journal plus a compacted snapshot**, atomic-renamed, under
  Barkeep's own data directory, with a `version` field. A snapshot that fails to parse
  is set aside as `*.corrupt-<timestamp>`, the previous snapshot is used, and the iPad is
  told state was recovered and from when. A user-visible reset deletes journal and
  snapshot explicitly. Unknown versions refuse to guess and offer the reset.

Rejected: SQLite or any embedded database — one writer, one host, at most a few hundred
small records between compactions; a database earns its place only if the journal
measurably cannot keep up, which at theater scale it cannot fail to. Rejected: writing
anywhere near YARG's own files. Rejected: relying on `ProcessExit`/`SIGTERM` handlers as
the durability mechanism, which D-019 shows would simply never fire on this host today —
a deliberate shutdown path remains worth adding
([#23](https://github.com/roguen/cantina/issues/23)), but correctness must not depend on
it.

Consequences: The crash matrix that closes #7 is fixed now, and lands with the
implementation: kill −9 mid-append; kill between YARG action and outcome append; corrupt
snapshot recovery; reboot with a queued setlist. Each must show no duplicate execution
and an honest `ambiguous` where the outcome was unobservable. Issue #23 inherits the
deliberate-shutdown question with the pressure removed — shutdown becomes a nicety, not a
correctness dependency.

## D-024 · Focus loss pauses the game and focus regain does not resume it; cues fail closed on named readiness signals

- Date: 2026-08-28
- Status: Accepted; closes issue [#8](https://github.com/roguen/cantina/issues/8) by
  publishing [`docs/failure-behavior.md`](../docs/failure-behavior.md), and corrects a
  mechanism three spike comments had recorded backwards

Context: Issue #8 asked what Barkeep can observe about theater contention — Holocron and
YARG share the PC, display, receiver, and HDMI audio endpoint — and how commands fail
honestly when YARG is hidden or unreachable. The contention was reproduced on the theater
PC with the wire as oracle: foreground stolen by another application mid-run, and
Holocron itself launched fullscreen with audio while YARG ran.

Measured:

- **Losing foreground mid-song pauses the game with no key behind it** — `PlayState
  1 → 2` on the wire the moment another window took focus.
- **Regaining foreground does not resume.** The pause survived four verified focus
  regains across two runs; byte 7 sat at `0x02` throughout. The only resume is the pause
  menu's `RESUME` entry. Three spike comments and the observation skill asserted the
  opposite ("focusing YARG resumes it") — an inference recorded as fact, never measured
  until now. All four texts are corrected in this change.
- **The pause menu is a blind hazard, concretely.** Stray Escapes had left its cursor on
  `BACK TO LIBRARY`; a blind Enter "to resume" would have destroyed the paused setlist.
  Only a screenshot caught it. The menu titles itself `SETLIST PAUSED`, and the paused
  setlist context survived fourteen hours untouched.
- **The datagram never stops.** Full rate while backgrounded, paused, and hidden —
  fourteen hours continuously. Under fullscreen Holocron the rate sagged to 74–81/s with
  zero gaps and zero rejections, recovering to ~90.6/s the instant Holocron died.
- **Holocron takes foreground on launch**, so launching it mid-song silently pauses
  YARG. Both processes ran concurrently without either failing; audible mixing is not
  observable by Barkeep and is not promised.

Decision: Readiness is five observable signals — process alive, stream live, single
sender, YARG foreground, and not-paused — and **a cue is attempted only when all five
hold**. Anything less fails closed before any input is sent, with the failing signal
named to the iPad in the vocabulary of D-022. Pause attribution uses the foreground
sample at the transition instant: the same `1 → 2` bytes are "a player paused" with YARG
foreground and "another application took the screen" without it. Recovery from
contention belongs to the players, because the pause menu is exactly the blind surface
D-015 excluded; Barkeep reports, and does not press.

Rejected: Driving the pause menu to auto-resume, which the cursor incident demonstrates
is Russian roulette against the setlist. Treating focus regain as recovery, which is now
measured false. Any Holocron coordination beyond observing who has the screen — no IPC,
no lockfile, no protocol; Cantina does not control Holocron and does not pretend to.
Inferring audio health from process liveness.

Consequences: Issue #8 closes. The observation skill and both input-adjacent spikes now
carry the corrected mechanism. Issue [#23](https://github.com/roguen/cantina/issues/23)
gains a measured input: a Barkeep-launched YARG would come up foreground, but any later
launch of anything else silently pauses gameplay, which strengthens the case for Barkeep
*watching* foreground rather than managing it. The M2 cue pipeline implements the
five-signal gate as its precondition check.

## D-025 · The filesystem is the metadata source, and YARG's hash is learned, never computed

- Date: 2026-08-28
- Status: Accepted; closes issue [#5](https://github.com/roguen/cantina/issues/5)

Context: Issue #5 asked which source is authoritative for song metadata. The candidates:
YARG's `songcache.bin` (binary, undocumented, version-coupled — a private surface of the
kind rule 6 exists to keep Cantina off), or the song folders themselves — `song.ini`
beside the note files, which is what YARG itself reads. The theater library was measured
before deciding: 447 folders, every one carrying `song.ini`, zero `.sng` archives yet,
and YARG's settings name exactly one source directory.

Decision:

- **Index the filesystem** — the same `SongFolders` YARG's settings name, so search
  results are always cueable. `song.ini` is parsed directly; a folder that cannot be
  indexed is skipped with a named reason, never silently dropped.
- **The folder path is the join key.** `currentSong.json` states `ActualLocation`, and
  Cantina knows every indexed folder — so observation and index join on path.
- **YARG's content hash is learned, never computed.** The algorithm is YARG's private
  detail; computing it would couple Cantina to internals. The first time a song is
  observed loaded, its stated hash is joined to the indexed folder and kept across
  rescans. Cue verification matches on location first, learned hash second.
- **Cantina's search is plain substring**, ranked title > artist > album > charter —
  deliberately unlike YARG's fuzzy search, whose widening D-017 measured. Predictable
  results are the point when a wrong selection costs a whole song.
- **`.sng` metadata is deliberately unimplemented** until the first real archive lands
  (D-007's handoff baseline): each one found is reported by name rather than parsed
  against a guessed format.

Rejected: Reading `songcache.bin`. A database (the D-023 argument at the same scale:
447 songs, one host, in-memory wins). Computing YARG's hash. Fuzzy matching.

Consequences: Issue #5 closes. Measured on this host at first run: **447 of 447 folders
indexed in 89 ms with zero skips**, and the learned-hash join fired within a minute —
YARG sat paused on *The Unforgiven* and the index acquired its hash from pure
observation. Charter fields carry YARG's inline color tags raw; stripping is the
client's display concern, not the index's. One open observation: YARG's library screen
displays "652 SONGS" against 447 folders on disk from its single configured source —
unexplained, recorded rather than reconciled, and worth revisiting if selection ever
misses.

## D-026 · Physical presence is the pairing authority, and a theater certificate authority makes rotation free

- Date: 2026-08-28
- Status: Accepted; implements issue [#6](https://github.com/roguen/cantina/issues/6)

Context: Barkeep has been loopback-only by design since M0, with `docs/security-model.md`
holding the open questions. Issue #6 asked for seven answers before it binds anything
wider: discovery, pairing and credential lifecycle, certificate issuance and iPad trust,
origin and host validation, the least-scope firewall rule, reconnection without replay, and
a recorded threat model.

Measured on the theater PC first. It is `HOME-GRIFFEN-PC` at `192.168.68.54/24` by **DHCP,
not reservation**, and it carries a **Tailscale interface at 100.102.146.115** as well as
its Ethernet one — which is the whole argument against binding `IPAddress.Any`.

Decision:

- **One explicit interface, never `Any`.** `Network:Mode` stays `Loopback` by default;
  `Lan` binds one address, chosen explicitly or from the interface holding the default IPv4
  gateway. If `Lan` is configured and nothing resolves, Barkeep refuses to start. Silently
  falling back looks healthy while being unreachable; silently binding everything publishes
  the theater to a network nobody asked about.
- **Physical presence at the theater PC is the pairing authority.** A pairing window can be
  opened only from loopback, and the code it produces is shown there and nowhere else — not
  on the onboarding page, not in `/api/onboarding`, not to any LAN client. A device Barkeep
  already trusts still cannot authorise another one.
- **A theater certificate authority, not a self-signed leaf.** The iPad trusts one
  ten-year authority once; the server certificate it signs lives 397 days — inside Apple's
  398-day ceiling — and is re-issued automatically whenever it stops naming where Barkeep
  answers. That makes a DHCP address change and annual rotation invisible to the iPad,
  which a self-signed leaf would not.
- **Bearer tokens and no cookies at all.** 256 bits, handed out once, stored as a SHA-256
  hash and compared in fixed time. No cookie anywhere is what makes cross-site request
  forgery structurally impossible rather than merely defended against.
- **The live socket takes a single-use thirty-second ticket**, because a browser cannot put
  a header on a WebSocket and the alternatives put a long-lived credential in a URL that
  gets logged.
- **Plain HTTP on the LAN carries onboarding and a `307` to TLS**, never control. `307`
  rather than `302`, because a redirect that turns a POST into a GET drops a command and
  reports success.
- **Barkeep prints the firewall rule and never runs it.** Two TCP ports, private profile,
  the theater's own subnet, one program.

Rejected: mDNS as a dependency (see below). Binding `IPAddress.Any`. A self-signed leaf.
Cookies or sessions. The token in the WebSocket URL or in `Sec-WebSocket-Protocol`. Letting
a paired device open a pairing window, which would make the first device a permanent
authority rather than a peer.

Consequences: `docs/lan-transport.md` is the normative contract; `docs/security-model.md`
now points at it. 22 new server tests and a new `Cantina.SelfTest run lan` suite, which
**passed 8 of 8 on this host against a real LAN binding**: both plain-HTTP cases, a TLS
handshake whose certificate the client validated by name and by chain against the served
authority with the machine's own root store taking no part, pairing over the wire,
ticketed socket connect, single-use rejection on reuse, reconnect, no command replay, and
immediate revocation.

**Two things this did not prove, and neither should be described otherwise.**

*mDNS is inconclusive, with a named cause.* A query for this host's own `.local` name and a
`_services._dns-sd._udp.local` enumeration both drew zero answers in six seconds, and
`svchost` does bind UDP 5353 here. That looked like a finding until the screen was captured
for an unrelated reason and showed a Windows Defender dialog stating it had **blocked
PowerShell on all public and private networks** — the measurement ran through a blocked
socket, so it measured the firewall. This is the project's recurring trap firing again:
something the tool could not see was doing the work. Whether iPadOS resolves this host by
name needs two devices and remains unmeasured, which is why the design uses mDNS for
nothing.

*Reachability from another device is unproven.* Every client that reached the LAN binding
was on this host, where traffic to the host's own address does not cross the inbound
filter. All three firewall profiles are enabled; the rules could not be enumerated without
elevation, so nothing is claimed about what exists. Binding TCP 5273 and 5274 raised no
Defender prompt at all, unlike the UDP bind of D-020 — recorded, not explained. The
remaining gap needs the printed rule, which is the owner's to run, and an actual iPad.

## D-027 · Check that input can arrive before sending it, because Windows discards it silently

- Date: 2026-08-28
- Status: Accepted; advances issue [#3](https://github.com/roguen/cantina/issues/3)

Context: Issue #3 asks for evidence about "focus loss, elevation mismatch, lock/logoff, and
held/repeated input". Focus loss was measured (D-024) and held input was fixed in the
production actuator, which wraps every key-down in a try/finally the spikes lacked. The
other two share a property that makes them dangerous: **Windows blocks the input silently.**
`SendInput` returns success, the accepted-event count is correct, and the game receives
nothing. That is byte-identical to a delivered keystroke from Barkeep's side, and it is the
exact shape that produced this project's second wrong conclusion — every failed typing run
of D-017 had 100% acceptance.

Proving those conditions by causing them would mean running YARG elevated or locking the
theater PC. Both change the machine, and neither is something to do on the owner's behalf.

Measured instead, read-only, on 2026-08-28: **YARG runs at Medium integrity in Windows
session 1**, the same as Barkeep and every other process here. That is why D-014's proven
path works at all, and it was previously an unstated assumption.

Decision: an unprovable hazard becomes a **named readiness signal that fails closed**.
`inputDeliverable` joins the five signals of `docs/failure-behavior.md` and is checked
before anything is sent, covering the three conditions that swallow input without saying so:

- **A locked workstation** — the input desktop becomes Winlogon's and injected events land
  where YARG is not. Detected by `OpenInputDesktop` failing.
- **A session boundary** — input does not cross Windows sessions. This is the same fact that
  makes `architecture.md` refuse to assume a service deployment.
- **An integrity mismatch** — User Interface Privilege Isolation discards input from a
  lower-integrity process to a higher-integrity one, and does not report it.

An unreadable token is reported as **unknown**, not assumed equal: a guess here would be a
guess about whether the whole control path works.

Rejected: causing any of the three to prove the failure mode, because it changes the
operator's machine. Inferring deliverability after the fact from a failed cue, because a
discarded keystroke and a delivered one are indistinguishable from here. Assuming Medium
integrity because that is what was measured today — the check runs every time, since the
condition is a property of how YARG was launched, not of the build.

Consequences: the gate now refuses by name in three more cases, before any input is sent.
`Cantina.SelfTest run readiness` reports the signal through the production actuator rather
than a reimplementation, and on this host reads "no integrity, session, or desktop barrier
between Barkeep and YARG". #3's lock/logoff and elevation items are answered as *detection
and refusal* rather than as an untested claim that they work.

**A correction found while doing this, worth recording on its own.** Every
`Cantina.SelfTest` transcript has opened with `attest_no_input=true (links no SendInput/
keybd_event/mouse_event/SetForegroundWindow)`. That line is true of
`Cantina.Spikes.YargSetlist`, where innocence is a property of the assembly, and it was
carried into this tool. It has been **false since the cue suite landed**, because
`CueSuite` constructs `Win32YargActuator` in the same assembly. The attestation now states
the weaker truth — which suites in *this run* send input — and names where the strong
guarantee actually lives. A false attestation printed on every run is worse than no
attestation, because it is read as evidence.

## D-028 · Stock YARG is a go, and the three things it cannot do are named rather than worked around

- Date: 2026-08-28
- Status: Accepted; closes the M1 milestone item "record a stock-YARG go/no-go decision"

Context: D-005 set the rule — prove stock YARG before proposing an upstream hook. The
kickoff brief's four capabilities were not obviously reachable without one: YARG exposes no
remote-control API, ViGEmBus is retired, and the first reading of YALCY's parser suggested
the datagram carried neither song identity nor position. M1 existed to find out. It is
finished, so this entry records the verdict.

**The verdict is go.** Every capability the brief asked for is delivered on stock YARG 0.15
with no game modification, and the product core has been exercised end to end on the
theater PC.

What carries it, each with the evidence rather than the claim:

- **Observation, from two surfaces.** The 47-byte version-3 datagram parses over 80,000
  real packets with zero rejections (D-010), byte 7 is a three-state play state (D-012), and
  `currentSong.json` supplies the identity the datagram omits. `Cantina.SelfTest run live`
  passes here against the running game.
- **Selection, as request-and-verify.** `SendInput` with scan codes drives stock YARG from
  a background process without taking foreground (D-014), scope is narrowed to choosing a
  song (D-015), and the cue gate refuses by name before sending anything (D-024, D-027).
  Recorded on 2026-08-28 by the `cue` suite: staged, cued, players stood in, verified by
  outcome in 15 s.
- **A setlist that survives the host.** Write-ahead at mutation time because graceful
  shutdown does not exist here (D-023), with the crash matrix passing under **real process
  kills**.
- **A remote that is actually a remote.** One explicit LAN interface over TLS that chains
  to a theater authority, pairing gated on physical presence, and Barkeep serving the iPad
  its own client (D-026). `run lan` passes 10 of 10 against the published binary.
- **Latency that is not a problem.** Search p50 0.5 ms, a journaled command round trip p50
  1.3 ms, delivered state p50 2.3 ms old (M5 measurement, 2026-08-28).

**Three things stock YARG cannot do, and what Cantina does instead.** None is worked
around by guessing; each is either refused honestly or specified as an upstream ask in
`docs/upstream-interface.md`.

1. **No playback position, anywhere.** Dead-reckoning from BPM was rejected (D-010) because
   there is no length, no seek, and no reconciliation, so the indicator would drift with no
   way for the client to notice. Cantina ships no progress indicator at all.
2. **No menu-screen identity.** `CurrentScene` reports `Menu` for the start menu, the song
   list, settings, and instrument setup alike (D-015). Cantina therefore cannot confirm a
   precondition before typing, and answers by verifying afterwards and by handing the blind
   surfaces — instrument setup, the pause menu, the score screen — to the players.
3. **No keyboard route to the search field, and a fuzzy search.** Selection needs a pointer
   click at a screen coordinate (D-017), and a query resolves to whatever YARG ranked first
   across 652 searchable entries against 447 indexed folders (D-025). Cantina reads back
   what actually loaded rather than assuming the search hit what it asked for.

**What would make this a no-go later, stated now so it is recognised when it happens.** The
control path contains exactly one element that cannot be verified before it is used: the
click coordinate. A YARG update that moves the search box breaks song selection **silently**
— the click lands somewhere harmless, the typing goes nowhere, and the cue fails by outcome
with no indication of the cause. Verify-by-outcome contains the damage but does not explain
it. That is why a keyboard focus route is the *first* ask upstream, ahead of the more
valuable position field: it is the one change that would remove a standing fragility rather
than add a feature.

Rejected: proposing an upstream hook before proving the stock path, which D-005 forbade and
which the captures would have made embarrassing — the first reading of the parser was wrong
about identity. Shipping a derived progress indicator. Treating a sent keystroke as
evidence. A virtual-controller path, because ViGEmBus is retired and has no maintainable
installation story for a theater PC.

Consequences: M1 closes. What remains open is not about whether stock YARG suffices: it is
coexistence with a real lighting consumer (#11, needs software this host lacks), the
Geomitron Bridge acquisition boundary (#17, needs the Bridge exercised), the two hardware
steps of #6, and one scope question that is the owner's alone — whether Cantina presses
`CONTINUE` on the score screen (#39). None of those would be improved by a go/no-go that
waited for them.

## D-029 · The theater has a name, and the certificate comes from outside; the private authority becomes the fallback

- Date: 2026-08-29
- Status: Accepted; **supersedes the certificate half of D-026**, which stands in every other
  respect — binding, pairing, tokens, tickets, origin and host validation, rate limits, and
  the firewall rule are unchanged

Context: D-026 built a private theater certificate authority because Cantina had no name to
be issued a certificate *for*, only `192.168.68.54`. That was the right answer to the
question as it stood and the wrong answer to the question the owner actually had. He asked
for a real subdomain, which changes what is possible: with a name under a zone he controls,
a publicly trusted certificate is obtainable **without exposing anything to the internet**,
because DNS-01 validation never requires the host to be reachable.

The site was already doing exactly this. `aero4ge.com` is on Cloudflare; `ha-blue` and
`ha-blanc` both hold Let's Encrypt certificates issued over DNS-01; the ACME machinery lives
on the NAS with a deploy key it already uses to push certificates to the CloudKey; and
`nas.aero4ge.com` has resolved publicly to a private address for some time. Cantina was
about to reinvent all of it, worse.

Decision:

- **`cantina.aero4ge.com` is the address.** A record created 2026-08-29, DNS-only, pointing
  at `192.168.68.54`, whose DHCP reservation was confirmed first — a public name over an
  unpinned lease breaks silently.
- **Barkeep serves a supplied certificate when `Network:CertificatePath` is set**, and in
  that case **creates no authority at all**. Not as a spare, not as a fallback on disk: two
  certificates where one is unused is a private key nobody is watching.
- **Barkeep runs no ACME client.** Issuance and renewal are a job this network already does
  for other services; doing it here would put a second copy of a DNS credential on the
  theater PC. Barkeep's whole contribution is to load a file and serve it.
- **A configured certificate that will not load is fatal.** No silent fall back to the
  private authority — a server quietly serving a different certificate than the operator
  configured is a server whose clients fail in a way nobody can explain.
- **Certificate expiry is a reported health signal**, on `/api/health` and in the client.
  This is the mitigation for the one way a public certificate is *worse* than the private
  authority: renewal is done by machinery Barkeep does not own and cannot see, so a renewal
  that quietly stopped presents as a theater that works perfectly until a day weeks later
  when nothing connects. The private authority has the opposite shape — a ten-year anchor
  and a leaf Barkeep reissues itself — so the signal is reported for both and alarming for
  one, and the client copy says which.
- **The private theater authority remains, demoted.** It is what a site with no domain, no
  internet, or no wish to run ACME still gets, and it needs no renewal machinery at all.

Rejected: an ACME client inside Barkeep. Falling back silently when a configured certificate
is missing. Keeping the private authority on disk alongside a supplied certificate. Asking
the owner to install a general-purpose trust anchor on his personal iPad — one able to sign
for any domain, with its private key protected by a profile ACL — to avoid work the network
had already done twice.

Consequences: the iPad's setup loses three of its five steps. No profile to install, no
fingerprint to compare, no Certificate Trust Settings toggle; the onboarding page reduces to
one sentence and a link, and `/cantina-theater-ca.cer` answers 404 because there is no
authority to distribute. Measured on the theater PC the same day, still on the private
authority because the public certificate has not been issued yet: `Cantina.SelfTest run lan`
passed **10 of 10 against `cantina.aero4ge.com`**, with the client validating **by name** as
well as by chain, and the plain-HTTP redirect now targeting the name rather than the address.

Two things this does not yet do. **The certificate is not issued** — the name is added to
the NAS issuer as a separate, approved change. And **nothing watches the renewal from
outside Barkeep**: the health signal is visible to whoever looks at the iPad, which is better
than nothing and is not a monitor. The same gap the network's own records describe against
its backup chain, and worth closing the same way.

## D-030 · The Geomitron Bridge handoff is real: the first .sng landed, Scan Songs is drivable, and the 652 mystery is closed

- Date: 2026-08-29
- Status: Accepted; implements the core of issue [#17](https://github.com/roguen/cantina/issues/17)

Context: the acquisition pipeline existed as policy code exercised only by semantic fakes
(D-008), gated on evidence nobody had: no `.sng` file had ever existed on this host, no one
had proven YARG's library could be refreshed without a restart, and D-025 deliberately
refused to parse a format it had never seen. The owner delegated the one human step —
driving Geomitron Bridge's UI as his stand-in, once — with "get this project done
autonomously."

What was measured, in order, all on 2026-08-29:

- **Bridge's own UI was driven once, as the operator.** Its library path already pointed at
  a dedicated subfolder of the YARG source (`Songs\Bridge`, 49 extracted folders — the
  operator already uses it); the one change made was `.sng` retention on, through its
  Settings screen, corroborated afterward by the settings file changing to `isSng: true`.
  One song was downloaded: **Foo Fighters – Everlong (Hoph2o)**, 2,566,282 bytes, drums-only
  chart. The product still automates none of this (D-007, rule 6); an agent standing in for
  the operator is not a Cantina mechanism.
- **The `.sng` version-1 layout was read off the real file**: `SNGPKG` magic, uint32
  version, 16-byte seed, then length-prefixed UTF-8 metadata pairs carrying the same
  vocabulary as `song.ini` (31 pairs on this file), then a file table Cantina does not
  touch. `SngDocument` implements exactly that, and the index now treats an `.sng` as a
  song whose location is the file path — the same D-025 join key, because
  `currentSong.json` names the archive path for an archive-loaded song.
- **Scan Songs is drivable and bounded.** The Music Library's MORE OPTIONS control opens a
  popup whose third entry is SCAN SONGS; both are pointer-clickable — measured at
  (1340,2064) and (1903,939) on 3840×2160, evidence not constants, same contract as
  D-017's search box. The scan completed in seconds. **It has no completion signal on any
  observable surface**, so the production sequence is open-loop and time-bounded, and the
  cue's read-back is what proves visibility.
- **The scan resolved D-025's open discrepancy.** The library count went **652 → 448**:
  447 folders + the new archive. The unexplained 652 was a stale `songcache` surviving
  from before this library's history; a fresh scan counts what is on disk. Cantina's index
  and YARG's library now agree exactly — 448 = 448 — for the first time.
- **The pipeline ran end to end against the real world**: Detected → Stabilizing →
  Validating → Indexed → RefreshPending → YargVisible → Queued → **Cued**, outcome
  **Completed**. Everlong sits next in the durable setlist, and YARG reached SELECT
  INSTRUMENT for it with the cue honestly `pending-players` — instrument setup belongs to
  the players (D-015), and a drums-only chart makes that boundary visible: the screen
  offered exactly what the chart carries.

Decision:

- **The watcher treats events as hints.** Every hint funnels through one queue; a startup
  sweep and a periodic sweep re-enqueue everything; the import journal makes duplicates
  free. Arrival identity is name + length + write time, so a re-download imports again and
  a re-notification replays.
- **Stability is two signals, not one**: size unchanged across a probe interval *and* no
  writer holding the file. Containment refuses anything that resolves outside the watch
  root, by name.
- **The import journal is D-023 discipline applied to acquisition**: lease flushed before
  work, receipt flushed at outcome, torn tails tolerated. A crashed import is claimable
  again — every step is idempotent, so re-running converges. A **completed** import never
  reruns; an **ambiguous** one needs eyes, not retries; a **failed** one gets one fresh
  chance per sweep, because failure usually means the world was wrong, not the file.
- **`WaitForSongVisibleAsync` is a named no-op.** YARG's library is not observable — the
  wire says nothing, the count is pixels, `songcache.bin` is off-limits (rule 6) — so
  visibility is proven where it can be: the cue reads back `currentSong.json` and matches
  the path. Claiming otherwise would be a claim with no mechanism behind it.
- **Play-next is a first-class setlist intent** (`InsertNext`): after the cursor, cursor
  unmoved, idempotent by command id through the same journal as every other mutation.
- **Acquisition is off unless `Acquisition:WatchDirectory` is configured**, and Windows-only,
  because the refresh drives menus. Barkeep never reads Bridge's settings to discover the
  directory; the operator names it (docs/geomitron-bridge-integration.md, phase 1 step 3).

Rejected: parsing `songcache.bin` to observe visibility (private surface). Deriving the
watch directory from Bridge's `settings.json` (same coupling, other direction). A
single-entry index insert instead of a rescan (a second copy of what-is-a-song). Extracted
chart folders as an arrival shape (unproven; `.sng` only, per the contract). Trailing
Escape after the scan clicks to "clean up" (from the library, Escape navigates to the
start menu — tidying that can wrong-foot the next cue).

Consequences: the README's acquisition sentence is now implemented end to end. What remains
of #17 is the folder-arrival question (deliberately out of scope), a SelfTest acquisition
suite so the proof reruns without an agent driving, and latency measurement of the handoff
itself. The 652-line in D-025's consequences is answered here. The cue pipeline gains a
known gap worth naming: during instrument setup the wire reads Menu/NoSong — indistinguishable
from idle — so a second cue dispatched then would type into the setup screen; pre-existing
menu blindness (D-015), recorded rather than fixed.

## D-031 · A config-gated debug surface may stand in for the players, and four owner decisions land at once

**2026-08-30.** The owner asked for a way to kick off a cued song from the iPad with
nobody holding an instrument — "for testing at least". That is a deliberate carve-out
from D-015's rule that instrument setup belongs to the players, and it is built as one:

- `Debug:Enabled` is **off by default**, and while off the surface is invisible — both
  endpoints answer 404 and the client draws nothing. Enabling it is a bench
  configuration, not a product feature.
- The stand-in (`POST /api/debug/players`) refuses by name unless a cue is actually
  `pending-players`, YARG is single-instance, observable, live, still on a menu, and the
  input can arrive. It then sends one ready confirm per configured player at the cue
  suite's measured cadence (2000 ms lead, 1500 ms between — the sequence the acceptance
  run proved).
- Sending is not success. The confirms prove nothing; the cue still resolves only when
  the confirmation poller observes gameplay, exactly as for real players.

Recorded in the same entry, three more owner decisions from the same conversation:

1. **Direct chart-provider integration is approved** — the terms review and build for
   in-app search/download against Chorus Encore, since Geomitron Bridge has no external
   interface (upstream #96/#97/#98). Bridge remains un-automated (D-007).
2. **#39 is decided: Cantina presses CONTINUE as well as the players.** The score screen
   takes one key (D-018); M5 auto-advance may drive it under the usual gates.
3. **Pairing-code email goes to `admin@aero4ge.com` to start.** The future enhancement —
   sending from `cantina@aero4ge.com` to whatever address requests it — is recorded
   *with its cost*: a requester-supplied destination lets any LAN device mail itself a
   code, so it must not ship without a compensating control (an allowlist, or
   operator approval per address).

## D-032 · Cantina speaks to Chorus Encore directly, and the missing terms are answered with posture

**2026-08-30.** The owner approved direct chart-provider integration, which the iPad's
"look for and add a song" ask requires: Geomitron Bridge has no external interface
(upstream #96/#97/#98), and D-007 keeps it un-automated. The provider is Chorus Encore
(enchor.us) — the service Bridge itself is the desktop client of, same author, GPL-3.0.

The terms review (docs/chart-provider.md) found **no published API terms anywhere** —
not on the site, the repository, or the Patreon. D-030's bar asked for explicit
permission; the honest finding is that no document can grant it. What stands in is
recorded posture, held in code rather than intention: a self-identifying User-Agent so
Encore's operator can see or block Cantina by name, person-initiated searches behind a
server-side cooldown, one download at a time under a 30/hour ceiling, no crawling or
mirroring ever, and a single `Encore:Enabled=false` kill switch honored the day the
operator objects. The service is donation-funded with bandwidth as its stated top cost;
Cantina behaves like a guest who knows that.

Two structural choices worth keeping:

- **There is no second import path.** The coordinator stages outside the watch
  directory, validates the SNGPKG header (a byte stream from the network proves
  nothing), and moves the finished file in under Bridge's own naming convention. From
  there the proven D-030 pipeline owns it, and the arrivals feed reports the outcome.
- **The md5 is identity, not checksum** — the `_novideo` variant serves different bytes
  under the same md5, so bytes are never verified against it; the header parse is the
  validation.

Content use is unchanged in kind from Bridge: local personal play, never redistributed,
never committed, charter attribution carried end to end.

## D-033 · A pairing code may be emailed, and the widened trust anchor is named

**2026-08-30.** The owner asked for pairing codes by email rather than a walk to the
theater PC every time. D-026 made physical presence the credential; this entry widens it
deliberately: **"can read the operator's inbox" now stands in for "is standing at the
theater PC."** The widening is held narrow four ways:

- **The destination is operator configuration (`PairingEmail:To`), never client input.**
  The owner's original ask was a field where the requester types an address; that shape
  lets any device on the LAN mail itself a code, so it was declined and the owner
  accepted the configured-address form. His future wish — sending from
  cantina@aero4ge.com to whatever address asks — is recorded as an enhancement that
  must not ship without a compensating control (an allowlist or per-address operator
  approval).
- A small ceiling (3/hour) with the refusal pointing back at the console.
- The requester's address is named in the message, and an unexpected email is itself
  the alarm.
- The console still prints every code; an open window is reused rather than replaced,
  so a code the operator just read survives the tap.

Theater configuration: To=admin@aero4ge.com, From=cantina@aero4ge.com, via
mail.aero4ge.com:25 STARTTLS (the house host accepts local delivery credential-free and
refuses relay, so non-local destinations will not deliver until forwarding exists).

## D-034 · The score-screen advance ships players-first, and the unknown menu is answered by the cue's own verification

**2026-08-30.** Implements the owner's #39 decision (Cantina presses CONTINUE as well as
the players). The shape that survives the observation rules:

- **Off at startup, armed from the iPad.** A show is armed deliberately.
- **The grace period belongs to the players** (default 6 s). If they dismiss the score
  screen themselves, Cantina never presses — it only cues what comes next.
- **One CONTINUE, bounded attempts (2 per score screen), never a hammer.** Every gate a
  cue runs — single instance, live wire, deliverable input, verified foreground — runs
  before the press.
- **The advance is the ordinary cue pipeline.** After the score screen dismisses to a
  menu and it settles, the next setlist entry goes through YargCueService: same journal,
  same pending-players (instrument setup still belongs to the players, D-015), same
  verify-by-outcome. The cursor moves only after the cue confirms the right song loaded.
- **The wire cannot say which menu the score screen dismisses to** (D-015/D-018). The
  cue's verification is the honest answer: if YARG landed somewhere the search click
  cannot reach, no song loads and the episode fails by name. The target-PC acceptance
  run is the proof that the landing screen is the Music Library; until it runs, that is
  recorded here as assumed-and-checked-by-outcome, not known.

## D-035 · The requester names the destination, and the operator hears about every grant

**2026-08-30.** The owner overrode D-033's deferral in plain words: "let's just go
ahead with this end state and have a field that I can provide my email to," sender
cantina@aero4ge.com. The risk D-033 named — any LAN device can mail itself a code —
is accepted by the owner and held down with controls rather than a refusal:

- **`PairingEmail:AllowRequesterAddresses` gates the whole path**, default off; the
  operator turns it on knowingly.
- **Every requester-addressed send mails the operator a notification** naming the
  destination address and the requesting device — the grant is never silent, and an
  unexpected notification is itself the alarm. The notification deliberately omits
  the code.
- The hourly ceiling, single-use codes, the 10-minute window, and five-wrong-attempts
  closure all still apply, and the console still prints every code.

Delivery to arbitrary destinations needs authenticated submission: the house
docker-mailserver relays externally only for authenticated @aero4ge.com senders
(SMTP2GO, per the echobase record). The transport gained AUTH PLAIN; the identity is
cantina@aero4ge.com and the password lives at a file path
(`PairingEmail:SmtpPasswordPath`) — the value in exactly one place, never in
configuration dumps, transcripts, or argv. The mailbox itself is created on the NAS
by the operator, interactively, per the house procedure.

## D-036 · The operator-requested acceptance run: one defect, one measured assumption, one subtlety

**2026-08-30, evening.** The owner asked for the full click-through — "go through and
click on the buttons in this app, make sure that it is operating within the YARG
application as it should" — driven over loopback against live YARG, screenshots before
every blind step. Three findings:

1. **A defect, fixed the same hour.** The first screenshot showed YARG's search box
   holding two cue typings interleaved — a double-tapped Play now had raced two
   keystroke sequences together, matched nothing, and stranded the cue at
   pending-players. The cue service's lock covered bookkeeping, not keystrokes.
   `ActuationGate` now serializes whole input sequences process-wide (cue, stand-in,
   advance CONTINUE), with a regression test that reproduces the race.
2. **D-034's owed assumption is now measured.** A full advance episode ran live: score
   screen → players-first grace → one CONTINUE → **dismissed to the Music Library** →
   menu settled → next setlist entry cued → stand-in confirms → playing → cursor moved
   only after the verified load. The advance loop's own status sentences narrated every
   step.
3. **Duplicate titles resolve to whatever YARG ranks first.** The library holds two
   "Ace of Spades"; the cue requested one, YARG loaded the other, and the cue failed
   naming exactly what loaded — D-017's hazard demonstrated end to end, and the reason
   verify-by-outcome is load-bearing. The instrument chips and charter display are the
   operator's disambiguation tools.

Also proven live in the same session: the D-035 requester-addressed email loop (gmail
delivery via SMTP2GO, operator notification, hourly ceiling), the index-targeted
setlist remove (used to restore the setlist after the test), and the score screen's
"no input device assigned" banner NOT blocking a synthetic ready confirm.

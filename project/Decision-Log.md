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

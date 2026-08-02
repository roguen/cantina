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

## D-010 · Scope live YARG state to what the datagram actually carries

- Date: 2026-08-01
- Status: Accepted

Context: The kickoff brief promised current song, playback position, and score screen on
the iPad. YALCY's LGPL parser (`YALCY/Udp/UdpIntake.cs` and `UdpIntake.Enums.cs` on
`master`) shows YARG's datagram is a fixed-layout state snapshot of 49 bytes plus two
bytes per player, headed by magic `0x59415247`. It carries scene, pause, venue size,
BPM, a three-value song section, instrument note bitmasks, vocal and harmony pitches,
lighting and camera fields, and per-player star power. It carries no song identity, no
playback position, and no score value.

Decision: Live state promises only fields the datagram carries. Score-screen detection
uses the `CurrentScene` byte, not the lighting cue. Song identity is known only for
songs Barkeep itself cued; a song chosen at the theater PC is reported as unknown and
never guessed. No playback progress indicator is derived from BPM and beat pulses.
Because the missing fields are exactly what capability 4 requires, the upstream YARG
interface leaves "Beyond" and becomes in-scope work for M4 and M5.

Rejected: Dead-reckoning position from BPM and beat pulses, because there is no song
length, no seek signal, and no reconciliation, so the indicator would drift and
misreport with no way for the iPad to detect that it had. Inferring the score screen
from the lighting cue, because the scene byte states it directly and the UDP and DMX
cue enumerations use different values. Leaving the upstream interface unscheduled in
"Beyond," because M4 and M5 cannot meet the brief's stated capability without it.

Consequences: `docs/yarg-interface.md` records the wire contract as provisional evidence
read from YALCY, pending target-PC capture in issue
[#2](https://github.com/roguen/cantina/issues/2). Issue
[#12](https://github.com/roguen/cantina/issues/12) gains a "present but unpopulated"
category, because the DMX wiki lists sing-alongs, spotlights, and camera cuts as not yet
implemented while their datagram bytes still exist. Roadmap M4 and M5 carry the
upstream-hook work.

## D-011 · Publish the repository

- Date: 2026-08-01
- Status: Accepted, supersedes D-006

Context: D-006 kept Cantina private during bootstrap and accepted the resulting loss of
the wiki and of branch protection. The owner made the repository public on 2026-08-01.
Public visibility was always the intent, because upstream contribution to YARG is a
stated goal.

Decision: Cantina is public. This supersedes D-006, which stays recorded.

Consequences: Issue [#10](https://github.com/roguen/cantina/issues/10)'s release gate is
satisfied. The wiki is available, so issue
[#13](https://github.com/roguen/cantina/issues/13) can migrate the living pages out of
the `project/` fallback. Branch protection is available on the current plan, so issue
[#14](https://github.com/roguen/cantina/issues/14) can close with GitHub-enforced rules
instead of a bypassable client-side hook. History, Actions output, and issues are now
publicly readable; nothing may be committed on an assumption of privacy.

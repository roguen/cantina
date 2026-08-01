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

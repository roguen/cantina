# Geomitron Bridge integration

Status: **contract accepted; target-PC proof and external automation remain open in
issue [#17](https://github.com/roguen/cantina/issues/17).**

## About the upstream project

[Geomitron Bridge](https://github.com/Geomitron/Bridge) is an independent open-source
project, licensed GPL-3.0, that searches for and downloads rhythm-game charts. It is not
written, maintained, distributed, or modified by Cantina. The operator installs and
updates it themselves.

Per D-009 it is always named in full. Cantina code uses `GeomitronBridge` as its
identifier stem and `geomitronBridge` in configuration keys, never bare `Bridge`, which
in this project would collide with Barkeep's former description as "the bridge process."
Bare `Bridge` appears below only inside upstream URLs and release titles.

## Outcome

Cantina will accept a song acquired with Geomitron Bridge, add it to Barkeep's
authoritative index, make stock YARG rescan its library, resolve the exact imported
song, and fulfill a play-next intent. When YARG is idle the song can be cued
immediately; an active song is never interrupted implicitly.

## Verified upstream boundary

The project brief targets Geomitron Bridge 3.4.0. The latest published release inspected
on 2026-08-01 is 3.4.5. Both are Electron GUI applications with the same relevant
integration limit:

- only the internal renderer can submit downloads through Electron IPC;
- command-line arguments do not submit work, and a second instance only focuses the
  existing window;
- there is no documented local HTTP/WebSocket API, named pipe, CLI, custom URL scheme,
  or reusable SDK;
- a download is staged in its private data directory, then moved to its configured
  library as a chart folder or `.sng` file;
- it neither requests a YARG rescan nor changes a YARG play queue.

The evidence is Geomitron Bridge's
[3.4.5 main process](https://github.com/Geomitron/Bridge/blob/v3.4.5/src-electron/main.ts),
[preload IPC surface](https://github.com/Geomitron/Bridge/blob/v3.4.5/src-electron/preload.ts),
[settings model](https://github.com/Geomitron/Bridge/blob/v3.4.5/src-shared/Settings.ts),
and [download finalization](https://github.com/Geomitron/Bridge/blob/v3.4.5/src-electron/ipc/download/ChartDownload.ts).
Upstream requests for
[YARG integration](https://github.com/Geomitron/Bridge/issues/96),
[deep linking](https://github.com/Geomitron/Bridge/issues/97), and a
[reusable SDK](https://github.com/Geomitron/Bridge/issues/98) remain open.

## Phase 1: verified filesystem handoff

1. The operator installs Geomitron Bridge from its official release and uses its UI to
   select a dedicated subdirectory of a YARG song source.
2. Geomitron Bridge is configured to retain `.sng` files. Folder extraction is not a
   supported Cantina path until its archive and cross-volume behavior passes issue #17.
3. Barkeep is independently configured with the same canonical directory. It does not
   read or modify Geomitron Bridge's private `settings.json`, `library.db`, or temp
   directory.
4. Geomitron Bridge owns search and the act of starting or canceling a download. Barkeep
   observes only the final library directory.
5. Barkeep treats watcher events as hints, waits for a stable readable file, validates
   containment and content within limits, fingerprints it, and indexes it exactly once.
6. From a proven safe YARG state, Barkeep invokes the replaceable YARG controller to
   run **Scan Songs**. It waits for bounded completion and resolves the exact indexed
   song before mutating the setlist.
7. Barkeep fulfills the authenticated play-next intent. It cues immediately only if
   fresh session state proves YARG is idle.

Geomitron Bridge writes to its final configured directory; Barkeep does not move the
file away and defeat its path-based duplicate check. Startup and periodic reconciliation
cover missed watcher events. A partially written, locked, malformed, duplicate, or
out-of-root item produces a visible skip/failure outcome and cannot trigger YARG.

## Future direct acquisition

The desired iPad flow is search, choose, acquire, refresh, and play next without using
the theater PC keyboard. Enabling the first two steps requires one of these reviewed
contracts:

1. a versioned Geomitron Bridge CLI, deep link, or authenticated loopback API accepted
   upstream;
2. an independently documented chart-provider integration with explicit permission,
   versioning, rate limits, privacy, attribution, and content-use rules.

Both sit behind replaceable chart-catalog and chart-acquirer interfaces. A submission
contains a provider-owned chart reference, idempotency key, and play intent. A result
contains a job identifier, bounded state/progress, final provider identity, and a
validated local song identity. No interface accepts arbitrary executable commands,
URLs, or filesystem paths.

Private Electron IPC injection, remote-debugging attachment, screen scraping, window
automation, settings/database coupling, and undocumented provider download URLs are not
acceptable adapters.

## Licensing and distribution

Geomitron Bridge declares GPL-3.0 and remains a separate operator-installed program.
Cantina is LGPL-3.0-or-later and does not vendor, link, modify, repackage, or silently
update it. Its source may inform interoperability research but is not copied into this
repository. Its license does not grant rights to redistribute downloaded music, video,
art, or charts; Cantina keeps song content local and never serves it to the iPad.

## Proof gate

Issue #17 closes only with evidence from the Windows 10 theater PC for Geomitron Bridge
3.4.0 or the explicitly adopted release, both `.sng` and rejected/approved folder
behavior, slow and failed writes, duplicate and restart recovery, stock-YARG scanning,
exact-song resolution, idle and active-session queue behavior, and measured end-to-end
latency.

The deterministic harness in [`test-harness.md`](test-harness.md) exercises these
application-level policies with symbolic inputs. It does not satisfy this target-PC
proof gate or claim that a synthetic payload is a valid `.sng` file.

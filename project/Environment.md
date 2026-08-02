# Environment

## Authoritative deployment target

These target facts come from the project brief. A change requires an explicit decision
instead of silently updating this table.

| Item | Target |
|---|---|
| Theater PC | Intel i3-9100F at 3.6 GHz, 16 GB RAM, AMD RX 6800 16 GB, 500 GB SSD |
| OS | Windows 10 Pro 22H2 x64; no Windows 11 upgrade; Consumer ESU applies |
| Game | YARG 0.15 stable through YARC Launcher; Geomitron Bridge 3.4.0 for song sourcing |
| Input | Xbox controllers through an Xbox 360 wireless adapter |
| Display | PC → AV receiver → projector |
| Remote | iPad mini 6th generation, MK7P3, iPadOS 26.5.2, 64 GB with 19 GB free |
| Intended development host | The same Windows theater PC; no Mac-dependent workflow |
| Shared with | Holocron on the same PC, projector, receiver, and audio endpoint |

## Bootstrap environment observed on 2026-08-01

The initial Codex workspace ran on macOS despite the brief naming the Windows theater
PC as the development host. This is recorded as an environment discrepancy, not a
change to the target.

| Item | Observed bootstrap value |
|---|---|
| Host | macOS 26.5.2 on arm64 hardware |
| .NET | SDK 10.0.301 and runtime 10.0.9 under osx-x64 |
| Node.js | 24.13.0 |
| npm | 11.6.2 |

Local macOS checks and GitHub-hosted Windows builds are portability evidence only.
Issue [#9](https://github.com/roguen/cantina/issues/9) remains the authority for the
actual Windows 10 artifact smoke test.

## Windows working host adopted on 2026-08-01

Work moved onto the Windows host described by the target table above. Observed directly:

| Item | Observed value |
|---|---|
| OS | Windows 10 Pro 22H2, build 10.0.19045 |
| Volume | 465 GB total, consistent with the target's 500 GB SSD |
| .NET | SDK 10.0.302, satisfying `global.json` 10.0.300 with `latestPatch` |
| Node.js | 24.18.1 |
| npm | 11.16.0 |

The .NET SDK and Node.js were installed during this session; the host previously carried
only the .NET 8 runtime. This makes the repository buildable and testable locally for
the first time.

**Not yet present on this host:** YARG, YARC Launcher, and Geomitron Bridge were not
found in any standard install location. Until they are installed, this host can run the
build, the tests, and the deterministic harness, but it cannot run any M1 spike, and it
is not yet evidence for issues #2, #3, #4, #9, #11, or #17.

## Geomitron Bridge reference observed on 2026-08-01

The brief's Geomitron Bridge 3.4.0 remains the target fact until the theater PC is
inspected. The latest published upstream release observed during design was 3.4.5,
released on 2025-04-13. Upstream `master` was newer but still declared 3.4.5, so it is
treated as unreleased source rather than an installed contract. Issue
[#17](https://github.com/roguen/cantina/issues/17) must record the actual installed
version and pin the tested behavior; Cantina never silently installs or updates it.

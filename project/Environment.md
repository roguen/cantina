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

## Bootstrap environment

The repository was bootstrapped on a non-target host before work moved to the theater PC.
Nothing produced there closes a target claim: portable local checks and GitHub-hosted
builds are portability evidence only. Issue
[#9](https://github.com/roguen/cantina/issues/9) remains the authority for the actual
Windows 10 artifact smoke test.

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

**Correction recorded 2026-08-14.** This section previously stated that YARG, YARC
Launcher, and Geomitron Bridge "were not found in any standard install location," and that
this host therefore could not run any M1 spike. That was wrong when written: a filtered
directory listing rendered blank rows and was misread as no match. All three were already
installed. Session 005 corrected the YARG and YARC Launcher half in the Time Log but never
updated this page, and the Geomitron Bridge third was not corrected anywhere until now.

| Item | Observed on this host |
|---|---|
| YARG | 0.15 stable under `C:\YARC\YARG Installs\`, installed by YARC Launcher |
| YARG settings channel | `%USERPROFILE%\AppData\LocalLow\YARC\YARG\release`, carrying `currentSong.json` |
| YARC Launcher | 1.3.0, `C:\Program Files\YARC Launcher\` |
| Geomitron Bridge | 3.4.5, `C:\Program Files\Bridge`, reporting `ProductName` `Bridge` and `CompanyName` `Geo` |

This host runs the build, the tests, the deterministic harness, **and** the M1 spikes.
Every capture under `spikes/captures/` was taken here, and this host is the source of the
evidence behind issues #2 (D-010, D-012), #3 (D-014), #4 (D-015), and #11 (D-013).

Evidence existing is not the same as an issue closing. Only #2 is closed; the Roadmap still
carries the cases no capture has covered. Nothing produced here yet bears on issue
[#9](https://github.com/roguen/cantina/issues/9) or
[#17](https://github.com/roguen/cantina/issues/17).

The installed Geomitron Bridge is **3.4.5**, not the brief's 3.4.0. That difference is
issue [#17](https://github.com/roguen/cantina/issues/17)'s to reconcile and pin, so the
target table above still states the brief's figure rather than being silently updated.

## Geomitron Bridge reference observed on 2026-08-01

The brief's Geomitron Bridge 3.4.0 remains the target fact. The latest published upstream
release observed during design was 3.4.5, released on 2025-04-13. Upstream `master` was
newer but still declared 3.4.5, so it is treated as unreleased source rather than an
installed contract. Issue [#17](https://github.com/roguen/cantina/issues/17) must record
the actual installed version and pin the tested behavior; Cantina never silently installs
or updates it.

**The theater PC has since been inspected**, which this section was waiting on. The
installed build reports 3.4.5, matching the published release rather than the brief. It
has not been exercised by Cantina, so the version is observed rather than tested, and #17
still owns pinning its behavior.

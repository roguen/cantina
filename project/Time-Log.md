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

# Cantina — working context

Cantina is an iPad web remote for stock YARG. Read `README.md`,
`docs/architecture.md`, and `project/Decision-Log.md` before changing architecture.

## Current state

M0 foundations are being established. No YARG integration is proven. Do not describe
the UDP stream, input injection, song selection, or target deployment as complete until
the corresponding spike is captured on the theater PC.

## Target facts

- Deployment target: Windows 10 Pro 22H2 x64 on the theater PC.
- Game target: YARG 0.15 stable, installed through YARC Launcher.
- Remote: iPad mini 6th generation on iPadOS 26.
- Holocron shares the PC, projector, receiver, and audio endpoint, but is out of scope.
- This repository was initially bootstrapped from a non-target Codex host. Portable builds
  here and hosted CI are not evidence that Windows-session integration works.

## Standing rules

1. `main` stays stable. The empty-repository bootstrap is the only direct-main
   exception; all subsequent work uses a focused branch and pull request. Never push
   `main` directly. Merge only after the stable `Regression gate` check succeeds, then
   verify the post-merge `main` run.
2. Specifications in `docs/` change with the code. Living decisions, roadmap state,
   environment notes, and time accounting belong in the wiki. They temporarily live
   in `project/` until the private-repository hosting limitation in issue #13 closes.
3. Decision Log and Time Log entries are append-only. A reversal supersedes an older
   decision; it never rewrites it.
4. Open a GitHub issue when a bug, enhancement, or unresolved argument is identified.
   Close it through the implementing commit or pull request.
5. Do not copy Photonics code into this LGPL repository. It is GPL corroborating
   evidence. Prefer YARG's producer and YALCY's LGPL parser as implementation sources.
6. Geomitron Bridge is a separately installed GPL application. Do not copy or embed
   its source, depend on its private Electron IPC/settings/database, automate its UI,
   or treat its undocumented provider URLs as a Cantina API. Treat every downloaded
   chart as untrusted input.
7. Never commit song content, credentials, private certificates, or unreviewed packet
   captures.
8. Test harnesses use semantic fakes and symbolic identities. They do not invent YARG
   packets or input behavior, expose production test endpoints, read real song folders,
   or call Geomitron Bridge/private providers.

## Vocabulary

- **Setlist**: ordered songs plus the current cursor.
- **Barkeep**: the bridge process.
- **YARG session**: the running game. Do not introduce `Stage` as a canonical type.

## Architecture boundaries

- The iPad is a thin client. Barkeep owns live setlist state and idempotent commands.
  Durability across process and PC restarts remains unresolved until issue #7 closes.
- YARG control sits behind one interface so input synthesis can be replaced by an
  upstream API without touching higher layers.
- The YARG data-stream parser should become a dependency-light, separately testable
  project once the M1 capture fixes the wire contract.
- Song acquisition stays behind replaceable catalog/acquirer boundaries. The initial
  Geomitron Bridge path is a verified filesystem handoff, not programmatic automation.
- A Windows service is not assumed. Interactive desktop input may require a logged-in
  user process; settle that with evidence before choosing deployment packaging.

## Validation

Before handing off a change, run the complete regression sequence in
`docs/development.md`, including the deterministic theater harness. Windows-specific
claims require a recorded test on the theater PC; GitHub's Windows runner is not a
substitute.

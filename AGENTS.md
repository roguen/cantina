# Cantina — working context

Cantina is an iPad web remote for stock YARG. Read `README.md`,
`docs/architecture.md`, and `project/Decision-Log.md` before changing architecture.

## Current state

M0 foundations are established and the repository is public. The M1 spikes have run on the
theater PC, so `docs/yarg-interface.md` is **confirmed by capture**, not provisional: the
layout parses over 80,000 real datagrams with zero rejections (D-010).

Proven so far: the wire contract and the `Menu → Gameplay → Score` transition (D-010,
D-012); `SendInput` with scan codes reaching stock YARG from a background process (D-014);
the narrowed control scope of choosing a song and verifying by outcome (D-015); song
selection by typed query, which needs a **pointer click** because the search field has no
keyboard focus route (D-017); and that **YARG's setlist does not auto-advance** — it waits
on the score screen, which `CONTINUE` clears in one key (D-018).

Still unproven: listener coexistence with the lighting consumer, Geomitron Bridge
acquisition, and the Windows 10 deployment artifact. Do not describe any of those as
complete until the corresponding spike is captured on the theater PC.

## Target facts

- Deployment target: Windows 10 Pro 22H2 x64 on the theater PC.
- Game target: YARG 0.15 stable, installed through YARC Launcher.
- Remote: iPad mini 6th generation on iPadOS 26.
- Holocron shares the PC, projector, receiver, and audio endpoint, but is out of scope.
- Work now happens on the Windows theater PC itself. The original bootstrap ran on a
  non-target host; anything it claimed about the target is portability evidence only.
- YARG, YARC Launcher, and Geomitron Bridge are **all installed** on this host, and
  target-PC spikes run here. An earlier revision of this line claimed the opposite; it was
  a misread directory listing. Geomitron Bridge reports **3.4.5**, not the brief's 3.4.0 —
  see `project/Environment.md` and issue #17.

## Where the operating knowledge lives

- **`.claude/skills/yarg-observation/`** — how to run an unattended spike against the running
  game: what each oracle can and cannot see, the proven menu sequence, the confounds that
  have actually fired here, and how to reach a verdict. Read it before driving YARG.
- **`../cantina-agent/`** — the agent folder, a sibling directory outside this repository so
  it never appears in a diff. `AGENTS.md` there holds operating rules; `current.md` holds
  live session state and the next concrete action. A new session with an empty context
  window starts by reading `current.md`.

The split is deliberate. Durable, reviewable knowledge belongs in the repository, where
D-016 keeps it under the same pull request as the code. Per-session state that would go
stale within the hour belongs in the agent folder and nowhere else.

## Standing rules

1. `main` stays stable. The empty-repository bootstrap is the only direct-main
   exception; all subsequent work uses a focused branch and pull request. Never push
   `main` directly. Merge only after the stable `Regression gate` check succeeds, then
   verify the post-merge `main` run.
2. Specifications in `docs/` change with the code. Living decisions, roadmap state,
   environment notes, and time accounting live in `project/` permanently (D-016) and change
   through the same branch and pull request as the code they describe. Do not propose
   moving them to a wiki; that was considered and rejected.
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
- **Barkeep**: the Cantina server process. Never call it "the bridge" (D-009).
- **Geomitron Bridge**: the independent GPL-3.0 chart-acquisition app, always named in
  full; code stem `GeomitronBridge`, config stem `geomitronBridge`. Never bare `Bridge`
  except in upstream URLs, file paths, and release titles.
- **Chart acquisition**: the role Geomitron Bridge fills. Use the role word for the
  role and the product name for the product.
- **YARG session**: the running game. Do not introduce `Stage` as a canonical type; it
  collides with YARG's own Stage Kit.

## Architecture boundaries

- The iPad is a thin client. Barkeep owns live setlist state and idempotent commands.
  Durability across process and PC restarts remains unresolved until issue #7 closes.
- YARG control sits behind one interface so input synthesis can be replaced by an
  upstream API without touching higher layers.
- The YARG data-stream parser lives in `src/Cantina.YargSession`, the dependency-light
  project the M1 capture made possible. The spikes and the server reference that one
  copy; a second parser is how D-012's boolean trap happened, so never fork it.
- Song acquisition stays behind replaceable catalog/acquirer boundaries. The initial
  Geomitron Bridge path is a verified filesystem handoff, not programmatic automation.
- A Windows service is not assumed. Interactive desktop input may require a logged-in
  user process; settle that with evidence before choosing deployment packaging.

## Validation

Before handing off a change, run the complete regression sequence in
`docs/development.md`, including the deterministic theater harness. Windows-specific
claims require a recorded test on the theater PC; GitHub's Windows runner is not a
substitute.

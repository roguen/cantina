---
name: cantina-codebase
description: Map of what Cantina actually contains — which project holds which responsibility, which decision each component implements, and the loop for changing any of it. Use when starting work in this repository, when you need to find where a behaviour lives, before adding a project or endpoint, or when a change spans Barkeep, the shared session library, the client, or the harnesses.
---

# What Cantina contains

Standing rules live in [`AGENTS.md`](../../../AGENTS.md); the regression sequence lives in
[`docs/development.md`](../../../docs/development.md); the reasoning behind everything
lives in `project/Decision-Log.md`. This file is the **map**, and it points rather than
restates — a second copy of a rule is a copy that goes stale and gets believed.

## Projects, and what each is for

| Project | Responsibility |
|---|---|
| `src/Cantina.YargSession` | Dependency-light, no I/O: the wire parser, `currentSong.json` reader, and `YargSessionTracker` (latching, freshness, named faults). **Barkeep and the spikes share this one copy — never fork it** (D-012). |
| `src/Cantina.Barkeep` | The server. Hosts the listener, poller, library, journal, cue pipeline, and the HTTP/WS surface. |
| `src/cantina-client` | The iPad web app: live stage, search, cue, setlist. |
| `tests/Cantina.Barkeep.Tests` | xUnit. Deterministic — no sockets, no sleeps, no YARG. |
| `tools/Cantina.TestHarness` | The semantic-fake policy harness (D-008). Runs in CI. |
| `tools/Cantina.SelfTest` | **The target-PC acceptance harness.** Proves what only this machine can prove. |
| `spikes/*` | Historical evidence-gathering tools. Still useful; no longer the product. |

## Where each behaviour lives, and what it implements

| Behaviour | Code | Decision |
|---|---|---|
| Datagram parse | `YargSession/YargDatagram.cs` | D-010, D-012 |
| Song identity from disk | `YargSession/CurrentSongDocument.cs` | D-010 (86 ms clear), D-018 (`HashBytes`, not `Hash`) |
| Latching, freshness, faults | `YargSession/YargSessionTracker.cs` | D-022 |
| UDP listener, `SO_REUSEADDR` | `Barkeep/Yarg/YargUdpListener.cs` | D-013, D-020 |
| Library index and search | `Barkeep/Library/` | D-025 |
| Durable setlist | `Barkeep/Setlist/SetlistJournal.cs` | D-023 |
| Cue gate + actuation + verify | `Barkeep/Yarg/Control/` | D-015, D-017, D-024 |
| Client honesty copy | `cantina-client/src/liveState.ts` | D-022, D-024 |

The normative contracts these implement are `docs/live-state.md`,
`docs/failure-behavior.md`, and `docs/yarg-interface.md`. **When code and contract
disagree, that is a bug in one of them — decide which, and fix that one.**

## Two invariants worth stating plainly

**Observation and actuation are separate, and only observation is trusted.** A sent
keystroke is never evidence of success (D-015). Every command that touches YARG journals
its intent first, acts, and records its outcome only when the wire or `currentSong.json`
confirms it. `pending-players` and `ambiguous` are real states, not failures to model.

**Identity joins on folder path, not hash.** `currentSong.json` states `ActualLocation`;
the index knows every folder; YARG's hash is *learned* from observation and never
computed (D-025). Cue verification matches location first, hash second.

## Changing anything

Branch before the first edit and verify the branch again before committing
(`force-barrier`). Then, before opening a pull request:

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet run --project tools/Cantina.TestHarness --configuration Release --no-build -- run all
```

and in `src/cantina-client`: `npm run lint`, `npm run test`, `npm run build`.

**A new project needs three things or CI fails in ways local builds do not**: an entry in
`Cantina.slnx`, `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` if the publish job will
restore it (otherwise `NU1004` under `--locked-mode --runtime win-x64`), and a committed
`packages.lock.json`.

Anything claiming target-PC behaviour also needs the acceptance run:

```bash
dotnet run --project tools/Cantina.SelfTest --configuration Release -- run all
```

## The acceptance harness is the point

It exists so nobody has to sit at the theater PC stepping through checks. Suites report
**PASS**, **FAIL**, or **INCONCLUSIVE with a named cause**, exit 0/1/2.

- `run all` — journal (D-023 crash matrix with *real process kills*), live (the session
  library against the real broadcast), readiness (the D-024 signals, read-only).
- `run cue` — the whole loop: stage, cue, stand in for the players, verify by outcome,
  pause what it started. **Sends input**, so it is deliberately excluded from `run all`.
- `run confirmdiag` — reproduces the confirm loop in isolation against whatever is
  running. This is what cornered the load-screen latch defect.

**Extend it rather than writing a one-off script.** Every suite that exists was added
because a question kept needing a human, and each one has since caught something: the
`cue` suite found a tracker bug on its first real run that no unit test could see.

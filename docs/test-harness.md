# Deterministic theater test harness

Status: **application-policy regression harness; external adapter proof excluded.**

## Purpose

The harness exercises Cantina's import-and-play-next policy without depending on the
theater PC, Geomitron Bridge, stock YARG, song content, real time, or a network. It
composes Barkeep's application coordinator with scripted semantic adapters and compares
the resulting event and call transcript with an exact expected transcript.

This is intentionally an executable rather than a test endpoint in Barkeep. Production
startup cannot enable it accidentally, and the iPad cannot select a scenario.

## Commands

```bash
dotnet run --project tools/Cantina.TestHarness -- list
dotnet run --project tools/Cantina.TestHarness -- run idle-success
dotnet run --project tools/Cantina.TestHarness -- run all
dotnet run --project tools/Cantina.TestHarness -- run all --format json
```

With no arguments the harness runs all scenarios. It exits `0` only when every exact
transcript and invariant passes, `1` for a scenario regression, and `2` for invalid
arguments. Text and JSON output use fixed scenario names, symbolic ticks, and ordered
event numbers; they contain no wall-clock timestamps, random identifiers, raw exception
messages, or absolute paths. The CLI writes literal LF line endings on every operating
system, and the successful full-catalog report is byte-stable across Ubuntu and
Windows.

## Semantic ports

The harness substitutes deterministic implementations for:

- arrival validation and indexing;
- fresh YARG observation and safe-refresh eligibility;
- YARG library refresh, exact-song visibility, and cue requests;
- idempotent Setlist insertion;
- atomic, process-local command leases and terminal-result replay.

The production coordinator knows only these semantic ports. No harness component
launches Geomitron Bridge, reads its files, calls Electron IPC or Encore, parses an
`.sng`, emits a YARG packet, drives a menu, synthesizes input, or inspects a real song
directory.

## Included scenarios

| Scenario | Regression proved |
|---|---|
| `idle-success` | An accepted song is indexed, safely refreshed, matched exactly, inserted once, and cued only after a fresh idle observation. |
| `playing-defers` | An active observation cannot authorize refresh; a subsequent scripted safe observation allows processing to continue. |
| `duplicate-idempotent` | Duplicate watcher/reconciliation delivery produces one index, Setlist insertion, and cue. |
| `concurrent-same-key` | Simultaneous callers sharing one command key produce one owner; the other receives `in-progress` without workflow side effects. |
| `idempotency-conflict` | Reusing a command key with a different arrival candidate fails explicitly before workflow side effects. |
| `arrival-rejected` | An invalid arrival reports a stable failure and performs no YARG or Setlist mutation. |
| `song-not-visible` | A bounded post-refresh lookup failure produces no Setlist insertion or cue. |
| `stale-idle-defers` | An idle value with stale observation time never authorizes refresh or cue; a later fresh observation can safely resume the intent. |
| `refresh-ambiguous` | A lost refresh outcome is terminal and visible; replaying the intent cannot issue a second refresh. |
| `cue-ambiguous` | A lost cue outcome is terminal and visible; replaying the intent cannot issue a second cue. |
| `cancel-before-dispatch` | Caller cancellation after lease acquisition is finalized independently and replays as canceled. |
| `adapter-throws-before-dispatch` | A semantic adapter fault before an external dispatch is finalized as a stable failure and is not re-executed. |
| `refresh-canceled-after-dispatch` | Cancellation while refresh may have taken effect becomes terminal ambiguous and cannot dispatch a second refresh. |
| `cue-throws-after-dispatch` | An exception while cue may have taken effect becomes terminal ambiguous and cannot dispatch a second cue. |

Every scenario also enforces the global ordering rules: YARG is untouched before index
acceptance, Setlist insertion waits for an exact post-refresh match, and duplicate
intent keys sharing the harness journal cannot repeat insertion or cue. Terminal
finalization uses a bounded token independent of caller cancellation. A journal
finalization failure propagates as infrastructure failure and does not release the
lease for blind retry.

## Adding a regression

Add one named scenario to the harness catalog with scripted inputs and a complete
expected transcript. Keep it deterministic: inject semantic outcomes and logical
time; do not sleep, poll the operating system, read environment-specific paths, or
weaken an assertion to accommodate nondeterminism. The xUnit suite enumerates the
catalog so a new scenario automatically becomes a test.

If a regression needs real packets, chart archives, input injection, Geomitron Bridge
behavior, or Windows-session behavior, it belongs in a reviewed fixture or target-PC spike
instead. Never encode a guessed external contract merely to make the harness green.

## Regression gate

The hosted server matrix builds, tests, and runs every harness scenario on Ubuntu and
Windows. The stable `Regression gate` job fails unless both server variants, the
client, repository-policy check, and self-contained Windows artifact all succeed.

Server-side enforcement of that check became available when the repository was published
(D-011) and was turned on the same day, so `Regression gate` is required by GitHub rather
than by a bypassable client-side hook. Every change still follows the mandatory branch,
local regression, pull request, green gate, merge, and post-merge verification sequence in
[`development.md`](development.md).

## Evidence boundary

A pass proves deterministic Cantina application policy and fake-adapter composition on
the executing OS, including atomic ownership among callers sharing one in-memory
journal. It does not prove durable replay after a crash or restart, Geomitron Bridge
completion detection, SNG parsing, YARG UDP, interactive input, iPad behavior, Windows
desktop-session access, or theater hardware. Durable replay is proven by the acceptance
layer below: the production journal of D-023 exists in Barkeep, and its crash matrix
passes on the theater PC with real process kills.

## The target-PC acceptance layer

The deterministic harness above proves logic; it deliberately cannot prove target claims
(D-008). Those belong to `tools/Cantina.SelfTest`, run on the theater PC:

```bash
dotnet run --project tools/Cantina.SelfTest --configuration Release -- run all
```

| Suite | Proves | On failure |
|---|---|---|
| `journal` | D-023's crash matrix with **real process kills**: a child races journal appends and is killed mid-flight, crashes hard after acknowledging, has its snapshot corrupted, and restarts. The invariant is the acknowledgement contract — acknowledged means durable, un-outcomed means ambiguous, corruption is quarantined, recovery replays identically. | `FAIL`, exit 1 |
| `live` | `Cantina.YargSession` against the real broadcast for three seconds: parser, freshness, single sender, zero rejections. | `FAIL`, exit 1 |
| `readiness` | The D-024 signals, read-only. | reported per signal |
| `cue` | The full loop: gate, actuate, verify by outcome — the same service, actuator, tracker, and journal classes Barkeep runs, against the real game. **Sends input**, stages the library from the start menu, stands in for the players at instrument setup, and pauses the song it started. Not part of `run all`; run it deliberately. | `FAIL`, exit 1 |

A suite whose preconditions do not hold — YARG not running, port held by a consumer
without `SO_REUSEADDR` — reports `INCONCLUSIVE` with the named cause and exit 2, never a
guess. The tool sends no input and links no input APIs, so running it never perturbs the
theater.

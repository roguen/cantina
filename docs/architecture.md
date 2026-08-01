# Architecture

This document is normative. It describes the component boundaries Cantina must
preserve while M1 establishes what stock YARG actually makes possible.

## Components

### Client

The client is a React/TypeScript web app designed for an iPad mini in a Home Screen
window. It communicates only with Barkeep. It does not read the song library, listen to
YARG broadcasts, synthesize controller input, or become authoritative for the setlist.

Connection state is always explicit. A disconnected Barkeep must never render as an
empty library, and reconnecting must not replay a command.

### Barkeep

Barkeep is an ASP.NET Core process running in the logged-in Windows session on the
theater PC unless the deployment spike proves another shape works. It owns:

- configured song sources and the searchable index;
- acquisition jobs, completed-library-arrival reconciliation, and pending play intent;
- the live setlist and its cursor; persistence policy remains unresolved in issue #7;
- command identifiers, outcomes, and idempotency;
- decoded YARG state and its freshness;
- the one control interface used by higher layers.

The eventual public API uses HTTPS and WSS. Pairing, origin validation, DNS-rebinding
defense, interface binding, and a narrowly scoped firewall rule must be designed before
LAN deployment. M0's host binds to loopback only.

### YARG integration

The observation and control directions are separate:

```text
YARG UDP broadcast ──> parser ──> normalized session state

setlist command ──> YARG control interface ──> proven stock-YARG adapter
```

YARG 0.15's UDP producer is the wire-format authority. YALCY is the LGPL reference
consumer. Photonics may corroborate behavior but its GPL implementation is not copied.

The control interface must not expose SendInput, virtual-controller, or menu-driving
details above the adapter. A future upstream hook replaces that adapter only.

### Song acquisition

[Geomitron Bridge](https://github.com/Geomitron/Bridge) is an optional, separately
installed GPL desktop application. It is not Barkeep and it does not run inside the
Cantina process. Its supported releases expose no external CLI, API, deep link, or
operating-system IPC contract. Cantina therefore does not call Bridge's private
Electron IPC, inspect its settings or database, automate its window, or copy its code.

The first supported compatibility path is a filesystem handoff:

```text
operator searches/downloads in Geomitron Bridge
    └── completed .sng appears in a dedicated configured YARG song source
          └── Barkeep reconciles, stabilizes, validates, and indexes the arrival
                └── YARG control adapter requests Scan Songs at a safe point
                      └── Barkeep proves the exact song is visible
                            └── setlist play-next intent is fulfilled
```

Bridge's library path is configured through Bridge, while Barkeep is configured with
the same allowlisted directory independently. Barkeep never discovers that path from
Bridge's private files. `.sng` is the baseline handoff format; extracted folder mode
remains unsupported until issue #17 proves its containment and completion behavior on
the theater PC.

A filesystem notification is only a reconciliation hint. Startup and periodic scans
must recover missed events. An arrival remains provisional until size and modification
time remain unchanged across the defined quiet window, a bounded read probe and
validation succeed, a final snapshot is still unchanged, path containment is verified,
and the song parses within resource limits. No setlist or YARG mutation may occur
before the authoritative index accepts the song.

Programmatic iPad search/download remains behind replaceable chart-catalog and
chart-acquirer interfaces. It can be enabled only when Bridge publishes a versioned
external contract or an independent provider is documented and approved. The client
submits provider identifiers and intent, never arbitrary URLs or destination paths.

The manual handoff uses these observable states:

```text
detected -> stabilizing -> validating -> indexed -> refresh-pending
         -> YARG-visible -> queued -> cued
```

Any non-terminal state can end as `failed` or `canceled`. A cancellation, exception, or
crash around an external YARG operation can end as `ambiguous` when its effect cannot
be observed safely. A future direct provider may prepend `requested -> acquiring`.
Every transition has a bounded outcome and an idempotency key.

**Play next** means insert immediately after the active setlist cursor. If fresh YARG
state proves the game is idle, Barkeep may cue it immediately. A delayed acquisition
or refresh never interrupts an active song and never upgrades itself to an implicit
play-now command.

## State ownership

Barkeep is authoritative for live setlist state. This does not yet promise durability
across process, YARG, or PC restarts; issue #7 owns that decision. Each mutating request
eventually carries an idempotency key. Client state is a projection and may be
discarded at any time.

Acquisition progress and pending play intent follow the same rule. Reconciliation may
recover an imported song after restart. The target durable command journal must prevent
duplicate index and setlist mutations; its persistence mechanism remains owned by
issue #7. If Barkeep crashes after an external YARG request and cannot observe whether
it took effect, the outcome is `ambiguous`; Barkeep never blindly repeats a refresh or
cue. An expired play intent is surfaced for confirmation rather than executed late.

Decoded YARG state carries a reception timestamp and becomes `stale` after a defined
timeout. Unknown data is represented as unknown, never inferred from an old packet.

## Testing boundary

The deterministic theater harness composes the real application coordinator with
scripted semantic implementations of the arrival, index, YARG, setlist, and journal
ports. It runs in a separate executable and is never registered with the production
web host. It uses symbolic songs and session states; it does not fabricate YARG wire
packets, keyboard/controller behavior, Bridge APIs, SNG validity, or filesystem proof.

The harness proves application transition order, cancellation and adapter-fault
propagation, process-local atomic command leases and replay, fresh-state cue policy,
and cross-platform deterministic output. It does not prove persistence across a crash
or restart. Target-PC spikes remain the only evidence for external adapters.

## Deliberate non-goals

- controlling Holocron;
- switching the receiver or theater applications;
- driving DMX or Stage Kit hardware;
- shipping a native iOS application;
- requiring a custom YARG build before the stock-YARG spikes finish.

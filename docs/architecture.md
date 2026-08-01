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

## State ownership

Barkeep is authoritative for live setlist state. This does not yet promise durability
across process, YARG, or PC restarts; issue #7 owns that decision. Each mutating request
eventually carries an idempotency key. Client state is a projection and may be
discarded at any time.

Decoded YARG state carries a reception timestamp and becomes `stale` after a defined
timeout. Unknown data is represented as unknown, never inferred from an old packet.

## Deliberate non-goals

- controlling Holocron;
- switching the receiver or theater applications;
- driving DMX or Stage Kit hardware;
- shipping a native iOS application;
- requiring a custom YARG build before the stock-YARG spikes finish.

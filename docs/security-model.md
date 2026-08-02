# Security model

Status: **open design input for M0/M1, not an implemented guarantee.**

Cantina turns any authorized LAN client into a control surface for an interactive game
session. The LAN is not assumed trustworthy merely because it is private.

Before Barkeep binds beyond loopback, the design must resolve:

- first-device pairing and credential rotation;
- HTTPS/WSS certificate issuance and iPad trust onboarding;
- allowed WebSocket and HTTP origins;
- host-header and DNS-rebinding defense;
- explicit network-interface binding and least-scope Windows firewall rules;
- rate limiting and idempotency for control commands;
- redaction of paths, packet contents, and local addresses from logs;
- visible revocation and recovery when the iPad is replaced.

Song acquisition adds an untrusted-content boundary. Before Geomitron Bridge arrivals or
a future remote provider can trigger YARG or setlist changes, the design must also
resolve:

- a dedicated canonical allowlisted song root, with rejection of traversal,
  drive/UNC/rooted names, alternate data streams, case collisions, symlink/reparse
  escape, and overwrite races;
- stable-write detection, bounded retry, free-space checks, per-file and total-size
  limits, parser time/resource limits, quarantine, and explicit skip reasons;
- reconciliation and idempotency across watcher storms, cancellation, Geomitron Bridge
  or Barkeep crashes, antivirus locks, and duplicate arrivals;
- authenticated, origin-checked, rate-limited acquisition, refresh, play-next, and cue
  commands; the iPad never supplies a raw download URL or filesystem destination;
- redaction of search terms, provider identifiers, URLs, local paths, and chart data
  from logs and client error detail;
- provider terms, privacy, attribution, content rights, rate limits, and redirect-host
  policy before Cantina contacts any third-party catalog or file service directly.

Geomitron Bridge must be installed and updated explicitly by the operator. Cantina
neither bundles nor silently updates it, never reuses GitHub credentials for it, and
does not serve downloaded audio or chart archives to LAN clients.

The test harness is a separate local executable with semantic in-memory fakes. It is
never registered as a production endpoint, never relaxes authentication or path
validation, never reads production configuration, and never touches a real song
directory. A harness scenario cannot be selected through the LAN client.

The security decision must keep ordinary theater use simple. That usability constraint
does not justify an unauthenticated control endpoint.

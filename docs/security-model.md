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

The security decision must keep ordinary theater use simple. That usability constraint
does not justify an unauthenticated control endpoint.

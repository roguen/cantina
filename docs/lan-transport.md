# LAN transport, pairing, and trust

Status: **normative.** Implemented in `src/Cantina.Barkeep/Network/` and
`src/Cantina.Barkeep/Access/`, decided in D-026 and amended by **D-029**, proved on the
theater PC by `Cantina.SelfTest run lan`.

**The theater is `cantina.aero4ge.com`.** D-029 supersedes D-026's certificate design: the
certificate normally comes from outside, and the private authority below is the fallback for
a site with no name.

This is what happens when Barkeep stops being loopback-only. `docs/security-model.md`
states the open questions this answers; where the two disagree, this file is the contract
and that one is the older document.

## Threat model

Barkeep turns a LAN client into a control surface for a live game session. The worst
outcome is not data loss — it is somebody who is not in the room changing what the room is
doing, so the design treats **being in the theater** as the credential that matters.

| Assumed | Not assumed |
|---|---|
| The theater PC is single-user and its profile is the operator's | That the LAN is trustworthy |
| Code already running on this host is trusted | That every device on the LAN is the operator's |
| The operator can read a code off the theater screen | That the iPad is the only device that will try |
| Physical access to the theater PC is authority | That DHCP will keep handing out one address |

Out of scope, and deliberately: an attacker with the operator's Windows profile, a
compromised iPad, and anything arriving from outside the LAN. Barkeep is never exposed to
the internet and the design offers no story for it.

## Binding

`Network:Mode` is `Loopback` by default and always will be. Leaving loopback is one
explicit configuration change, and it never happens by inference.

| Setting | Default | What it decides |
|---|---|---|
| `Network:Mode` | `Loopback` | `Loopback` or `Lan` |
| `Network:Address` | empty | The one interface address the LAN listeners bind. Empty selects the interface holding the default IPv4 gateway |
| `Network:Port` | `5273` | Plain HTTP. Loopback always; on the LAN, onboarding and redirect only |
| `Network:SecurePort` | `5274` | TLS. The whole control surface, on the LAN |
| `Network:HostNames` | empty | Extra names for the certificate and the Host allowlist |
| `Network:LeafCertificateDays` | `397` | Server certificate lifetime, clamped below Apple's 398-day ceiling |

**One address, never `IPAddress.Any`.** The theater PC carries a Tailscale interface as
well as its Ethernet one; `Any` would publish the control surface to a network the
operator is not standing on. If `Lan` is configured and no routed IPv4 interface resolves,
Barkeep refuses to start rather than falling back to loopback and looking healthy while
being unreachable.

## Discovery

**The name is the route.** `cantina.aero4ge.com`, an A record in Cloudflare pointing at
`192.168.68.54`, DNS-only. Publicly resolvable is not publicly reachable, and the same
pattern already carries `nas.aero4ge.com` and `fios-blanc.aero4ge.com`. Nothing shipped
generates a QR code yet; when one does, it carries the name and is a convenience over
typing, never a credential — the pairing code stays on the theater PC.

**The reservation is load-bearing.** A public name over a DHCP lease breaks silently when
the lease moves, so `192.168.68.54` is pinned to the theater PC's MAC before the record
exists.

mDNS is a convenience Cantina may add and must never depend on: whether iPadOS resolves
this host's `.local` name has not been measured with two devices, and D-026 records why the
single-host attempt was inconclusive.

The bound address must be a **DHCP reservation** on the theater's router. Without one, the
address can change; Barkeep survives that — it re-issues the server certificate as soon as
the certificate stops naming where it answers — but the bookmark and the home-screen icon
go stale, and that is a trip to the theater to fix.

## Certificates

There are two configurations, and which one is running is visible in `/api/health` and in
the shape of the onboarding page.

### Supplied, and publicly trusted — the normal case (D-029)

`Network:CertificatePath` names the certificate. Two formats are accepted, because an ACME
client writes one of them and Windows tooling tends to want the other:

| Setting | Meaning |
|---|---|
| `CertificatePath` alone | A PKCS#12 file holding the certificate, its key, and any intermediates. `CertificatePassword` if it has one |
| `CertificatePath` + `CertificateKeyPath` | **PEM**, the shape acme.sh writes. Point the first at `fullchain.cer` and the second at the `.key`. No conversion, no password to keep |

**Point the PEM at the full chain, not the leaf.** Everything after the first certificate in
the file is sent to the client alongside the leaf. A leaf-only file is legal and quietly
worse: it works on any machine that already holds the intermediate and fails on every other,
which is an expensive way to find out.

When a certificate is supplied:

- Barkeep serves it and **creates no theater authority at all** — not even as a spare,
  because an unused private key on disk is one nobody is watching.
- There is **nothing for the iPad to install**. `/onboarding` reduces to one sentence and a
  link, and `/cantina-theater-ca.cer` answers `404` because no authority exists to hand out.
- **A configured certificate that will not load is fatal.** Barkeep refuses to start rather
  than falling back, because a server quietly serving a certificate the operator did not
  configure fails its clients in a way nobody can diagnose.

**Barkeep runs no ACME client**, deliberately. Issuance and renewal belong to the machinery
the site already runs for its other services; doing it here would put a second copy of a DNS
credential on the theater PC. Barkeep loads a file and serves it, and that is the whole
contribution.

### The private theater authority — the fallback

Used when `Network:CertificatePath` is empty: a site with no domain name, no internet, or no
wish to run ACME. Two certificates, not one.

- **`Cantina Theater CA`** — a private authority for this theater. Ten years, generated
  once, and the thing the iPad is asked to trust.
- **The server certificate** — signed by that authority, at most 397 days, carrying an
  extended key usage of `serverAuth` and subject alternative names for every host name and
  every address the binding actually uses.

The authority exists so that rotation never touches the iPad. The server certificate is
re-issued automatically when it is missing, within thirty days of expiry, or no longer
names the address Barkeep answers on. The authority is re-created only if it is missing or
itself near expiry, and a new authority means every device re-trusts and re-pairs — so it
is treated as an event, not a routine.

Both private keys are files in Barkeep's data directory, protected by the operator's
profile ACL and nothing more. **State that plainly:** anyone who can read that directory
can impersonate the theater to the iPad. It is the same protection the setlist journal
already has on this host, and no stronger.

### iPad trust onboarding — fallback configuration only

None of this applies with a supplied certificate. It is what the private authority costs.

1. Open `http://<address>:5273/onboarding` on the iPad. This page is served without
   encryption on purpose — it exists to deliver the certificate that makes encryption
   possible, and it grants nothing.
2. Download `cantina-theater-ca.cer`. It is served as `application/x-x509-ca-cert`, which
   is the content type iPadOS offers to install as a profile.
3. **Compare the SHA-256 fingerprint** shown on the page against the one Barkeep logged on
   the theater PC before tapping Install. This is the step that makes tampering visible.
4. Settings › General › VPN & Device Management → install the profile.
5. Settings › General › About › Certificate Trust Settings → enable full trust.
6. Open `https://<address>:5274` and Share › **Add to Home Screen** — then open the
   installed app and pair *there*. iPadOS gives a Home Screen web app its own storage,
   separate from Safari's, so a pairing done in the browser does not carry into the app;
   the app's own pairing is the one that persists (and Home Screen apps are exempt from
   Safari's seven-day storage eviction). Learned from the first real iPad, 2026-08-30.

## Pairing

**The pairing window can only be opened from loopback**, which means from the theater PC.
The code appears in Barkeep's console and in the response to that loopback request, and
nowhere else — not on the onboarding page, not in `/api/onboarding`, not to any LAN client.
Standing in the room is the authority.

- The code is eight characters from an alphabet with no `I`, `L`, `O`, `U`, `0`, or `1`,
  because it is read off a screen and typed on a tablet.
- The window expires on a clock, is single-use, and closes permanently after five wrong
  codes. Guessing costs a walk back to the theater PC.
- A successful claim mints a 256-bit token, returned once. The registry stores a SHA-256
  hash and compares in fixed time, so the registry file yields no working credential.
- **Rotation** is revoke-and-pair-again. **Revocation** is `DELETE /api/devices/{id}` and
  takes effect on the next request. **Recovery**, when the iPad is lost or replaced, is the
  same act performed at the theater PC.
- A paired device cannot authorise another device. Only loopback can.

## The request rules

Every request is decided in one middleware, in this order:

1. **Origin.** A request carrying an `Origin` Barkeep does not serve is refused
   `403 origin-not-served`. Browsers attach `Origin` to cross-site requests and to every
   WebSocket upgrade, which is the one place the same-origin policy would not have helped.
2. **Loopback is trusted**, exactly as it has been since M0.
3. **On the LAN, plain HTTP** serves onboarding and redirects everything else to TLS with
   `307` — not `302`, because a redirect that turns a POST into a GET would drop a command
   and report success.
4. **The live socket takes a single-use ticket.** A browser cannot put a header on a
   WebSocket, and the alternatives put a long-lived credential in a URL that gets logged. A
   paired device spends its token on `POST /api/live/ticket` and receives a ticket good for
   one connection and thirty seconds.
5. **The app shell is public; its data is not.** A `GET` or `HEAD` outside `/api` and `/ws`
   is the client bundle, which an unpaired iPad has to load in order to have anywhere to
   type its pairing code. It is markup and script, it carries no theater state, and
   treating it as a secret would make pairing impossible. Any other method, and every
   `/api` and `/ws` path, takes a credential.
6. **Everything else takes a bearer token** from `POST /api/pair`.

**There are no cookies anywhere in Cantina.** That is what makes cross-site request forgery
structurally impossible rather than defended against: a hostile page cannot make a browser
attach a credential Barkeep never issued to it.

**Host filtering is the DNS-rebinding defense.** The allowlist is computed from the
resolved binding — loopback, the LAN address, the machine name and its `.local` form, plus
anything in `Network:HostNames`. A name that resolves to this host but is not on that list
is rejected before any endpoint sees it.

**Refusals are named and say nothing else.** `pairing-required`, `ticket-required`,
`origin-not-served`. No echo of what was presented, no host, no path.

## Serving the client

Barkeep serves the iPad its own app from `wwwroot`, so the theater PC is the only place the
client comes from: no app store, no CDN, and nothing to install but a home-screen shortcut.
The bundle is built by `npm run build` in `src/cantina-client` and copied into the server's
output; the Windows artifact job builds it and fails if the published artifact has no
`index.html`.

**The web root is pinned to the binary's own directory.** ASP.NET Core's default content
root is the *working* directory, so a Barkeep started from a shortcut or a scheduled task
would look for its client somewhere else entirely. That was measured, not assumed: a
published Barkeep launched from the repository root answered 404 for its own front page
before this was fixed.

A path that matches no file and no endpoint returns the app, because a single-page client
owns its own routing — except under `/api` and `/ws`, where a mistyped endpoint must fail
as an endpoint rather than quietly return HTML the caller will try to parse as JSON.

## A renewal is picked up without a restart

**Kestrel reads its certificate once, at startup.** Left alone, that makes the whole
arrangement useless: the issuer renews on its own schedule, delivers a new file, and Barkeep
goes on serving the old certificate until somebody restarts it — which is to say, until the
old one expires and the theater stops working. The renewal runs perfectly and changes
nothing.

So a supplied certificate is held behind a source that can swap it. The TLS handshake reads
whatever is current, a background poll notices the file changing every five minutes, and a
renewal takes effect on the next connection.

Two behaviours are deliberate:

- **A failed reload keeps the certificate that is working.** The file arrives by `scp`, which
  is not atomic, so a poll can catch it half-written. Dropping the live certificate because a
  copy was in flight would turn a renewal into an outage. The failure is logged and the next
  poll retries.
- **The initial load is still fatal**, for the reason above: a server quietly serving
  something other than what was configured fails its clients undiagnosably.

The theater authority is not watched. It reissues its own leaf at startup and has a ten-year
anchor, so there is nothing arriving from outside to notice, and claiming to watch it would
be a claim with no mechanism behind it.

## Certificate expiry is a reported signal

The one way a publicly trusted certificate is *worse* than the private authority: renewal is
done by machinery Barkeep does not own and cannot see. A renewal that quietly stopped looks
like nothing at all until the day it lapses and every device refuses at once.

So `/api/health` reports it, for both configurations:

```json
"certificate": { "source": "supplied", "needsDeviceTrust": false,
                 "notAfter": "...", "daysRemaining": 62, "status": "ok" }
```

`status` is `ok`, `expiring` within `Network:CertificateWarnDays` (default 21), or `expired`.
The client renders a warning for anything but `ok`, and the copy names **whose** problem it
is — the renewal on the NAS for a supplied certificate, a Barkeep restart for the theater
authority, which reissues its own leaf.

There is deliberately no client copy for a certificate that has already lapsed: the handshake
fails, so the client is not running to render anything. The only useful window is before
that, and it is the only one the client has words for.

**This is a signal, not a monitor.** It is visible to whoever looks at the iPad. Nothing
outside Barkeep watches the renewal, and closing that gap is separate work.

## Rate limits

| Policy | Limit | Partitioned by |
|---|---|---|
| `pairing` | 5 per 5 minutes | Remote address |
| `commands` | 60 per minute | Device id, falling back to remote address |

`commands` covers `/api/setlist/commands`, `/api/cue`, `/api/library/rescan`, and
`/api/live/ticket`. Both answer `429` rather than failing open.

## Reconnection

An iPad sleeps constantly, so reconnection is the normal case, not the exception.

The live socket carries **no commands in either direction** — it is a push feed of the
projection in `docs/live-state.md`. Reconnecting therefore cannot replay a command, by
construction rather than by care. The retry risk lives entirely in the HTTP command
channel, where D-023's journal answers it: a repeated `commandId` returns the recorded
outcome with `replayed: true` and applies nothing.

## The firewall

Barkeep needs one inbound rule and prints it at startup:

```
netsh advfirewall firewall add rule name="Cantina Barkeep" dir=in action=allow protocol=TCP localport=5273,5274 profile=private remoteip=<subnet> program="<path to Cantina.Barkeep.exe>" enable=yes
```

Removal:

```
netsh advfirewall firewall delete rule name="Cantina Barkeep"
```

It is scoped to two TCP ports, the private profile, the theater's own subnet, and one
program. **Barkeep prints it and never runs it.** Changing the firewall is the operator's
decision, and D-020's finding that the UDP listener needs no rule does not extend to these
TCP ports.

## Logging

Barkeep logs its binding and nothing about the requests it serves. Cantina writes no
request log, and the framework's own is suppressed by the `Microsoft.AspNetCore: Warning`
level in `appsettings.json` — a line naming a token, a ticket, a song path, or a device's
address outlives the session it describes. The pairing code is the one secret that is logged, because the
console is where the operator reads it, and it is short-lived, single-use, and useless off
this machine.

## What the tests prove, and where

| Claim | Proved by |
|---|---|
| Refusal, redirect, credential, ticket, revocation logic | `tests/Cantina.Barkeep.Tests/AccessEndpointTests.cs` |
| Registry storage, pairing window, ticket lifetime, certificate coverage and rotation | `tests/Cantina.Barkeep.Tests/AccessUnitTests.cs` |
| Real listeners, real TLS handshake, chain to the theater authority, the public app shell, pairing over the wire, socket reconnection, revocation | `tools/Cantina.SelfTest run lan` |
| A delivered renewal taking effect, and a half-written file not causing an outage | `tests/Cantina.Barkeep.Tests/CertificateReloadTests.cs` |

`run lan` and `run latency` take `--barkeep <origin>` to say where Barkeep is listening on
loopback. It defaults to `http://localhost:5273`; a theater configured for a portless URL is
on `http://localhost:80`. The harness assumed 5273 until the port became configuration, at
which point both suites reported `INCONCLUSIVE` against a port nothing was on — a harness
confidently measuring the wrong thing.

The unit tests run against a test server, which has no sockets and no TLS; they prove
decisions, not transport. `run lan` proves transport and reports `INCONCLUSIVE` with a
named cause when Barkeep is loopback-only, which is the default. It sits outside `run all`
for that reason and is run deliberately.

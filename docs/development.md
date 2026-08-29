# Development

## Toolchain

- .NET SDK 10.0.300 or a later 10.0 patch
- Node.js 24
- npm 11

Restore operations use committed lockfiles. Do not update dependencies incidentally in
an unrelated change.

## Main-branch workflow

Every change starts from an issue and a focused branch. Never push `main` directly.
Install the tracked local guard once per clone:

```bash
git config core.hooksPath .githooks
```

Run the complete regression sequence below, push only the development branch, open a
pull request, and wait for `Regression gate` to succeed. Merge through GitHub, switch
back to `main`, fast-forward from `origin/main`, and verify the post-merge `main` run.

Branch protection and rulesets previously returned HTTP 403 because Cantina was private
on a plan without that entitlement. Publication (D-011) removed that barrier and
protection was enabled on 2026-08-01: `Regression gate` required, strict up-to-date
branches, `enforce_admins` on, force pushes and deletions refused. The hook is defense in
depth in this clone, not the control.

## Validate the server

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

The integration test starts the real ASP.NET Core pipeline and calls `/api/health`; an
empty template test does not satisfy CI.

## Run the deterministic theater harness

```bash
dotnet run --project tools/Cantina.TestHarness \
  --configuration Release --no-build -- run all
```

See [`test-harness.md`](test-harness.md) for scenarios, output modes, and the strict
boundary between semantic application simulation and unproven external behavior.

## Validate the client

```bash
npm ci --prefix src/cantina-client
npm test --prefix src/cantina-client
npm run lint --prefix src/cantina-client
npm run build --prefix src/cantina-client
```

The complete local regression result requires the server sequence, theater harness,
and client sequence to pass from one commit. Hosted CI repeats the harness in both
server operating-system jobs, publishes the Windows artifact, and exposes one stable
`Regression gate` summary.

## Target-PC acceptance boundary

The acceptance run for everything below is one command on the theater PC:

```bash
dotnet run --project tools/Cantina.SelfTest --configuration Release -- run all
```

It reports PASS, FAIL, or INCONCLUSIVE with a named cause per suite (exit 0/1/2) and
covers the D-023 crash matrix with real process kills, the session library against the
real broadcast, the D-024 readiness signals, and the D-026 LAN transport. `run cue`
exercises the whole cue loop and is excluded from `run all` because it sends input and
starts a real song. See [`test-harness.md`](test-harness.md).

`run lan` reports INCONCLUSIVE unless Barkeep is running with `Network:Mode=Lan`, which is
not the default. To exercise it:

```bash
Network__Mode=Lan dotnet run --project src/Cantina.Barkeep --configuration Release
```

then `dotnet run --project tools/Cantina.SelfTest --configuration Release -- run lan` in
another shell. See [`lan-transport.md`](lan-transport.md).

Work now happens on the Windows 10 Pro 22H2 host itself, the full regression runs there,
and YARG, YARC Launcher, and Geomitron Bridge are all installed on it. That is necessary
but not sufficient: hosted CI still uses hosted machines, and an installed game is not a
recorded observation. Before M1 or deployment work is accepted, record evidence from the
actual theater PC for:

- launching the self-contained `win-x64` Barkeep artifact;
- listening to YARG while the normal lighting application is running;
- interactive-session input and integrity-level behavior;
- firewall and iPad connectivity;
- sleep, YARG restart, user logoff, and PC reboot recovery;
- contention with Holocron and the HDMI audio endpoint.

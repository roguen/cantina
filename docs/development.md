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

GitHub currently returns HTTP 403 for branch protection and rulesets because Cantina is
private on a plan without that entitlement. The hook is useful defense in this clone,
but is not server enforcement and can be bypassed. Issue
[#14](https://github.com/roguen/cantina/issues/14) remains open until protection is
available without changing repository visibility unexpectedly.

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

The initial workspace is a non-target host and CI uses hosted machines. Neither can close a claim
about the theater PC. Before M1 or deployment work is accepted, record evidence from the
actual Windows 10 Pro 22H2 machine for:

- launching the self-contained `win-x64` Barkeep artifact;
- listening to YARG while the normal lighting application is running;
- interactive-session input and integrity-level behavior;
- firewall and iPad connectivity;
- sleep, YARG restart, user logoff, and PC reboot recovery;
- contention with Holocron and the HDMI audio endpoint.

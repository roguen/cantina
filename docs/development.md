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
on a plan without that entitlement. Publication (D-011) removed that barrier, so
server-side protection can now be enabled. Until it is, the hook is useful defense in
this clone but is not server enforcement and can be bypassed. Issue
[#14](https://github.com/roguen/cantina/issues/14) remains open until protection is
actually turned on.

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

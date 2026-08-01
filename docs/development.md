# Development

## Toolchain

- .NET SDK 10.0.300 or a later 10.0 patch
- Node.js 24
- npm 11

Restore operations use committed lockfiles. Do not update dependencies incidentally in
an unrelated change.

## Validate the server

```bash
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

The integration test starts the real ASP.NET Core pipeline and calls `/api/health`; an
empty template test does not satisfy CI.

## Validate the client

```bash
npm ci --prefix src/cantina-client
npm test --prefix src/cantina-client
npm run lint --prefix src/cantina-client
npm run build --prefix src/cantina-client
```

## Target-PC acceptance boundary

The initial workspace is macOS and CI uses hosted machines. Neither can close a claim
about the theater PC. Before M1 or deployment work is accepted, record evidence from the
actual Windows 10 Pro 22H2 machine for:

- launching the self-contained `win-x64` Barkeep artifact;
- listening to YARG while the normal lighting application is running;
- interactive-session input and integrity-level behavior;
- firewall and iPad connectivity;
- sleep, YARG restart, user logoff, and PC reboot recovery;
- contention with Holocron and the HDMI audio endpoint.

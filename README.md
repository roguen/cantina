# Cantina

Cantina is a LAN remote for [YARG](https://github.com/YARC-Official/YARG). An
iPad-installed web app will browse the theater's song library, build a setlist, cue
songs, and show the live state that stock YARG exposes. **Barkeep** is the Cantina
server process running beside YARG on the theater PC.

[Geomitron Bridge](https://github.com/Geomitron/Bridge) is an independent open-source
chart-acquisition application (GPL-3.0), separately installed by the operator and not
part of Cantina. Cantina will recognize its completed downloads, refresh YARG, and place
the exact imported song next in the setlist. Geomitron Bridge has no supported external
automation interface today, so searching for and starting a download stays inside
Geomitron Bridge until issue
[#17](https://github.com/roguen/cantina/issues/17) proves a supported boundary.

## Status

**M1 · Spike results. Nothing controls YARG yet.** The Barkeep host and client foundation
build and test, and the M1 spikes have run on the theater PC. The data-stream question is
answered: the wire contract is confirmed by capture (D-010, D-012). The control question
has a proven mechanism — `SendInput` with scan codes, from a background process, never
taking foreground (D-014) — with elevation, lock and logoff, and bounded failure still
open. Deterministic song selection remains genuinely unresolved (D-015,
[#4](https://github.com/roguen/cantina/issues/4)).

## Shape

```text
iPad mini · Home Screen web app
          │
          │ HTTPS/WSS over the private LAN
          ▼
Barkeep · ASP.NET Core process on the theater PC
          ├── indexes configured YARG song sources
          ├── validates completed Geomitron Bridge arrivals
          ├── owns the setlist and reports command outcomes
          ├── listens to YARG's UDP data stream
          └── drives stock YARG through one replaceable control interface

Geomitron Bridge · separately installed desktop app
          └── writes completed .sng downloads to a configured YARG song source
```

The client is React and TypeScript. Barkeep targets .NET 10. The Windows-native
boundary stays below the application layer so a future upstream YARG hook can replace
input synthesis without changing the client or setlist logic.

## Vocabulary

| Term | Meaning |
|---|---|
| **Setlist** | The ordered collection of songs plus the cursor identifying the current song. |
| **Barkeep** | The Cantina server application/process on the theater PC. |
| **YARG session** | The running YARG instance Barkeep observes and controls. |
| **Geomitron Bridge** | The independent GPL-3.0 chart-acquisition application the operator installs separately. Never shortened to "Bridge". |

`Stage` is not a canonical code term: it is overloaded by theater staging, deployment
stages, and YARG's own scene and Stage Kit terminology.

`bridge` is not a role word for Barkeep. Barkeep is the Cantina server; writing "the
bridge" reintroduces the collision with Geomitron Bridge that D-009 removed.

## Development

Prerequisites are .NET 10 SDK and Node.js 24.

```bash
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build

npm ci --prefix src/cantina-client
npm test --prefix src/cantina-client
npm run lint --prefix src/cantina-client
npm run build --prefix src/cantina-client

dotnet run --project tools/Cantina.TestHarness --configuration Release --no-build -- run all
```

See [`docs/development.md`](docs/development.md) for the local workflow and target-PC
acceptance boundary, and [`docs/architecture.md`](docs/architecture.md) for the
normative component contract. [`docs/yarg-interface.md`](docs/yarg-interface.md) records
what stock YARG does and does not expose — including the absence of playback position from
every stock surface, and of song identity from the datagram specifically — and is confirmed
by capture on the theater PC.
[`docs/geomitron-bridge-integration.md`](docs/geomitron-bridge-integration.md) defines
the acquisition handoff and its current automation limit, and
[`docs/test-harness.md`](docs/test-harness.md) defines the deterministic theater
simulator and its evidence boundary.

## Project records

- Versioned specifications live in [`docs/`](docs/).
- Living material still lives in [`project/`](project/), a fallback adopted while the
  repository was private and had no wiki. Publication (D-011) removed that limitation;
  issue [#13](https://github.com/roguen/cantina/issues/13) owns the migration.
- Open work and unresolved arguments live in
  [GitHub Issues](https://github.com/roguen/cantina/issues).

## License

Cantina is licensed under the GNU Lesser General Public License v3.0 or later. See
[`LICENSE`](LICENSE).

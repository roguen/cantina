# Cantina

Cantina is a LAN remote for [YARG](https://github.com/YARC-Official/YARG). An
iPad-installed web app will browse the theater's song library, build a setlist, cue
songs, and show the live state that stock YARG exposes. **Barkeep** is the bridge
process running beside YARG on the theater PC.

## Status

**M0 · Foundations. Nothing controls YARG yet.** The repository currently proves that
the Barkeep host and client foundation build and test. The YARG data-stream, control,
and deterministic-selection questions are deliberately unresolved until M1 spikes run
on the theater PC.

## Shape

```text
iPad mini · Home Screen web app
          │
          │ HTTPS/WSS over the private LAN
          ▼
Barkeep · ASP.NET Core process on the theater PC
          ├── indexes configured YARG song sources
          ├── owns the setlist and reports command outcomes
          ├── listens to YARG's UDP data stream
          └── drives stock YARG through one replaceable control interface
```

The client is React and TypeScript. Barkeep targets .NET 10. The Windows-native
boundary stays below the application layer so a future upstream YARG hook can replace
input synthesis without changing the client or setlist logic.

## Vocabulary

| Term | Meaning |
|---|---|
| **Setlist** | The ordered collection of songs plus the cursor identifying the current song. |
| **Barkeep** | The bridge application/process on the theater PC. |
| **YARG session** | The running YARG instance Barkeep observes and controls. |

`Stage` is not a canonical code term: it is overloaded by theater, deployment, and
YARG scene terminology.

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
```

See [`docs/development.md`](docs/development.md) for the local workflow and target-PC
acceptance boundary, and [`docs/architecture.md`](docs/architecture.md) for the
normative component contract.

## Project records

- Versioned specifications live in [`docs/`](docs/).
- Living material temporarily lives in [`project/`](project/) because GitHub does not
  provide a wiki for this private repository on its current plan. Issue
  [#13](https://github.com/roguen/cantina/issues/13) gates migration to the wiki.
- Open work and unresolved arguments live in
  [GitHub Issues](https://github.com/roguen/cantina/issues).

## License

Cantina is licensed under the GNU Lesser General Public License v3.0 or later. See
[`LICENSE`](LICENSE).

# Contributing

Cantina is pre-1.0 and its hardware-facing contracts are still being measured.

1. Start from a GitHub issue that names the expected resolution.
2. Create a focused branch from `main`.
3. Change the relevant specification in `docs/` with the implementation.
4. Add or update tests that prove the behavior.
5. Run the complete local regression sequence in `docs/development.md`.
6. Open a pull request and wait for `Regression gate` before merging.
7. Verify the post-merge `main` run. Never push `main` directly.

Install the repository's local push guard once per clone:

```bash
git config core.hooksPath .githooks
```

GitHub cannot server-enforce branch protection for this private repository on its
current plan; issue [#14](https://github.com/roguen/cantina/issues/14) tracks that
remaining control.

Use clear technical names in code. The project vocabulary is defined in
[`docs/glossary.md`](docs/glossary.md).

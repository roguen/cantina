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

GitHub server-enforces branch protection on `main`: the `Regression gate` check is
required, the branch must be up to date, `enforce_admins` is on, and force pushes and
deletions are refused. The hook above is defense in depth, not the control. An earlier
revision of this file said protection was unavailable on a private repository; the
repository is public (D-011) and protection has been enforced since 2026-08-01.

Use clear technical names in code. The project vocabulary is defined in
[`docs/glossary.md`](docs/glossary.md).

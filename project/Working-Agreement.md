# Working Agreement

## Main and pull requests

- `main` is stable.
- The initial bootstrap commit is the single direct-to-main exception, used to populate an
  empty repository.
- All subsequent changes begin with an issue, use a focused branch, and land through a
  pull request after the stable `Regression gate` check succeeds. Direct pushes to
  `main` are prohibited.
- Branch protection is enforced by GitHub: the `Regression gate` check is required, the
  branch must be current, force pushes and deletions are refused, and administrators are
  included. The repository also supplies a tracked pre-push hook and each clone configures
  `core.hooksPath=.githooks`, but a hook is defense in depth, not a server-side control.
- A deliberate history rewrite is the only operation protection cannot accommodate, because
  it replaces history rather than adding to it. It requires removing protection, force
  pushing with `--no-verify`, and restoring protection immediately afterwards.
- **A rewrite does not remove content from public reach, and must not be relied on to.**
  The 2026-08-28 audit (D-021) verified that GitHub still advertises the pre-rewrite
  commits of the 2026-08-02 rewrite through `refs/pull/15/head` … `refs/pull/28/head`:
  anyone can enumerate and fetch them with plain `git ls-remote` and `git fetch` — no
  known SHA required, which is a weaker position than the "retrievable by hash until
  garbage collection" caveat previously recorded here. Completing a rewrite requires
  asking GitHub Support to delete the superseded pull-request refs and run garbage
  collection. Plan any future rewrite around that, and treat a rewrite alone as
  presentation, not removal.
- After merging through GitHub, verify the complete post-merge `main` run. A failure is
  fixed forward through a new issue, branch, and pull request.

## Where information lives

- Normative contracts live in `docs/` and change with the implementing code.
- Living decisions, roadmap state, environment notes, and time accounting live in
  `project/`, permanently (D-016). They change through the same branch, pull request, and
  `Regression gate` as the code they describe. The wiki was rejected because it has none of
  those, which would leave the append-only rule below resting on discipline alone.
- Open work and unresolved arguments live in one GitHub issue each, with an explicit
  resolution to close against.

## Records

- The Decision Log and Time Log are append-only.
- A reversed decision gets a new entry that supersedes the old entry; history is not
  rewritten.
- A work session gets one factual Time Log entry.

## Evidence

- CI runs on every push and pull request and must prove behavior, not only syntax. The
  `Regression gate` succeeds only when the Linux and Windows server tests and harness,
  client tests/lint/build, repository policy, and Windows artifact all succeed.
- Hosted CI and any non-target development host do not close Windows-session, YARG, iPad,
  or theater-hardware claims.
- Target claims name the tested OS, YARG version, artifact, scenario, and observed
  result.

## Licensing and source boundaries

- Repository code is LGPL-3.0-or-later unless a file says otherwise.
- YARG's producer and YALCY's LGPL consumer may inform implementation.
- Photonics is GPL corroborating evidence. Do not copy its implementation into Cantina.
- Never commit song content, credentials, private certificates, or unreviewed network
  captures.

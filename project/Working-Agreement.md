# Working Agreement

## Main and pull requests

- `main` is stable.
- Commit `8e6c268` is the single direct-to-main exception used to bootstrap an empty
  repository.
- All subsequent changes begin with an issue, use a focused branch, and land through a
  pull request after the stable `Regression gate` check succeeds. Direct pushes to
  `main` are prohibited.
- GitHub cannot enforce branch protection for this private repository on its current
  plan. Issue [#14](https://github.com/roguen/cantina/issues/14) tracks making the rule
  server-enforced. Until then the repository supplies a tracked pre-push hook, each
  clone configures `core.hooksPath=.githooks`, and the rule remains a mandatory project
  convention. A hook is defense in depth, not a server-side control.
- After merging through GitHub, verify the complete post-merge `main` run. A failure is
  fixed forward through a new issue, branch, and pull request.

## Where information lives

- Normative contracts live in `docs/` and change with the implementing code.
- Living decisions, roadmap state, environment notes, and time accounting belong in
  the GitHub wiki. They temporarily live in `project/` under issue
  [#13](https://github.com/roguen/cantina/issues/13).
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

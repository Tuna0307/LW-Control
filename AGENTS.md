# Agent handbook

Read this first. It is the single entry point for any AI (Claude, Codex, …) or person taking over.
Keep it short and current: replace outdated lines instead of appending a log. History belongs in
[docs/HISTORY.md](docs/HISTORY.md) and in git.

## Current state (2026-10-29)

- Latest release is **v1.2**. `main` also includes repository cleanup through 2026-10-28.
  `codex/launch-update-recovery` repairs pending official Lua updates across all launch routes;
  see STATUS.md for validation and review state.
- What works, what has only been tested offline, and what is pending: [docs/STATUS.md](docs/STATUS.md).
  The owner will test Treasure live; Chat claims and the Alliance Drill join still need a live
  opportunity.
- Old history: tag `archive/pre-cleanup-2026-10-27` (old docs/tools/AGENTS log) and tag
  `archive/research-offline-controller` (decompiled original LWBridge evidence). Read them with
  `git show <tag>:<path>`; never merge them back.

## Read next

1. [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how the pieces fit together.
2. [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md): build, test, release, game-update procedure.
3. [docs/LIVE_TESTING.md](docs/LIVE_TESTING.md): **mandatory before touching the real game.**
4. [docs/BEHAVIOR_DIFFERENCES.md](docs/BEHAVIOR_DIFFERENCES.md): deliberate differences from the original.

## Product rules

- The goal is a faithful, reliable, **fully working** replacement for LWBridge 0.3.17
  (owner direction, 2026-10-10). Familiar UI and workflows; differences are allowed if they are
  verified and recorded in BEHAVIOR_DIFFERENCES.md.
- No fake success, placeholder providers or fixture-only "passes". Controlled tests are labelled
  as controlled; only genuine runs against the real game count as live evidence.
- The original login/licensing/commercial account system stays excluded. No credentials, no
  access-control or anti-cheat bypass.
- Don't remove a feature, change its purpose, or bring back an owner-removed one
  (see STATUS.md) without owner direction.

## Working rules

- Work on a feature branch and open a PR to `main`. Don't push to `main` directly. Merge only
  after the checks pass and the change is reviewed (or the owner says so).
- Run the applicable suites before pushing (DEVELOPMENT.md). Some checks need Windows. If you
  can't run them, say so in the PR; don't claim they pass.
- When the work changes what is true, update STATUS.md (and BEHAVIOR_DIFFERENCES.md if relevant).
  Add a dated note to `docs/design-notes/` for a significant design or qualification. Don't
  create per-round status/continuation/receipt files.
- New shipped game scripts go in `src/LWBridge.GameScripts/` **and** in the `Content` lists of
  `LWBridge.Desktop.csproj`. `tools/release/test_package_release.py` fails otherwise.
- Never commit account identifiers in product code, databases, screenshots, raw live receipts or
  third-party binaries. Use the git-ignored `artifacts/`.
- Respect explicit owner pauses and tool/permission denials; don't retry or reroute around them.

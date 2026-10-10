# Home — independent lead progress check, 2026-10-11

**Decision: PARTIAL; whole Home is not fully live verified. PR #6 stays draft.**

Assessed source/documentation checkpoint:
`7d23e79268938277c8a8e427417994be740b9e53`.
Compiled candidate source: `d09b8328ce1752cd6c305acae8f52acf827e6c87`.
Local and direct origin matched; initial working tree was clean.

## Independently executed and inspected

- `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS, five groups;
  all nine locale catalogues have 1,383 keys.
- `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`: PASS,
  actual native routing/CRUD, repair/adoption, recovery/ownership and stored Map
  regressions. Output explicitly records zero game launches.
- `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release`:
  PASS; actual Windows named pipes, rejected authentication, malformed traffic,
  idle retirement, reconnect/RPC, concurrent A/B RPC and shutdown; gameLaunches=0.
- Initial local `check:production-build`: FAIL because ignored `dist` was stale
  (`cc7ce094...` vs current source `fb7344bf...`). Ran the canonical frontend build
  and repeated the check: PASS. No source fix was necessary.
- Independently checked extracted candidate ProductionUi with
  `node src/LWBridge.UI-0.3.17/scripts/check-production-build.mjs artifacts/home-004/functional-d09b8328-extracted/LW-Control-UI/ProductionUi`:
  PASS, source `fb7344bfba80045b7ca97c56ba8f842aeb1eaf11662932266221d17e7db5c595`,
  artifact `3fffd060c66a5211b632e87e2b543b26947de105dff47083db53d13f3f960ef2`.
  Thus stale local dist did not invalidate the retained candidate ZIP.
- ZIP SHA-256 independently matches
  `0F871CDCADACBCF0C3685726608F7AA36D1B96744E42EFC8859801C16EA4FA43`;
  83 entries. SOURCE-COMMIT and extracted EXE ProductVersion match `d09b8328`;
  EXE SHA-256 matches
  `66AEE4C45FCA50156982827801712D341B89ACA3ED5EFEA6D318B48214CEDC39`.
- Inspected current D-06–D-15 production diffs and native/JSX proof boundaries.
  Saved `functional-d09b8328-{en-light,ja-dark}.json` both explicitly use inert
  owners, with genuineGameLaunches=0, mapScanStarts=0, all recorded checks true,
  owners stopped, activeRequests/subscriptions=0 and no cleanup failures.
  Viewed the saved Japanese/dark Connected image; this is controlled UI evidence.
- Read prior genuine hang/recovery timeline and genuine pending-Stop follow-up.
  They identify actual game processes, authentication, stable successor and
  restoration. They are historical witnesses, not new live tests of all later code.
- No LastWar, launcher or Desktop process found at this review's process check.
  One worktree and three necessary local branches; no obsolete checkout removal.
  `git diff --check` passed before documentation edits.

## Interpretation

Basic manual launch/connection/Close, startup, same-build host adoption, exit/hang
recovery and pending-recovery Stop have genuine current-client evidence.
Profile controls, persistence, ownership, adverse outcomes and the latest fixes
also have meaningful native/packaged controlled evidence. This is real progress.

Still outstanding: F-04 responsive-game transport-only recovery, F-06 genuine
replacement-bridge repair/reconnect, and F-07 genuinely simultaneous independent
games. No source-only, pipe-only or fixture screenshot result closes those gates.
Original-only research is optional under the revised owner policy.

This review established no additional demonstrated product defect in its focused
scope. It is not exhaustive source correctness certification or fresh whole-Home
live acceptance. No game experiment, installation mutation, product edit, new ZIP,
main merge or publication was performed. Current status/campaign and PR metadata
were tidied; previous detailed evidence/negatives remain unchanged.

Continue only the [remaining functional task](HOME_004_REMAINING_LIVE_PROMPT.md).

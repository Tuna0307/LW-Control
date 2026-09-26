# LWB-R7-134 — Current acceptance-matrix refresh and browser-port hardening

**Date:** 2026-09-22
**Base revision:** `4e3c77f537e410d8f6d3ed043a3bf5ca7b2adb93`
**Scope:** refresh all 47 acceptance rows using durable R7-089 through R7-133 evidence plus a fresh clean-worktree automated release/browser/visual rerun. This does **not** claim final integrated live acceptance.

## Fresh defect found during acceptance rerun

The first R7-134 browser rerun failed before application assertions:

- `check_map_data_r7131.cjs` used `http.Server.listen(0)`.
- Windows assigned loopback port `10080`.
- Chromium rejected navigation with `net::ERR_UNSAFE_PORT`.

This was a nondeterministic test-harness defect, not a product/UI failure. The same OS-assigned-port pattern existed in the R7-132 browser fixture.

## Repair

The two browser regressions now use dedicated safe loopback defaults with explicit environment overrides:

- R7-131 fixture: `18081`, override `LWBRIDGE_R7131_PORT`
- R7-132 fixture: `18082`, override `LWBRIDGE_R7132_PORT`

Source identities after the repair:

- `tools/check_map_data_r7131.cjs` SHA-256 `3F58DC00C9D0B39E37F04892695C8BB7600E293DD7349BE9C589BC343008F49D`
- `tools/check_map_auto_r7132.cjs` SHA-256 `F1337AFC80750956B4CBDE91E9534A31FF0E789C410B60E65F7CADEBAE45264F`

## Fresh automated acceptance

Clean detached worktree at R7-133 plus only the R7-134 harness repair:

- canonical frontend generator check: **PASS**
- R7-131 Map Data persistence/saved-server/Stop/Clear-race browser regression: **PASS**
- R7-132 navigation/Refresh Status/reconnect Auto regression: **PASS**
- full browser source/parity suite: **36 checks PASS**
- City Excel export removal browser regression: **PASS**
- visual comparator: **32 pairs PASS**
  - strict pairs: 28
  - strict identical: 28/28
  - strict max MAE: 0
  - strict max high-delta rate: 0
  - four intentional Map Data override pairs remain within their existing bounded delta
- all Python tools compile: **PASS**
- Release build: **0 warnings / 0 errors**
- deterministic groups: all six **true**, `failures=[]`
- no Last War or launcher process was active after the deterministic run

Verification environment:

- Node `v24.18.0`
- Playwright `1.63.0`
- Microsoft Edge `153.0.4234.32`
- Release executable SHA-256 `6A82617C8414F3E1DA591C04625EFF4D79C0338651D97C8A38590E675F457AE2`

## Acceptance-matrix refresh

The historical R7-088 matrix already enumerated all 47 rows but became stale after later checkpoints. R7-134 preserves the row definitions and updates only statuses/notes supported by later durable evidence.

The new status totals are:

- `pass_current_offline`: 15
- `pass_current_plus_historical`: 6
- `pass_historical_live`: 5
- `partial`: 10
- `partial_population`: 4
- `partial_offline_action`: 2
- `partial_offline_authorization`: 1
- `blocked_implementation`: 1
- `blocked_implementation_authorization`: 1
- `not_run_authorization`: 1
- `retired_by_owner`: 1

Total: **47 cases**.

## Material stale rows corrected

R7-134 integrates these later checkpoints:

- **A04** → current offline pass via R7-091 Launch-spam/refresh matrix.
- **A05** → current offline pass via R7-092 managed/unmanaged process ownership matrix.
- **A06** → current offline pass via R7-095 close-timing matrix; R7-128 adds live close-during-start rollback.
- **A07** → current offline pass via R7-093 reconnect-policy matrix.
- **A08** → current offline pass via R7-094 fault-admission matrix.
- **A09** → live pass via R7-089 twenty-cycle current-v19 stress proof.
- **A10** → current offline pass via R7-090 startup failure matrix.
- **A11** → current + historical pass by composing R7-091 refresh-during-launch with R7-127 authenticated live `get_status`/pending/getStatus transport and owned Stop.
- **A12** retains live pass; R7-133 adds the real rejected-travel branch and same-session continuation.
- **C05** → current offline pass via the atomic Clear contract plus R7-131 delayed stale-search/Clear browser acceptance.
- **D01/D03** notes are refreshed with R7-131/R7-132 browser evidence.
- **D04** remains partial: disable/Stop/reconnect are now strongly covered, but one explicit app/process restart while a target is actively mid-cycle is still not recorded.
- **F04** remains partial: R7-128 proves the normal Release window and close-during-start behavior, but the final human two-page restart/walkthrough is still impossible through the current connector because it provides no GUI input primitive.

## Deliberately unchanged incomplete areas

R7-134 does not turn missing live evidence into passes:

- Ghost positive rows remain deferred until 2026-09-24.
- Supplies remains population-pending.
- Treasure claim executor remains blocked at the preserved SB-79 boundary.
- State-changing Truck/Dispatch/Alliance/Treasure acceptance still requires explicit authorization and suitable targets.
- D02 still lacks the requested **three** full authorized multi-server Auto cycles as one acceptance set.
- B08 still lacks intentional app/process restart during an active scan.
- D04 still lacks explicit active-target app/process restart.
- F04 human built-executable restart/walkthrough remains open.
- simultaneous real multi-account UI population remains unavailable.

## Artifact

Machine-readable matrix:

`evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-refresh.json`

This artifact supersedes only the **acceptanceCases/status snapshot** inside R7-088. R7-088 remains the durable source for its own release-integrity findings.

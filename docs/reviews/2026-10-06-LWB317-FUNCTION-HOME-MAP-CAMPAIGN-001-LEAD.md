# Home/Map feature campaign interrupted-worker audit

Date: 2026-10-06. Decision: **PARTIAL / CHANGES_REQUIRED** for the feature campaign.
This is not a reversal of the accepted recovered-source/local UIUX phase.

## Audited checkpoint and preservation

Initial local HEAD and direct remote branch both resolved to
`540bc53d73053bb1eb70d6a09fea851a9e96d625`. Useful pushed checkpoints are
`c4509dac` (Home lifecycle/Map action connections), `fbdfae8f` (campaign notes),
and `540bc53d` (Home admission and Map convergence).

The worker also left **29 uncommitted files**, including the Auto service,
native composition/integration runner, canonical UI integration and new checks.
Every file's byte count and SHA-256 was frozen in
`evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/lead-audit-2026-10-06/worker-checkpoint.json`.
The lead did not modify, stage or discard those files. Lead audit changes are
separate documents/evidence. Passing WIP checks do not mean those changes were
committed or the campaign completed.

## Actual progress

| Milestone | Verified useful progress | Still required |
| --- | --- | --- |
| A | Source/command/owner baseline and queue exist. | Reconcile stale queue against this actual checkpoint; preserve original source locators. |
| B/C/D Home | Real lifecycle callbacks, same-host exact root rebinding, strict Start admission, idle/busy pipe retarget and focused lifecycle/recovery tests. | Fresh-host exact-root correction; real profile replacement; failure/status retirement; actual App preference acknowledgements and native Home/document/restart proof. |
| E/F Map data/manual | Canonical eight-kind store/query infrastructure, context refresh, scan state/control checks and durable rows. | Full recovered DTO/server/profile matrix and real injected native-service acquisition/reopen/loss/replacement/disposal proof. |
| G Export/marks/actions | Canonical XLSX checks now include 205 rows, Unicode, nulls, empty output, second-server City isolation and write failure. | Host save-dialog cancellation/failure acknowledgements; stale live Clear authorization contract and native regression. |
| H Auto | Native profile persistence, scheduler core, real controlled scheduler loop and atomic jump/start/return lease composition exist in WIP. | Auto run-scoped cancellation; frontend/native config ordering and deadline authority; integrated provider-positive inert cycle proof. |
| I Jobs/Treasure | Local Dispatch/Truck durability/restart/duplicate/retry/cancel checks; unavailable execution fences remain. | Actual injected native worker/event/profile/shutdown proof; source-backed Treasure state boundary. Claim/status and Ghost preparation remain blocked. |
| J Compatibility | Worker supplied installed-anchor compatibility report with explicit positive-population/action limits. | Reconcile provenance and execute read-only compatibility validation in final packet; do not call historical live proof current. |
| K Desktop | Two saved native Map/config transport packets, seeded isolated Map317 storage and captures exist. | Proper language/theme assertions; Home provider-positive inert routing; real controls, pending/rejection/cancel/restart/A-B-A/routes/events/console/cleanup and source/package identities. |
| L Delivery | Three useful worker commits pushed; lead has rerun important checks. | Correct findings, finish assigned offline gates, refresh documentation/evidence, commit preserved integrated WIP, fresh final checks and remote verification. |

No complete Home/Map feature parity or LIVE_PROVEN result is accepted here.
The unfinished assignment continues through the original A-L scope using medium
checkpoints. It must not restart already verified work or stop after another
readiness-only inventory.

## Independently verified checks

The lead executed the frozen **current working tree**, including WIP:

- Canonical `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`: PASS.
- Desktop Checks compilation with `--no-restore -p:SkipCanonicalProductionUiBuild=true`:
  PASS, zero warnings/errors. This intentionally skips the host's npm-ci/package
  target; it is not a fresh packaged Desktop acceptance claim.
- Explicit non-live runner flags: `--home-campaign-lifecycle-check`,
  `--map-auto-scan-campaign-check`, `--map-campaign-canonical-check`,
  `--game-root-select-check`, `--overview-bridge-host-transport-check`,
  `--overview-bridge-normal-composition-check`: **6/6 PASS**. Individual logs and
  exit codes are in `lead-audit-2026-10-06/runner-results.json`.
- Canonical `tests/LWBridge.Map-0.3.17.Checks` runner: PASS.
- Fresh frontend production build/package check: PASS. Source fingerprint
  `d436e629fc533fdffe4cc198cf37a202eeef9edc7497daed790d5044053be766`;
  package `81cfe101cd4d8325e72fd7957c9769ae20dfbb7395b4c499595a4ac6f744a026`.
- `git diff --check`: PASS.

The lead inspected selected test/provider paths before execution. No normal
game-connected host, live-game flag, updater, game launch/stop/scan/movement or
protected original service was used. Worker desktop captures were inspected
from disk, not regenerated or presented as independently driven interactions.

## Findings to correct

### HA-01 — Fresh host can choose fallback installation A while selected root is B

Same-host picker rebinding is fixed. Fresh `LWBridgeWindow.cs:251,262-267` still
constructs the lifecycle using `GameInstallationService.GetStatus`, whose
`ResolveConfiguredStatus` at `:65-86` can fall through from configured B to strict
detected A. Public native status prefers saved weak-valid B; exact admission at
`:284-297` rejects B. Therefore lifecycle/startup may target A while Home reports
B. Source-confirmed allowed interleaving; no actual launch was attempted.
Correct fresh composition and add an isolated restart case; retain recovered
weak public picker validation and strict exact launch admission.

### MA-01 — Auto's boolean ownership can cancel a replacement Manual run

`MapAutoScanCommandService.cs:475,486-499,671-679` remembers only a boolean;
`Map317CommandService.cs:197-199` stops whichever scan is current. Auto A can finish
and release its native lease, Manual M can begin before the next Auto poll, and
Auto disable/retire/timeout then stops M. The lead **reproduced this against the
actual linked Auto service core** with inert execution delegates:
`lead-audit-2026-10-06/auto-owner-repro-result.json` records `stoppedRuns=[manual-M]`.
The native completion/lease path was independently source-checked; this repro
does not claim execution of the native Map317 service boundary.
Propagate a run identity and atomically stop/status only the owned run; test the
actual injected native service with completion/replacement barriers.

### FA-01 — Older Auto snapshots overwrite newer immediate editable state

`App.jsx:295-319,545-555` applies every full snapshot without edit/snapshot revision.
The lead executed exact current callback bodies and actual config helpers with
controlled promises. Older save response removes a newer server edit; an older
status/event then makes a subsequent edit resubmit the old interval. Both are
reachable within the present single native profile. See
`check-auto-callback-races.mjs` and `auto-callback-races.json`.
The third reproduced case is a late failed-save recovery read after profile
replacement. Its current native same-mount reachability is not established,
because native selection is fixed; it is a required replacement acceptance case,
not a claim of a currently reachable production cross-profile leak.

### FA-02/03 — Native deadline and latest config intent have competing owners

Ordinary browser config edits include stale `nextRunAt`; native
`MapAutoScanCommandService.cs:233-240,262-267` replaces it and may admit an immediate
extra cycle after native finalization at `:619-652`. Separate host Task.Run requests
also acquire the config mutex in execution order, not necessarily UI edit order.
These are source-confirmed permitted interleavings, not executed native cycle
reproductions in this lead pass. Add controlled native tests; preserve ordinary
edits, explicit Run Now, exact defaults/timers and editable-while-saving behavior.
Define clone-internal revision/deadline authority rather than inventing original
async command semantics.

### HA-02/03 — Profile/runtime and failed status ownership remain unfinished

Normal bootstrap/App/backend/store/lifecycle owners remain constructed for one
config profile; registry selection does not replace them. Implement the recovered
real A/B/A owner contract, with retirement, isolated stores and request/event
generations. A preview switch is not that proof.

`App.jsx:402-424` retains old runtime/proxy on failed reads; `mapBackend.js:182-188`
can therefore keep UI connected availability after read rejection. Native providers
still have their own ready-session fence; this finding is stale UI availability,
not a proven native bypass. Prove failure/invalidation and fresh reconnection
without changing source timing. Preserve original local Auto Launch ownership;
native/local fresh-host disagreement needs an explicit tested convergence policy,
not an unreviewed native-only preference redesign.

### MA-02 — Clear reuses retained live context after failed refresh

`RefreshContextAsync` fails before changing state; `ReadScanStatusAsync` catches
that failure; `map_scan_clear` proceeds and Clear checks only retained server/live
identity. Confirm the recovered Clear ownership contract, then distinguish stale
historical browse state from current action authorization and prove unavailable/
foreign-context handling through the native seam. This is a source-backed current
authorization discrepancy requiring contract confirmation; offline history browsing
itself remains intentional.

### KA-01 — Desktop evidence names overstate observed coverage

Both decoded captures labeled `isolated-desktop-en-light` and
`isolated-desktop-ja-dark-narrow` actually show **Chinese (Simplified), light**.
They measure 1120x720 and 900x720 respectively. Native runner passes `language`/
`theme` URL options, while canonical UI reads local storage / preview-only options.
The JSON does not assert rendered language/theme or record console issues. A
preserved English packet `.error.txt` records an earlier timeout; it must remain
historical evidence, with failed/successful attempts distinguished.

The runner sets Home lifecycle null, seeds eight kinds, disables Auto scheduler and
plunder workers, and invokes commands through custom page script. This is useful
actual WebView/native Map/config **transport** evidence. It does not prove mounted
Home controls, native lifecycle, positive Auto execution, normal profiles, all
categories' DTO edges, actual export dialog or full K acceptance. Regenerate a
proper package-pinned native proof after corrections; inspect its actual settings,
screenshots, traces and error capture rather than relying on filenames or `ok=true`.

## Reviewer disposition and continuation

Three bounded independent source reviewers' reports are preserved in
`lead-audit-2026-10-06/home-review.md`, `map-review.md`, and
`frontend-auto-review.md`. The lead checked their actionable paths and independently
executed the negative reproductions above. Old fresh-server-0, Auto jump-before-
manual lease, missing real scheduler-loop and two-row-only workbook findings are
**superseded by real WIP fixes**, so they are not reassigned as defects.

No production changes were made by this audit. Findings are returned in
`docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-001.md`.
Worker lane docs/queue still describe earlier states; update them after concrete
correction checkpoints. Keep saved original/historical records intact. External
updater, positive current Railway/Ghost/Treasure population, Treasure claim/status
and Ghost preparation dependencies remain distinct from finishable offline work.
Final acceptance remains with the project lead.

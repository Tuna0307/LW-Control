# Milestone H — native Auto Scan control-plane core

Campaign: `LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001`
Scope: bounded native/profile-owned Auto Scan scheduler core only
Implementation state: **IMPLEMENTED_NOT_VALIDATED** for production composition; deterministic inert core checks pass
Live Last War/original-service activity: **none**

## Authority and source locators

The implementation preserves the recovered browser/original Auto Scan behavior before moving ownership into the native profile lifetime required by Milestone H.

- `src/LWBridge.UI-0.3.17/src/mapAutoConfig.js`, functions `normalizeAutoScanConfig`, `applyAutoScanConfigEdit`, `autoScanTargetServers`, `advanceAutoScanDeadline`, and `autoScanShouldRun`, is the current canonical browser contract for defaults, clamps, profile storage, enable/disable deadlines, target fallback and due admission.
- `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`, function `runAutoCycle` and its adjacent Auto scheduler effect, is the current canonical browser owner for status admission, ordered server iteration, jump/start, two-second polling, 45-minute timeout, terminal `lastError`, thrown failures, return-to-origin and final deadline advance.
- `src/LWBridge.UI-0.3.17/src/App.jsx`, `initialAutoScanConfig` / `updateAutoScanConfig`, confirms current profile selection owns the browser config and persists it through `lwbridge.mapAutoScan.<profileId>` local storage.
- `docs/reviews/2026-09-30-LWB317-RE-MAP-001-frontend-host-contract.md`, section `Auto Scan contract`, re-establishes the 0.3.17 contract: disabled/60/no explicit servers/default five Auto kinds/fast/return/zero deadline, ordered unique server IDs, five-second outer cadence, two-second status polling, 2,700,000 ms timeout, `server_jump` then `map_scan_start(..., resume:false)` and optional return.
- `docs/reviews/2026-09-24-r8-015-auto-scan-scheduler-parity.md`, sections `Persisted Auto configuration`, `Enable / Run Now / scheduler admission`, and `Cycle execution`, records the earlier byte-derived scheduler semantics. It is historical 0.3.1 evidence and is used here only where the current 0.3.17 recovery/current canonical source agrees.
- `evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/map-lane/findings.md`, Milestone H readiness findings, establishes that production Map ownership is `Map317CommandService`/Map317 and that the browser/localStorage scheduler had no native disable/stop/restart/error owner before this unit.

## Recovered configuration and scheduling contract

`MapAutoScanCommandService` normalizes the same configuration values:

- `enabled=false`;
- `intervalMinutes=60`, clamped to `20..1440`;
- `serverIds=[]`, preserving input order while deduplicating valid integer IDs `1..99999`, maximum 20;
- `selectedTypes=[truck, railway, dispatch, ghost, treasure]`, filtering through the recovered eight-kind allowlist and falling back to those five defaults when nothing valid remains;
- `scanMode=fast`; exact `normal` is retained, every other value normalizes to `fast`;
- `returnToOriginalServer=true`;
- `nextRunAt=0` and nonnegative when normalized.

Disabled -> enabled sets `nextRunAt` to the injected current time. Disabled state sets it to zero. `RunNowAsync` has no independent one-shot field: like the recovered Run Now control it writes the deadline to current time, then attempts ordinary due admission. Offline/manual-scan/busy admission leaves the deadline due for a later scheduler check.

The production scheduler constant is 5,000 ms. Completion polling is 2,000 ms. A started target is bounded by 2,700,000 ms; on timeout the Auto owner stops the scan it started and records `Auto scan timed out after 45 minutes`. A completed live cycle advances `nextRunAt` from the final injected `now` by the normalized interval, including cycles that ended through a thrown target error. A disabled config remains at zero.

## Exact failure distinction

The current page and recovered original assertions make a material distinction that the native core preserves:

1. A poll result with `isReading=false` and non-empty `lastError` is a **completed target with a terminal error**. The error is surfaced/persisted, but the ordered target loop proceeds to the next server while Auto remains enabled.
2. A **thrown** jump, start or status-wait error is not isolated per server. It escapes to the single outer cycle catch, aborts every later target for that cycle, and the normal cycle finalization still applies.

The deterministic checks exercise thrown jump, thrown start and thrown status separately and prove that target N+1 is never attempted after the throw. They separately prove that a terminal `lastError` on the first target still allows all later targets to run.

Return-to-original-server remains best effort. When configured and the original positive server was resolved, a live owner attempts the return in finalization. A return failure does not replace the cycle's real terminal error.

## Native ownership added for Milestone H

The original frontend did **not** have a native scheduler state file, restart marker, independent Auto Stop field or durable last-cycle record. The following behavior is a campaign-required clone-native ownership layer, not a claim about an original hidden host command:

- The service receives a caller-supplied profile state path. It persists normalized config, deadline, last surfaced error, last-cycle result, and an active restart marker atomically at that path. Separate profile paths never share state.
- Admission is serialized. Concurrent Run Now/due checks cannot create overlapping Auto cycles.
- The service records Auto scan ownership only after its injected Start boundary accepts. Disable, explicit cancellation, retirement and disposal call Stop only while that Auto ownership is present. A manual scan observed through the injected `IsMapScanActive` boundary is never stopped by this service.
- Disabling cancels an active Auto cycle and keeps `nextRunAt=0`. Explicit internal cancellation stops the owned cycle but leaves Auto enabled and schedules the next interval from finalization.
- Profile retirement/disposal cancels and stops task-owned work, rejects future admission and does not advance the persisted due deadline. This preserves the prior due time for the replacement profile/service lifetime rather than inventing a new schedule during teardown.
- Before a cycle begins, an active restart marker is persisted. A fresh service that finds a stale marker records the prior cycle as `interrupted`, clears the marker, preserves the existing `nextRunAt`, and therefore leaves an enabled already-due cycle due. `Auto scan interrupted by restart` is clone-internal diagnostic state; it is not presented as recovered original wording.

No manual-scan resume behavior is invented here. The existing Map317 resume producer remains unavailable as recorded in `map-lane/findings.md`; Auto starts use the recovered non-resume path through the integration boundary.

## Integration boundary

`src/LWBridge.Desktop/MapAutoScanCommandService.cs` deliberately does not create a database, Map store, preview provider or alternate execution stack. `MapAutoScanExecutionBoundary` requires the coordinator to bind:

- connection/online truth;
- current manual/Map scan busy truth;
- current scan status (`serverId`, `isReading`, `lastError`);
- server jump;
- scan start using the configured selected types and mode, with the coordinator binding the existing Map317 non-resume start contract;
- scan stop.

The normal production binding must reuse the existing `Map317CommandService`/Map317 command-control primitives. There is no preview/live fallback in this service and no provider is called directly by its deterministic checks.

The canonical UI is not rewired by this worker because `App.jsx`, `MapDataPage.jsx`, `mapBackend.js`, `LWBridgeWindow`, `Program` and shared Map317 files belong to the coordinator. Until that integration is complete, this core remains `IMPLEMENTED_NOT_VALIDATED` at the packaged frontend -> native-command boundary.

## Deterministic proof

`tests/LWBridge.Desktop.Checks/MapAutoScanCommandServiceChecks.cs` is inert and exposes `internal static Task RunAsync()` for the coordinator's explicit non-live check flag. It intentionally has no `[ModuleInitializer]`, so unrelated focused checks cannot start this scheduler suite.

The focused runner covers:

- exact constants/defaults/clamps/order/dedup/type fallback;
- disabled -> enabled, Run Now and disabled deadline semantics;
- durable config and two isolated profile paths;
- terminal `lastError` continues through later targets;
- thrown jump/start/status failure aborts later targets;
- 45-minute timeout stops the Auto-owned scan;
- mid-cycle disable stops Auto-owned work while a manual scan is untouched;
- concurrent Run Now/due admission stays single-cycle;
- explicit cancellation and next-interval scheduling;
- stale restart marker reconciliation without deadline movement;
- retirement stops owned work, rejects new admission and preserves the due deadline.

Focused local execution after removing the module initializer: `MapAutoScanCommandServiceChecks: PASS`. Production source and check-project compilation are validated separately with canonical UI build skipped; no live/test provider branch is invoked.

## Remaining coordinator work / limits

- Bind this service to the normal profile lifetime and Map317 execution primitives in coordinator-owned composition files.
- Expose config/status/Run Now through the canonical native bridge without inventing an original host command name; any new bridge route must be labeled clone-internal composition.
- Move the canonical UI scheduler ownership to native state while preserving accepted UI controls/labels and avoiding duplicate browser/native scheduling.
- Add the coordinator-owned explicit non-live check dispatch for `MapAutoScanCommandServiceChecks.RunAsync()`.
- Production packaged integration remains `IMPLEMENTED_NOT_VALIDATED`; this unit performs no Last War scan, server movement, original-service access or live provider call.

## Recovery coordinator integration closeout — 2026-10-06

The coordinator work listed above is now complete for the authorized offline/inert
campaign scope. The historical worker section remains above to show the state when
this file was first delivered.

- Normal production profile lifetime now owns the native Auto service through
  `ProfileRuntimeOwner` and the same Map317 runtime that owns Manual scan state.
- Canonical native config/status/Run Now routes are wired; production no longer owns a
  second browser scheduler. Preview/local behavior remains deliberately separate.
- `--map-auto-scan-campaign-check` is registered and passes.
- Auto ownership is a durable scan-run token rather than a boolean. Stale Auto A
  cleanup cannot stop a later Manual M, and server movement/start/return admission is
  serialized through the shared Map transition boundary. The final focused suite adds
  a real injected `Map317CommandService` barrier: Auto A completes, Manual M acquires
  the production scan owner, then stale Auto disable retires A without stopping M.
- Native scheduler deadline ownership is authoritative. Ordinary UI edits cannot
  reset a completed cycle's future deadline; persistence publishes only after durable
  write success and exposes failures truthfully.
- The frontend Auto coordinator serializes edits/Run Now, rejects retired profile
  generations, and prevents older acknowledgements/events/hydration from replacing a
  newer editable intent.
- The final v3 package-pinned desktop packets enable the native Auto scheduler and
  inert plunder workers, exercise the actual Auto checkbox through one accepted inert
  start and exact cancel, and still prove A/B/A persisted interval/server isolation.
  Named Auto/Map native events are recorded with profile generation. No Last War
  provider, movement or owner session is used.

No live scan, movement, protected-service call or owner-session action is claimed by
this closeout.

## RECOVERY-002 follow-up — 2026-10-07

The lead's remaining Auto findings are closed for isolated/offline scope. The mounted App queues/merges edits made before initial native hydration over the persisted base, retries after a failed hydration on the next edit, and keeps Run Now command errors independent from runtime status errors. The expanded real-Map317 Auto suite covers positive multiserver completion, exact owned stop/disable, timeout/cancel, retirement, between-target/manual ownership and return-to-origin. Fresh package proof executes these UI ownership cases with an inert run-scoped Map provider. No live server movement or scan is claimed.

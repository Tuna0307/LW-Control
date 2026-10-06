# Home/Map recovery delivery — independent lead review

Date: 2026-10-06. Reviewed clean worker HEAD / direct remote
`99d8f7b513ae56a0aa3b4d92a202f81586a32924`; implementation ends at `eaa73ce5`.
Decision: **CHANGES_REQUIRED**. The Home/Map feature campaign remains **PARTIAL**.

The worker really committed the previously interrupted work and supplied a clean
delivery. Prior root fallback, Auto run-token/deadline/ordering, fresh Clear and
locale/theme corrections are substantive improvements, not missing commits.
Their verified parts stay credited. This review identifies remaining cases;
it does not demand restarting the campaign or expanding to unrelated features.

## Verification and important isolation qualification

Lead execution: canonical frontend `npm run check` PASS; Desktop Checks build
with `--no-restore -p:SkipCanonicalProductionUiBuild=true` PASS, zero warnings/errors.
Seven focused flags returned zero exit codes: profile-runtime owner, Home lifecycle,
Auto Scan, canonical Map, root selection, bridge transport and normal composition.
Raw outputs are preserved in campaign `lead-closeout-2026-10-06/runner-results.json`
and the matching `.txt` logs.

**Those native PASS outputs are not accepted proof of owner isolation.** During
the subsequent source review, the lead confirmed the supposedly isolated owner
teardown still uses the real shared lifecycle runtime directory (HCF-01 below).
The profile-owner check had already executed before this was detected. Further
native/desktop execution was stopped. A post-run read found no shared `lease.txt`;
there was no pre-run lease baseline, so neither actual deletion of an owner's
lease nor unchanged owner runtime files can be asserted. No live-game flag or
normal game-connected host was run. Fix isolation before rerunning those checks.

Read-only production identity validation against the existing Release package
PASS: source fingerprint `4686126fe21d843506b4938371a5532cdf9a12b4c67d32f87b2e67f35833783c`,
artifact `d3f32d1de7108642278f841a274d9bd4dcf3f8ea3a65dec6b758eb45c1d10108`.
Apphost SHA `a349e1698b47dd9f14b5f52d172a48ccf2643226792521461521e0197c932c69`
and managed DLL `4368114d17656362003cbf1eb5ef6fbf14adcd14ada09b39d87ab22e5cb90f9d`
match both worker v3 packets. There are no later production/test changes between
`eaa73ce5` and the reviewed HEAD. The exact reference EXE hash matches AGENTS.
The lead did not rebuild or execute the Release desktop host after the isolation
finding; existing packaged screenshots were inspected from disk.

Both v3 captures now correctly show English/light (1120x720) and Japanese/dark
(900x720). Real rendered DOM controls, native generation-scoped requests/events,
isolated database/config transport, export and document reload are evidenced.
This resolves the predecessor Chinese/light mismatch. It does not close every
active lifecycle/provider boundary or certify pre-reload console history.

## Executed counterexamples

`lead-closeout-2026-10-06/check-counterexamples.mjs` executes exact current App
initializers/callbacks and production Auto coordinator/helpers with controlled
promises. No mounted browser/native command/game provider is used. Source hashes
and results are pinned in `counterexamples.json`. **Three of three defects reproduce:**

1. **FE-01:** a stored native Auto config enabled=true, servers=[321,322], types=[city]
   is still loading. Editing only interval to 45 starts from native-mode defaults.
   The full save and suppressed pending hydration result in enabled=false,
   servers=[], default types, interval=45. The user never edited the other fields.
2. **HCF-03:** older connected status pair deferred; newer pair rejects and UI
   becomes unavailable; older pair resolves and UI incorrectly becomes connected.
3. **FE-02:** Run Now rejects; a later clean runtime snapshot clears that independent
   action failure through the shared error setter.

These counterexamples remain immutable negative evidence. Future corrected checks
must assert the inverse rather than rewrite these results as green parity.

## Findings and independently reviewed disposition

### HCF-01 — shared lifecycle runtime cleanup is unisolated [P1, blocker]

`OverviewLifecycleService.cs:107-109` derives runtime/evidence roots from real
LocalAppData regardless of injected config/profile paths. `Close():403-420`
always calls `StopLeaseTimer(true)`, which deletes `lease.txt` and matching temporary
leases at `:1433-1446` even when this owner never created them. `DeleteFile:1596`
uses actual File.Delete when its hook is absent. Window's campaign owner creation
`:666-694` and `ProfileRuntimeOwnerChecks.CreateOwner:136-171` omit those hooks;
profile swap/shutdown closes those owners. Main lead inspected the full path.
Use explicit isolated runtime/evidence injection and exact ownership for production
cleanup. Preserve a foreign sentinel/lease in isolated tests; never demonstrate
the negative case against the owner's real runtime directory. Audit cancellation,
lease-temporary and evidence paths as well as the one primary lease.

### HCF-02/04 — active runtime and picker request owners can be lost [P1/P2]

Window selection `:521-563` replaces the graph and closes old lifecycle/bridge
`:762-775` without retaining/transferring/refusing a starting/running/stopping owner.
Close cancels monitors and deletes its lease; it is not an exact owned Stop. Current
A/B/A checks and packaged profile fixtures have no owned positive lifecycle instance.
Prove active A transitions using isolated lifecycle/transport seams and recover the
appropriate original owner contract; do not invent silent automatic Stop behavior.

General request dispatch captures `requestBackend`, but `game_root_select:3961`
calls a special method using mutable Window `backend` for async status/save/cancel
at `:4032-4069`. A swap during the await can write another profile's root even when
the eventual UI acknowledgement is dropped. Pin owner/backend/generation for the
entire native picker transaction and prove obsolete writes are refused.

### HCF-03 — paired status acknowledgements lack latest-read fencing [P1]

App `:559-598` checks profile/effect lifetime but not paired-read revision. Polling
is only locally serialized; explicit Refresh/startup reads can overlap it. An older
success sets ready=true and clears the newer error. Main executed the counterexample.
Fence all runtime/proxy/freshness/error commits together by the read owner, preserving
the recovered polling interval and new-profile retirement.

### HCF-05 — accepted global Auto Launch behavior changed [P2, parity regression]

Accepted original/local review
`docs/reviews/2026-10-04-LWB317-REVIEW-HOME-PREFERENCE-LIFETIME-001A.md:32-33`
explicitly retains global Auto Launch across local profile selection, one localStorage
key and absent-storage default true. Native-confirmed rollback is a separate clone
adaptation. Current `initialAutoLaunchGame` prioritizes bootstrap (`App:88-92`), and
profile config polls `:585-587` overwrite global storage/UI with each native profile
value. The worker packet asserts A=false / B=true. This is not the previously
documented native dual launch gate. Preserve the source-known global visible/local
contract; keep owner-scoped persistence acknowledgement and justified native gates
without presenting per-profile takeover as original behavior.

### FE-01/02/03 — initial config, errors and whole-session capture [P1/P2]

Native `initialAutoScanConfig:95-108` returns defaults; status hydration is async,
controls are editable, and `updateAutoScanConfig:480-488` sends the full default-based
config. Coordinator correctly protects pending edits from old snapshots but has no
initial-base merge/admission protocol, so unrelated stored fields are erased.
Maintain immediate editing; obtain a valid persisted base before full writes or
queue/merge exact field intent through hydration. Add actual mounted/native proof.

`applyAutoScanRuntimeSnapshot:444-447` and coordinator action errors share
`autoScanError`, so clean runtime events erase command failure. Keep independent
error channels with source-consistent presentation/clearing semantics.

Window `:868-883` creates an empty issue array on each document; runner reloads at
`:3532` and reads issues only at `:3578`. Zero issues establishes only the final
document, not the full campaign. Accumulate host-owned issue records across reloads,
record warnings/errors/rejections by document generation, and prove an injected
pre-reload issue cannot disappear. Preserve prior captures/packets as history.

### MR-01/02 and remaining native proof [P1/P2, acceptance boundary]

Injected Map317 constructor has `scanProvider=null` and no terminal event seam;
only the concrete production provider's `RunTerminated` releases the process lease.
Injected Start→Stop→Start therefore retains admission, and the distinguishing test
`CompleteInjectedMapScanAndReleaseLease:850-868` calls private release by reflection.
Injected provider lifetime is not stopped/disposed when service.Dispose only calls
`scanProvider?.Dispose():439`. This is an offline acceptance seam defect, not proof
that normal production provider lacks its terminal callback. Add an explicit
run-scoped capture/terminal/lifetime seam used by normal and inert providers.
Prove real native controlled acquisition/publication, terminal/stop/dispose/restart,
manual/Auto replacement and multiple targets without private lifecycle repair.

`Map317ScanProcessLease.TryAcquire:18-28` can open its exclusive stream, fail metadata
initialization/flush and return/throw without disposing the stream. Make acquisition
exception-safe and fault-prove immediate reacquisition/truthful errors.

Finish assigned native boundary coverage: actual due-job workers with instrumented
unavailable/positive inert providers, events/restart/shutdown and zero forbidden arm
calls; all-eight recovered DTO null/default/order/grouping and profile/server matrix;
positive isolated Home Start/Stop routing with real lifecycle/helper ownership.
One seeded row per kind, future scheduled jobs, raw status-provider booleans and
combining unrelated layer tests do not supply those missing cases.

## Closure decision and next task

The lead inspected the actionable paths in three source reports:
`home-review.md`, `map-review.md`, `frontend-evidence-review.md` under the closeout
packet. Their executable-proof limits are preserved. A preliminary hash-algorithm
mismatch was resolved; canonical source/package/native hashes match and are not a
finding. Earlier fixes are credited and external Treasure claim/status, Ghost
preparation, positive live populations/updater/protected runtime limits remain.

Do not mark all Home/Map functionality or the complete clone finished. Recovery-001
is CHANGES_REQUIRED. Continue only
`docs/work-items/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-RECOVERY-002.md`, beginning
with safe isolation before any native rerun. Medium correction checkpoints and
actual inverse counterexamples precede refreshed final acceptance evidence.

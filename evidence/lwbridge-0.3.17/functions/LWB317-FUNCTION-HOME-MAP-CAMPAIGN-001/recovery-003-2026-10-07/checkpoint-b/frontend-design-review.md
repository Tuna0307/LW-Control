# Independent frontend contract/design review — RECOVERY-003 checkpoint B

2026-10-07. Bounded read-only review for R3-04/R3-05. This report and adjacent `dto-source-locators.md` are the only writable files owned by this subreview. Parent coordinator owns implementation, mounted/native checks, integration and commits. No production/tests, historical evidence, Git, browser/game/native/build process was changed or executed by this reviewer.

Status: initial implementation source reviewed after coordinator's code-ready signal. One concrete semantic replay defect was reported and its follow-up correction is source-supported; mounted/current production-handler proof review remains pending. No final project acceptance is implied.

## Original authority and preserved behavior

- Global Auto Launch: `src/LWBridge.UI-0.3.17/src/autoLaunchPreference.js:1–9` and accepted 2026-10-04 preference-lifetime review establish one `lwbridge.autoLaunchGame` local key; absent/anything other than literal `false` defaults true. The checkbox changes immediately and stays editable while native mirror acknowledgement is pending. Profile native gates are clone adaptations, not the visible global preference authority.
- Auto config: original `index-BVfnK1wp.js`, SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`; normalization bytes `[359327,359801)`, parser `[359071,359234)`, append `[359234,359284)`, remove `[359284,359327)`, immediate parent callback `[364217,364377)`, synchronous profile load `[370501,370600)`.
- Original `MapDataPanel-B4GXEND2.js`, SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`; patch merge `[40514,40544)`, Add/Enter callback `[40544,40620)`, Auto card `[46001,48950)` including type last-choice checkbox disabled guard and Run Now admission.
- Ordered integer target IDs1–99999, mixed whitespace/ASCII/full-width comma/semicolon separators, first20 after deduplication; selected types filter to the eight recovered kinds, while normalizing a wholly empty config falls back to the recovered five defaults. That normalization fallback must not replace the last-type operation guard.

These locators are retained in `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/original-source-locators.json`; the earlier frontend audit re-read byte ranges and recomputed hashes. Current native revision/FIFO/profile-generation/deadline and independent errors are authorized clone composition and must remain intact.

## R3-04 — confirmed global rollback authority

One confirmed-global value must be initialized from the recovered global local preference and remain independent of per-profile native mirror/cache. A new owner starts its first edit from the preserved visible/global intent, including an optimistic value whose previous owner retired; this does not take authority from a profile gate or reset UI on selection. Successful acknowledgement may confirm it only within the acknowledged live document/profile owner. Poll/bootstrap/profile selection must never confirm or replace it from a profile gate.

Latest edit determines rendered/local optimistic value. A successful earlier same-owner write must still advance the confirmed-global value even when a later edit is already pending; its success must not overwrite the later optimistic UI. On latest-write rejection, restore the confirmed-global value, not a profile mirror and not merely the value preceding the last optimistic click.

Distinguishing traces:

1. Globalfalse/B-native-true → usertrue → reject: globalfalse remains, nativeBtrue unchanged; inverse values likewise.
2. Initialfalse → edittrue pending → editfalse pending → first succeeds → second rejects: globaltrue is restored. Ignoring the earlier success because its revision is older would incorrectly restore initialfalse.
3. A pending success → B → new A generation → old A response/rejection: neither confirmed global nor rendered/local current intent changes from retired work. Profile-ID equality alone is insufficient.
4. New current-owner write after retired queued A work must execute, while old queued work must not dispatch or clear current errors.

The local key remains immediately written and reloadable. Native mirror/gating may differ during a failure and must not be falsely reported as synchronized; truthful error remains. This review does not invent an original native save command, Retry control or wording.

## R3-05 — semantic array operation rebasing

Retain user operation intent until the acknowledged native base is known: append parsed server IDs, remove one ID, set a known type checked/unchecked. Scalar patches remain absolute field edits. Replay operations in original order against the current acknowledged base, rather than substituting arrays computed from initial defaults.

Required semantics:

- Add `[9,317,10]` to acknowledged `[317,8]` yields `[317,8,9,10]`; existing order wins and input duplicates are ignored.
- Apply dedupe and the20-target limit to the rebased combined list, not only the input draft. Add at cap is a truthful no-op; removing an existing target then adding a new one fills the freed position. Remove/re-add moves that ID to the end.
- An invalid-only Add leaves the input unchanged; a valid parsed Add clears input, even if all IDs duplicate or the cap admits no new entry, matching the recovered callback's valid-parser condition.
- Type checked=true adds only that known kind; false removes only that kind. Repeated checked=true does not duplicate it. Removing the last acknowledged selected type is a no-op; do not allow empty normalization to manufacture the five defaults.
- Unrelated acknowledged choices survive operations before/during hydration and saves. A pending interval edit plus Add/Enter, two Adds, remove/re-add, and type edits must compose without stale acknowledgement replacement.
- Failed hydration/save retains operation intent for a later retry/new edit. Successfully acknowledged operations must not be replayed so that a later unrelated edit resurrects a removed choice; latest native snapshot revision and pending revisions define the base/queue.
- Retirement discards callbacks and undispatched queued operations for that document/profile generation. Native cycle completion retains deadline authority; operations must not write stale browser deadlines.

The original initial local config was synchronously known. Semantic operation transport is a justified async-native adaptation to preserve recovered Add/toggle behavior, not a new workflow. Accepted labels, last-type disabled presentation when source-known, timing, immediate editability, Run Now conditions and Error channels remain.

## Preview and harness compatibility review boundary

Native rebasing and preview local persistence must apply the same operation semantics. Preview callbacks may historically accept only a first argument config/patch; new operation metadata must not silently become an unhandled product object or skip preview persistence. Actual Map handlers, App callbacks and helpers must be exercised together; coordinator-only operation tests do not establish mounted wiring.

Retained exact-source historical checkers/results must remain unchanged. If an old stripped-import/component harness needs an adapter for a new helper/optional callback argument, state that compatibility boundary and execute the actual current production handler in the adapter. A passing static source regex alone does not prove callback wiring or preview reload behavior.

## Evidence and final review requirements

Parent-owned inverse checks should bind actual production handlers to native acknowledgements, defer initial status and saves, and cover the four global traces plus Add/Enter/two-Adds/remove-readd/type-lastchoice/dedupe/cap/hydration-failure/A-B-A. Mounted isolated package evidence must connect these controls to persisted native config, independent errors, source/package identities and complete-session cleanup. No synthetic product success or live account action is required or authorized.

Adjacent DTO report now gives exact original EXE raw/RVA SQL literal locators, with explicit limits around control-flow xrefs/Monster forced direction/original JSON ingestion. Formula bytes and synthetic DTO checks must not be upgraded into runtime/population proof.

## Initial implementation source review

The initial read binds `App.jsx` SHA-256 `F3EE11F67DF6046756977D0F53B83F97B04C6A4D59EA47D6B7ADDAFCF831BBF4`, `mapAutoConfig.js` `329BF1871C12184BC71B7DEC5D50C65138DD5F8886FA7FEE7A2527DB71784A16`, `MapDataPage.jsx` `BB672102C30F23FD9E223E7E5235B4F796021DD13321D014B7C3C770B0485AFA`, and `autoScanNativeCoordinator.js` `DD0D65F50A39573F671AC023C660989D3C05F5009D9286343508D780994BE9DA`. These identify this intermediate read, not a committed/package milestone.

### Supported corrections

- `App.jsx:210–212,821–870` separates native per-owner cache and global confirmed authority. First edit by a new generation seeds global confirmation from preserved visible state; same-owner earlier success updates confirmation at851 while only latest revision writes UI/storage at852–855. Rejection restores that global reference at860. Current-owner checks before dispatch/ack/rejection preserve A/B/A. This directly addresses the prior native-first rollback defect by inspection. A subtle counterexample to an app-lifetime-only confirmed baseline is old-A optimisticfalse → B preservesfalse → retired-A acknowledgement ignored → new-A first edit rejects: restoredfalse must remain the preserved new-owner baseline, even if initial globaltrue was the old app-lifetime confirmation. Current829–831 handles that case without resetting the checkbox on selection.
- `MapDataPage.jsx:841–855,1037` transports append/remove/set-type operations as optional second callback arguments while preserving the ordinary first-argument patch. `App.jsx:484–495` uses the same intent helper for immediate native drafts and preview local persistence. `mapAutoConfig.js:92–107` rebases semantic arrays, retains ordered dedupe/cap helper behavior and disallows final-type removal on the acknowledged base. The old default-derived replacement defect is corrected in these seams.

### FD01 — successful earlier operations are replayed against their own effects (concrete source defect)

`autoScanNativeCoordinator.js:68–70` accepts an older successful save into `hydratedConfig` but retains its `pendingEdits`; only a latest success removes acknowledged edits at73–74. Semantic operations are not idempotent across changing bases. This was harmless for many scalar patches and uncapped append examples, but breaks the recovered20-target and last-type behavior.

Reason trace, no runtime execution by this reviewer: start with IDs `[1..20]`. While writes are deferred, queue Add21, remove1, Add22. Add21's first save is a cap no-op and succeeds. The second save replays Add21 (still a no-op) then remove1, succeeds with `[2..20]`. The third save replays the already-successful Add21 against that19-item acknowledged base, admitting21; remove1 now does nothing; Add22 hits the cap. Final persisted list is `[2..21]`, while recovered immediate operation semantics require `[2..20,22]`. A previously successful cap no-op has become a later effective append. The corresponding last-type no-op can also become destructive after a later type addition if replayed.

Retire successful acknowledged operation revisions once their effect is incorporated in the authoritative base; retain failed/unacknowledged intent for retries. Add a distinguishing three-edit deferred-ack check, rather than only two Adds or helper-only cap checks. Parent coordinator was informed with exact lines and trace.

Follow-up source: coordinator SHA-256 `0F5D4319C0F25F5379E9D9A266AD34245298B4E60EB791BB1BBB55EC8C2859D7` moves `committedEditRevision = revision` and acknowledged `pendingEdits` removal to71–72, before the older-success branch at73–75. The next queued operation now starts from the acknowledged base and contains only unacknowledged operations. FD01 is **source-fixed / IMPLEMENTED_NOT_VALIDATED by this reviewer**. Failed operations remain pending because the catch path at90–92 does not remove them. An execution result or mounted inverse packet has not been inspected yet.

## Mounted proof source review

Intermediate `LWBridgeWindow.cs` SHA-256 `C1903EFF4AA03BE2746987B05EA779B3A5BF7DC8CD7139BB69FC6453D4B7053B` was read only. No package execution/result is claimed by this review.

-3553–3573 adds actual mounted profile-B Auto Launch rejection while globalfalse/nativeBtrue diverge. It asserts both checkbox and exact global local keyfalse, visible error, and unchanged nativeBtrue. This distinguishes the previously masked native-first rollback defect.
-4210–4254 uses actual native DOM input setters/input events, Enter key, Add/removal buttons and checkbox clicks. It composes duplicate input/two Adds/remove-readd/type edits before deferred status release;4255–4261 verifies persisted base is unchanged during the deferral. The expected result at4266–4271 is native `[317,10,11,9]` and `['city','resource']` with scalar35/normal and unchanged return=false. The source-known City survives default-derived type editing, distinguishing replacement from intent rebasing.4282–4300 checks mounted convergence and restores via the same real controls rather than fabricated component state.
-4303–4343 retains the prior hydration failure → scalar40 retry and unrelated native arrays preservation. That remains useful scalar counterevidence, but is not yet an array-operation failure/retry witness.

Remaining proof gaps at this source snapshot: global inverse divergence and earlier-success/later-rejection; the repaired successful-no-op replay/cap and last-type traces; array operations retained through an actual hydration/save rejection and retry; preview local reload/helper-harness compatibility. These may be supplied by subsequent coordinator checks/packets. Source fixtures or helper checks must be labeled separately from mounted integration; code presence alone is `IMPLEMENTED_NOT_VALIDATED`, not an executed proof result.

## Implementation review continuation

Inspect the coordinator correction and completed Window mounted inverse proof seams when ready, and record updated source hashes/lines, actual parent results and remaining evidence limits here. Parent alone runs checks, integrates and commits; final project-lead acceptance remains separate.

## Follow-up independent inverse execution and updated Window source review

The coordinator explicitly expanded this subreview's evidence ownership to `check-frontend-inverses.mjs` and `check-frontend-inverses.results.json`, and authorized Node-only execution plus immutable starting-source reads via `git show`. Existing review snapshots and historical negatives above remain preserved. No production/test script, build, package, browser, native/game process or Git state was changed.

Executed with Node `v24.18.0` (`C:\Program Files\nodejs\node.exe`):

```text
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/recovery-003-2026-10-07/checkpoint-b/check-frontend-inverses.mjs
node evidence/lwbridge-0.3.17/functions/LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001/recovery-003-2026-10-07/checkpoint-b/check-frontend-inverses.mjs --verify
```

Both **PASS:14 scenarios,62 counted behavioral assertions**. The verify run compares exact source-bound results and never writes. Result creation uses exclusive `wx` mode, so another normal run refuses to overwrite the saved packet. Script SHA-256 `4ff21ad39c7d0eab031c4e330808680bced859c5ba79d9e70bf05ee962e3f3de`; complete source hashes and extracted callback body hashes, byte ranges and line locators are in the results JSON. Current App/helper/Map/coordinator hashes agree with the intermediate sources documented above and the corrected coordinator `0f5d4319...`.

The suite executes the actual extracted `App.updateAutoLaunch` and `App.updateAutoScanConfig` callback bodies, Map `emitAutoConfig`/`addAutoServers`/`toggleAutoType` functions and chip-removal arrow, plus actual current `mapAutoConfig.js` and `autoScanNativeCoordinator.js`. Native acknowledgements/status/storage and profile transitions are explicit inert dependencies. It also extracts App's actual coordinator merge expression rather than supplying a guessed baseline merge.

Immutable starting revision `138ea26469b7a35c0758198c98f8c44f352037b0` is read directly with `git show <SHA>:<sourcepath>` and never checked out or altered. Both globalfalse/nativeAtrue and inverse globaltrue/nativeAfalse reproduce the baseline native-first rollback; baseline actual Add9 before hydration overwrites saved `[317,8]` with `[9]`, while current actual Add yields `[317,8,9]`. These are newly reproduced baseline negatives alongside preserved historical evidence.

Current inverse coverage:

- Immediate global UI/storage; both divergent native gates with failure rollback and truthful error; earlier success/later rejection; stale A/B/A acknowledgement plus an undispatched retired queued write; preserved retired optimisticfalse as new-owner rollback baseline.
- Actual Add/toggle/remove callbacks compose scalar35, two Adds, duplicates and remove/readd before hydration over saved targets/types, preserving native deadline123456; invalid-only Add remains in the input and dispatches no save.
- Failed hydration retains Add9 through a later type/Add10 retry; failed save retains append/remove through a later retry; pending save keeps the latest draft, rebases the next operation and eventually converges.
- FD01's exact cap20 trace now yields `[2..20,22]` after successful Add21 no-op/remove1/Add22. Last-type Truck-uncheck no-op followed by City/Resource additions preserves `['truck','city','resource']`. Profile retirement fences callbacks and queued dispatch while replacement progresses. Preview executes the same actual handlers and reloads the exact selected-profile local-storage config.

FD01 is now independently **validated in the extracted production callback/coordinator composition**, beyond its prior source-only correction. This does not assert mounted, packaged, native scheduler or live parity. The suite does not mount React, dispatch an actual Enter event, exercise actual checkbox disabled state, select real profiles or call a native service. Source-backed Node composition and coordinator-owned mounted evidence remain distinct.

Updated Window proof source, latest inspected file hash `435D2EEE8455BFD1CBD3B3D28842FF10EC60090745E433EB6807E66623F2075B`, adds the following separately from the earlier snapshot:

- `LWBridgeWindow.cs:3630–3649` builds inverse nativeBfalse/globaltrue divergence through actual toggles/profile acknowledgements, rejects a disable, and checks global UI/localtrue with nativeBfalse unchanged. No local-storage injection manufactures the state.
-3650–3660 defers first false, queues true while editable, permits the first matching native call and rejects the next via the isolated matching-command counter at2959–2983. It checks global/local/nativefalse after the second rejection, distinguishing last confirmed success from preceding optimistic UI. A completed package packet is still needed to establish execution of that seam.
-4364–4381 defers actual first Add12 save, queues duplicate Add12/13, remove12 and Resource-on, then asserts exact native `[317,13]` and `['city','treasure','resource']` and real control convergence/restoration.
-4383–4428 exercises real Add14/14 after an initial hydration status rejection, then scalar40, verifies `[317,14]` plus saved unrelated types/fields, and restores via removal. This adds array retry wiring to the previously scalar-only mounted seam. It starts the Add after the first observation failure; the Node suite separately covers an operation already pending when its hydration/save attempt fails.

No new concrete product defect was found in this follow-up. Fresh package result/screenshots, mounted cap/last-type presentation if claimed, full-session issue/cleanup/source/package binding and final lead acceptance remain coordinator-owned and were not executed or accepted by this subreview.

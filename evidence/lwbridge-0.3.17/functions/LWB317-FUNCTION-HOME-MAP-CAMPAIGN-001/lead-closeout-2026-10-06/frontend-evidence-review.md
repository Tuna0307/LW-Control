# Frontend Auto and packaged-evidence closeout audit — 2026-10-06

Audited HEAD: `99d8f7b513ae56a0aa3b4d92a202f81586a32924`; starting Git working tree was clean. This is a read-only lead subaudit. Only this new report was written; the old audit report and all production/tests/Git were preserved. No builds, tests, executables, browsers, games or process control were performed. The main lead independently executes checks/reproductions and owns final disposition. Current counterexamples below are source-permitted traces, pending the lead's controlled execution.

Read: AGENTS.md; parent CAMPAIGN-001; RECOVERY-001; `docs/reviews/2026-10-06-LWB317-FUNCTION-HOME-MAP-CAMPAIGN-001-LEAD.md`; prior frontend report; current source and saved v3 evidence. Existing recovered source authority is unchanged: original `index-BVfnK1wp.js` SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`, original synchronous edit/ref/state/persist callback bytes `[364217,364377)`, synchronous selected-profile load bytes `[370501,370600)`; original `MapDataPanel-B4GXEND2.js` SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`, edit merge bytes `[40514,40544)`, Auto card bytes `[46001,48950)`. This is an `EXACT_CONTRACT` immediate editable frontend contract, with clone-internal native persistence/order/deadline adaptations expressly authorized by recovery checkpoint 3.

Current source hashes:

- `src/LWBridge.UI-0.3.17/src/autoScanNativeCoordinator.js`: `EB5633B7A111F8E94EA772AC25DDE9D5924AFAA8B0876CA9A2303F368981EE28`.
- `src/LWBridge.UI-0.3.17/src/App.jsx`: `3A6A5608735E59EE83DBC5F3C617C5FF3CEF9A65C8EF58AB871D2194ABE8CEEB`.
- `src/LWBridge.Desktop/LWBridgeWindow.cs`: `770903C1EBB6894313C5DEE81F4ED010B42EF1AF2C46114A2D7357BBD796254C`.

## Earlier findings: resolved mechanisms and remaining limits

| Earlier finding | Current source disposition | Proof boundary |
| --- | --- | --- |
| FA-01 older save/status/event replaces an existing newer draft | Corrected for edits already based on hydrated config. `autoScanNativeCoordinator.js:20–38,42–58` tracks local edit/commit revisions, prevents incoming config replacement while pending, and rejects lower native revisions. App routes event/status through that owner at `:721–736`. | Coordinator assertions in `scripts/check-map-integration.mjs:202–251` distinguish pending hydration/event, C1→C2, stale native revision and failed save. First-edit-before-initial-hydration remains a separate defect below. |
| FA-02 old browser deadline undoes native completion | Corrected. `MapAutoScanCommandService.cs:235–245` retains previous native NextRunAt for ordinary edits; only enable transition/disable update it. Run Now has the explicit native deadline mutation at `:275–297`. | `MapAutoScanCommandServiceChecks.cs:205–233` explicitly tests completion then stale browser NextRunAt edit. This audit read the test; main owns execution. |
| FA-03 reverse execution order persists old UI intent | Corrected in the production single-owner frontend path. Coordinator `:40–71,74–93` shares one chain for saves and Run Now, without blocking immediate App edits `:480–488`. | Direct coordinator FIFO assertions `check-map-integration.mjs:202–224,276–296`. Native accepts full config writes without a client intent revision, so arbitrary external concurrent writers are not covered; no such second production writer was established. |
| FA-04 failed config writes swallowed | Config-write portion corrected. Coordinator `:66–68` reports latest write failure and retains dirty edit revision; success `:53–54` clears its channel. App's separate `autoScanSaveError` is shown ahead of runtime error at `:1041`. | Assertions `check-map-integration.mjs:230–251` cover disk-full then subsequent successful save. Run Now action error still shares runtime error state, see FE-02. |
| Profile/unmount stale callback retirement | Substantially corrected. App `:450–478` retires coordinator on owner change/unmount; `:721–736` checks captured profile owner and closed listener; coordinator `:26,47,58,67,81,89,100` fences callbacks and queued invocation. Backend scoped transport uses profile generation in `backendBridge.js:93–120`; Map adapter captures event owner at `mapBackend.js:292–306`. | Coordinator queued-retirement test `check-map-integration.mjs:300–324`; saved native A→B→A/document generation evidence now exists. Previous same-native-mount fixed-profile reachability limitation is superseded by real native selection at `App.jsx:307–334`. |

## FE-01 — P1: first edit before native hydration can erase persisted fields

**New source-confirmed permitted interleaving; main executable reproduction requested.**

Locators: `App.jsx:95–108,450–488,721–731`; `autoScanNativeCoordinator.js:35–38,40–58`; `MapDataPage.jsx`, `emitAutoConfig` and Auto input handlers; `MapAutoScanCommandService.cs:235–245`.

Native `initialAutoScanConfig` starts with normalized defaults rather than the persisted selected-profile configuration. The layout effect resets the visible/ref draft to those defaults. Initial native status hydration is asynchronous. Auto field controls remain editable, preserving original editable semantics, but they now lack the synchronous original persisted base.

Trace: native profile has `enabled=true`, interval=60, serverIds=[317], selectedTypes=[resource], scanMode=normal, returnToOriginalServer=false. Defer first status. The visible initial config has disabled/empty servers/default five types/fast/return=true. User changes only interval to45. App submits that full default-derived candidate; coordinator suppresses initial persisted C0 while edit is pending, correctly preserving the interval draft but also preserving its unrelated default fields. Native replaces all configuration fields except its deadline. The edit disables the previously enabled scheduler and loses servers/types/mode/return even though those controls were never changed.

Original synchronously loaded profile storage before edits; old-snapshot rejection alone does not recover that base. Bootstrap the actual profile-owned config before exposing it, or preserve pre-hydration field edits as patches merged with acknowledged native state while keeping immediate editable intent. Add a distinguishing actual App/native test with a pre-existing non-default config and deferred initial status; the current marker-based coordinator test does not cover this case.

## FE-02 — P2: action errors can still be cleared by unrelated runtime snapshots

**Source-confirmed error-channel regression/gap; main executable reproduction requested.**

Locators: `App.jsx:445–447,470–471,729–730,1041`; `autoScanNativeCoordinator.js:25–31,74–90`.

Write errors are independent now, but `onActionError` and runtime `snapshot.lastError` both write `autoScanError`. A rejected Run Now writes its message at coordinator line89 via App line471. A later same-owner native status/event with no runtime lastError executes App line447 and clears the action failure. Initial status failure also shares this state. Independent command/runtime errors required by RECOVERY checkpoint3 are therefore not fully established.

Trace: Run Now rejects `GAME_CONNECTION_UNAVAILABLE` or an injected command failure; action error appears. Receive a valid native snapshot with higher revision and `lastError=null`; the alert disappears without successful Run Now or explicit local retirement. `check-map-integration.mjs` uses no-op action-error callbacks for its command-order cases and does not assert this distinction. Keep an owner-scoped action failure separate from runtime lastError; do not invent a recovered Retry control or error wording.

## Packaged proof: substantive improvements, identity verified

Inspected both `integration/recovery-v3-en-light.json` and `recovery-v3-ja-dark-narrow.json`, plus both local PNGs through image inspection. Their visible settings match their names: English/light at1120×720; Japanese/dark at900×720. JSON initial/final scrollWidth equals viewport width. The Japanese narrow capture retains contained table/tab scroll areas and has no whole-document overflow in the measured packet. These are clone screenshots with inert seeded data, not protected original-runtime pixel parity.

Both packets pin the same current canonical UI source/artifact identity. This audit independently recomputed the fingerprints by reading bytes, matching the exact path-NUL-file-NUL SHA-256 algorithm in `scripts/production-build-identity.mjs`; explicit ordinal string ordering was used. No build/check script was executed.

- Source fingerprint: `4686126fe21d843506b4938371a5532cdf9a12b4c67d32f87b2e67f35833783c`.
- Artifact fingerprint: `d3f32d1de7108642278f841a274d9bd4dcf3f8ea3a65dec6b758eb45c1d10108`.
- Captured/current executable SHA-256: `a349e1698b47dd9f14b5f52d172a48ccf2643226792521461521e0197c932c69`.
- Captured/current managed DLL SHA-256: `4368114d17656362003cbf1eb5ef6fbf14adcd14ada09b39d87ab22e5cb90f9d`.
- Captured/current packaged index SHA-256: `8a9940972b7c7fdad040317feb35e08a31ffb4983f541c9680a9c30b32b90e71`.

Runner source supports actual controls, rather than raw transport-only actions:

- `LWBridgeWindow.cs:2937–2950` clicks native profile controls and asserts native/UI convergence; `:3056–3130` sets actual language and clicks theme toggle, checking DOM language/theme/navigation.
- `:3136–3165` visits accepted routes and all eight Map tabs; `:3179–3235` owns deferred/rejected Home Auto Launch control transport; `:3294–3380` tests connected/deferred/rejected/disconnected/recovered status through Refresh controls.
- `:2967–3019,3240–3293` edits native Auto interval/servers, enables an inert provider-positive cycle, disables it and verifies native run/cancel state. `CreateHomeMapCampaignRuntimeOwner:615–694` genuinely uses real profile owners/stores and enables inert native Auto/plunder workers while disabling external bridge/recovery transport.
- `:3383–3408,3511–3561` captures delayed first-A search then B, returns A with a new native owner generation and persisted config, and cancels a specific pending B request on document reload.
- `:3410–3458` drives Export cancel/failure/success controls; `:3460–3509` drives B Clear and checks native counts; A's data survives. `:3611–3616` requires named Auto/Map events.
- `:4454–4511` writes post-disposal shutdown state. Packets record closed registries, zero active requests/subscriptions, detached profile event handlers, removed isolated roots and no cleanup failures.

## FE-03 — P2 evidence gap: pre-reload browser issues are discarded

Locators: `LWBridgeWindow.cs:868–883,3531–3533,3578–3580`.

The on-document-created hook creates a new `issues=[]` and replaces `window.__LWB317CampaignIssues` each time. The runner performs document reload at3532 and reads the issues array only at3578. Console/error/unhandled-rejection issues from earlier Home, Auto, export, profile and Map actions are therefore lost when the first document unloads. Saved `browserIssues=[]` proves the post-reload document's captured error hooks only; it does not prove zero captured issues across the whole campaign. Console warnings are also outside the selected hooks and should not be implied by that zero count.

Collect and retain issues before each document replacement, or forward them into an owner-scoped host accumulator, then assert the union. This is a proof weakness, not evidence that browser errors actually occurred.

## Remaining packaged-proof limits and external boundary

The v3 runner does not click Home Launch/Stop/Update-and-launch controls, does not exercise Run Now, does not defer/fail native Auto config writes, and does not reproduce the first-hydration/deadline/older-Auto-reply cases through mounted packaged App. Those requirements need the appropriate independent service/mounted evidence rather than inference from one enable/disable cycle. `afterRestart` is document reload within the same desktop process, not fresh-process host restart. Config reopening via native A→B→A is useful separate proof. Worker-enabled job composition is established, but the packet does not establish all job execution/event cases merely from the boolean.

Selected proof DTOs contain constants after prior assertions (`profileBInterval=55`, `profileBServerId=318`, runtimeComposition booleans, `profileBWasClearedThroughUi=true`); use their source assertions and actual measured state, not those constants alone, as evidence. Clear's captured DOM still says Processing with dashes, while native summary is zero and the old B row is absent; it is narrower evidence than a settled final empty-row/count presentation.

Inert provider acceptance and seeded all-eight rows establish local/native composition only. Treasure inspection/claim/status and Ghost preparation intentionally throw unavailable errors at `LWBridgeWindow.cs:641–657`. Current-client positive population, real server navigation/actions/jobs, real Home process lifecycle and original protected-runtime pixels remain external `BLOCKED`/`IMPLEMENTED_NOT_VALIDATED` boundaries according to their respective source-backed ledger, not LIVE_PROVEN upgrades.

Recommendation: preserve the substantial corrected FA mechanisms and package evidence, but do not close frontend/native ownership acceptance until FE-01/FE-02 are independently reproduced/disposed and corrected where confirmed. Repair FE-03 before asserting whole-campaign zero browser issues. Main lead integrates this report with native/Home audits, executed checks and final scope disposition.

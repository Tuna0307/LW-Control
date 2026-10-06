# Frontend Auto Scan ownership audit — 2026-10-06

Scope: read-only project-lead subaudit of the uncommitted frontend/native Auto integration at HEAD `540bc53d73053bb1eb70d6a09fea851a9e96d625`. Only this report was written. No production/test/Git mutations, builds, tests, process launches, browser control or game activity were performed. Findings below are source-derived execution traces; they are not executed reproductions or LIVE_PROVEN results. Main lead owns independent execution and final acceptance.

## Source identity and recovered contract

Current files inspected, SHA-256:

- `src/LWBridge.UI-0.3.17/src/App.jsx`: `EB7705E647025D100538C8730DE2A8768072554A5B1CAA3C8A866042F9210C0C`.
- `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx`: `4078443088D6F7D01EA2F94EF2213A9D0785C0A2195EAF8C99FC89EFD264A9BD`.
- `src/LWBridge.UI-0.3.17/src/mapBackend.js`: `A6888FDF4A515A06278BB4C2212764CCAB91CA6ACBEC401F2806E42719626476`.
- `src/LWBridge.Desktop/MapAutoScanCommandService.cs`: `9C41208D927C5F3E2DBEA90D5AA0AA0671F259543EF4852AD1674B768C911B62`.

`EXACT_BYTES`: re-read original recovered asset byte ranges, rather than relying only on the earlier review:

- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`, UTF-8 bytes `[364217,364377)` contains original `Ct`: normalize candidate, compute enable/disable deadline, assign ref and state immediately, then synchronous profile-local `ji` persistence. Bytes `[370501,370600)` load selected-profile config into ref/state on profile change.
- Same asset: bytes `[359327,359801)` normalize; `[360013,360170)` load `lwbridge.mapAutoScan.${profileId}`; `[360170,360259)` persist normalized configuration.
- `MapDataPanel-B4GXEND2.js`, SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`, bytes `[40514,40544)` shallow-merge field edits; `[46001,48950)` editable Auto controls, including Run Now editing `nextRunAt:Date.now()` and its enabled/online/running/manual-reading admission condition.
- Locator manifest: `evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/original-source-locators.json`, `index.parentConfigCallback`, `index.profileLoadEffect`, `index.helpers`, `panel.mergeCallback`, `panel.autoCard`. Both asset hashes were recomputed during this audit.

`EXACT_CONTRACT`: accepted immediate editable config is frontend-owned in the recovered implementation. Native persistence/scheduling migration is campaign-authorized clone composition, but must preserve those edits and single scheduling ownership. No original asynchronous Auto-config acknowledgement command/revision contract is claimed.

## FA-01 — P1: hydration, events and replies overwrite newer editable config

**Defect, source-proven permitted interleaving; execution proof still required.**

Locators: `App.jsx:295–301`, `304–319`, `545–555`; `MapDataPage.jsx:326–327`, `367–368`; `MapAutoScanCommandService.cs:233–249`, `267`, `778–803`; `LWBridgeWindow.cs:3150–3165`.

`acknowledgeAutoScanSnapshot` assigns every snapshot's entire config to both ref and rendered state, and also overwrites running/error. There is no local edit revision, native snapshot revision or request generation. The save response only checks profile-ID equality. Initial status and StateChanged events use the same replacement callback. Controls remain editable while requests are pending, as recovered.

Trace A: startup status has captured old config C0; user edits interval to 45 (C1) and immediately sees it; deferred startup status resolves with C0; line 550 passes profile/closed checks and lines 298–299 replace C1 with C0. No timeout or foreign profile is necessary.

Trace B: user issues C1, then C2 before C1's native event is delivered. The C1 event replaces the C2 draft. If the user now changes another field, the child merges into that obsolete C1 config and submits C3, which omits the prior C2 change. A later C2 response may temporarily repair the display, but C3 has already resubmitted the obsolete fields to native persistence.

This is stronger than harmless temporary display flicker: the ref becomes the basis of subsequent full-object writes. Generation-fencing only promises is insufficient because subscription events also carry config. Runtime running/error/deadline convergence and editable-field acknowledgement need explicit ordering ownership.

## FA-02 — P1: stale browser deadline can undo native completion scheduling

**Defect, source-proven permitted interleaving; execution proof still required.**

Locators: `App.jsx:304–313`; `MapDataPage.jsx:367–368`; `mapAutoConfig.js:82–86`; `MapAutoScanCommandService.cs:233–240`, `262–267`, `402–432`, `619–652`.

An ordinary field edit sends the entire browser config, including `nextRunAt`. Native UpdateConfig replaces the entire stored config; it changes that deadline only on disabled→enabled or disabled input. Native finalization separately owns deadline advancement.

Trace: enabled Auto finishes and stores `nextRunAt = completion + interval`; its event is queued, so browser still holds the prior due deadline. Once the prior cycle task is complete, user changes speed or interval before that event arrives. The full submitted config carries the old due deadline. Because enabled remains true, native lines 235–238 retain that stale deadline; line 264 attempts admission immediately; lines 412/423 can admit another cycle as soon as the prior task is completed. The scheduled waiting interval is lost and the user did not press Run Now.

Preserving backend-owned deadline for ordinary config edits, with an explicit Run Now operation and a versioned enable/disable transition, is needed to establish one owner. Native mutexes prevent overlap, but do not prevent this sequential immediate extra cycle.

## FA-03 — P1: concurrent full-object writes are not ordered by UI edit intent

**Defect, source-proven permitted interleaving; actual thread schedule not exercised.**

Locators: `App.jsx:304–319`; `backendBridge.js:66–78`; `LWBridgeWindow.cs:2856–2875`; `NativeRequestExecutor.cs:21–38`; `NativeRequestRegistry.cs:19–28`; `MapAutoScanCommandService.cs:230–240`.

Each edit starts an independent bridge request; no save chain or revision identifies latest intent. Host invocation runs each command through independent Task.Run. The request registry excludes duplicate IDs, not concurrent commands; each edit gets a distinct ID. The state semaphore serializes entry order, which is not UI intent order.

Trace: edit C1 changes interval; C2 adds a type while preserving interval. Host task C2 acquires stateGate first and persists C2, then C1 acquires it and persists C1. C2's newer type is lost durably. Responses/events lack revision information and the frontend accepts whichever snapshot arrives. FIFO WebView message receipt does not impose FIFO Task.Run scheduling.

Serialize config writes per owner or reject stale edit revisions at native storage; preserve editability throughout. Also cover pending enable→disable and disable→enable so an older intent cannot become final persisted state.

## FA-04 — P2: rejected configuration writes are silently swallowed

**Defect in failure handling; no claim of original save-error wording/UI.**

Locators: `App.jsx:313–319`, `295–301`, `330–331`, `553–554`; `MapDataPage.jsx:1072–1073`.

Config rejection discards its error, performs a status read, and discards status failure too. In contrast, Run Now and initial status report errors via `autoScanError`. If config save rejects but status succeeds, the draft silently rolls back without exposing the failed write. If both reject, the optimistic draft remains displayed indefinitely although native persistence was not acknowledged, and there is no queued retry or retained failure. A later runtime snapshot with no `lastError` clears any command error in the shared Auto error channel.

Required distinguishing proof: fail a config persistence request, resolve/reject the follow-up status independently, perform a newer edit before failure settles, and ensure failed persistence is observable without losing newer intent. Do not invent a recovered Retry button; use a justified local failure/retry policy and preserve accepted labels.

## Retirement/profile observations — implementation gaps versus reachable defects

`App.jsx:545–560` correctly gives startup status and subscription handlers a `closed` owner; cleanup unlistens. `mapBackend.js:370–373` unwraps and excludes foreign-profile envelopes. `LWBridgeWindow.cs:3205–3224` supplies profile and document identity. These protections should be retained.

However, config/Run Now promise callbacks have no `closed`/mount-generation gate. The failed-save follow-up `autoScanStatus().then(acknowledgeAutoScanSnapshot)` at line 317 has no profile check after that await. An A failure can start the follow-up while A is selected, and the callback will apply it after an A→B replacement if such replacement occurs in the same owner instance; A→B→A also defeats equality-only save guards. No frontend invalidation/cancel callback retires those requests on App unmount. Running native work surviving a hidden Map route is campaign-authorized; hidden-page activity is not itself a defect.

**Reachability limit:** current native `selectedProfileId` is immutable `backendBridge.profileId` (`App.jsx:100–103`; `backendBridge.js:25`), while same-mount shell profile changes are preview-only and do not use these native requests. Therefore a production same-mount cross-profile leak is **UNKNOWN**, not an established current production defect. On React unmount, stale setters target the abandoned instance, not automatically the replacement instance. Native request/document cancellation provides additional protection (`NativeRequestRegistry.cs:65–79`, `LWBridgeWindow.cs:2876`, `3227`). Missing frontend callback retirement remains a proof/maintenance gap; actual native same-profile stale acknowledgement and deadline defects above do not depend on profile replacement.

## Submitted proof limitations and acceptance checks

`scripts/check-map-integration.mjs:57–66` checks source patterns for hydration/listening/native transport and browser-scheduler removal. Lines 199–205 test event envelope filtering, and 231–234 invoke adapter commands. The fake bridge returns `{}` for these Auto commands (`169–181`). This does not execute App's Auto owners or assert draft preservation, deadline authority, failed-save recovery or teardown. `map-lane/auto-scan.md:72`, `94–100` explicitly leaves packaged frontend/native integration to coordinator; its inert core checks do not close frontend ownership findings.

Lead-owned executable distinguishing cases should mount actual App/native command boundaries and control response/event ordering:

1. Pending initial status → two edits → old hydration; newer editable fields stay intact.
2. C1 event/reply after C2 draft → third unrelated edit; persisted config retains C2.
3. Reverse native execution order of two edits, including enable/disable; latest UI intent wins durably.
4. Native completion before pending ordinary edit is processed; deadline stays future and cycle count does not increase until Run Now or due time.
5. Pending Run Now plus newer config, rejected save plus successful/failed/deferred status; config intent and independent errors survive.
6. Close/remount and native profile/document replacement; callbacks retire, native work ownership follows the established host contract, and no retired callback changes replacement state.

Report conclusion: frontend Auto integration remains **IMPLEMENTED_NOT_VALIDATED / CHANGES_REQUIRED** for the source-derived races above. No live/provider/runtime parity state is upgraded. Production/test files and Git index/history were untouched by this subaudit.

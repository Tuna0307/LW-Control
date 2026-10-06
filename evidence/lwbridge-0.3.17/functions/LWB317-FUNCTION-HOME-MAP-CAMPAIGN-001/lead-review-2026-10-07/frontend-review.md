# RECOVERY-002 frontend and packaged evidence review — 2026-10-07

Review HEAD: `59d3cde5192f03d39ae01580e05d1a6249158b20`, compared with `da629bce`. Starting working tree was clean. Scope is independent read-only frontend/global preference/status/Auto and v4 packet review. Only this new report was written. No production/test/Git edits, checks/builds/native executables/browser/game/process control were performed. Coordinator owns executable inverse checks and final acceptance. New findings below are exact source-permitted traces until linked to coordinator reproduction.

Read AGENTS.md, CAMPAIGN-001/RECOVERY-001/RECOVERY-002 instructions, prior lead/frontend findings, current changed source/tests, and `recovery-002-2026-10-06/{README,package-en-light.json,package-ja-dark-narrow.json}`. Both saved screenshots were decoded and visually inspected from disk.

## Fixed prior defects

| Prior defect | Current correction and distinguishing coverage |
| --- | --- |
| HCF-03 older connected pair restores availability after newer failure | `App.jsx:569–589` uses shared statusReadRevision for startup/Refresh/poll and checks latest/current owner before setters. `:709–715` advances revision before applying native status events. `check-home-integration.mjs:297–362` executes exact reader body with two controlled reads. Packaged `LWBridgeWindow.cs:3856–3896` defers old read, settles a newer proxy rejection, releases old connected work and verifies failure remains. |
| HCF-05 bootstrap/profile poll replaces global visible Auto Launch | `App.jsx`, `initialAutoLaunchGame`, now reads only the recovered `lwbridge.autoLaunchGame` key; absent/nonliteral-false defaults true (`autoLaunchPreference.js:1–9`). `App.jsx:594–603` caches native per-owner mirror without setting global state/storage. V4 records A-global-false/B-native-true selection and reload. Failed divergent-mirror rollback remains below. |
| FE-01 unrelated scalar edit before hydration erases saved fields | Coordinator `autoScanNativeCoordinator.js:44–64` hydrates native base before persistence and applies queued field patches; App `:464–498` supplies patch+edit-time merge and remains immediately editable. `check-map-integration.mjs:184–291` covers two scalar edits and failed hydration/retry. V4 captures nondefault types/server/return preserved through interval35 + normal speed, followed by interval40 retry. Array operations remain below. |
| FE-02 runtime snapshot clears Run Now action failure | App `:197–201,443–447,470–476,1063` separates save/action/runtime state. Coordinator `:101–111` clears action failure on successful Run Now and records failed Run Now independently. Packaged `LWBridgeWindow.cs:3763–3775` checks identical visible action failure after clean runtime event; both v4 packets retain the error. |
| FE-03 reload discards earlier browser issues | `LWBridgeWindow.cs:961–992` forwards warning/error/page/rejection records to host; `:4990–5021` accumulates recognized document/session generations. `:4267–4305` preserves pre-reload sentinel and simulated queued old-session issue; `:4354–4376` asserts host union excluding only expected sentinels. Both v4 packets contain the two generation1 sentinels, empty current document and unexpectedCount0. Previous per-document-only zero claim is corrected. |

Prior native-revision/FIFO/deadline/retirement mechanisms remain intact. This review did not rediscover or reopen their fixed defects. The recovered authority remains original immediate editable config and synchronous profile load: `index-BVfnK1wp.js` SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`, callback bytes `[364217,364377)`, load bytes `[370501,370600)`; panel edit/add bytes `[40514,40620)` in `MapDataPanel-B4GXEND2.js`, SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

## FR-01 — P2: failed global Auto Launch edit rolls back to profile native mirror

Locators: `App.jsx:599–603,825–860`; v4 runner `LWBridgeWindow.cs:3549–3576`.

Global visible/local intent is correctly preserved on profile selection and polling. However, failed-save rollback prioritizes `autoLaunchNativeCommittedByOwnerRef.get(ownerKey)` over `previousGlobal` (`:857–860`). A native profile gate is explicitly allowed to differ from global intent. It therefore cannot generally supply the global rollback value.

Trace: visible/local preference=false, selected B native gate=true; paired config read has cached B=true without changing global. User clicks on. Native `local_config_set` rejects. Current failure callback obtains B's cached true, writes global/local true and displays true, despite the rejected edit and previously global false. Inverse values produce the corresponding unwanted false rollback. Exact generation/latest-write guards do not prevent this same-owner case.

Saved v4 rejection tests only returned A with global=false and native=false (`Window:3553–3576`), so those values happen to agree. The earlier B differing-gate selection case is not combined with a rejected edit. Required inverse: different native mirror/global preference, reject latest edit and preserve previous successfully committed global intent while leaving B's native gate untouched; also retain first-success/second-failure concurrent edit behavior. Keep global commit authority separate from per-owner native mirror.

State: source-confirmed defect candidate awaiting coordinator's exact-current callback/mounted inverse result. No original save-error wording or new control is inferred.

## FR-02 — P1: pre-hydration Add/type edits still replace arrays computed from defaults

Locators: `MapDataPage.jsx:367–369,841–853,1034,1050`; `App.jsx:95–108,450–454,468–473,485–498`; `autoScanNativeCoordinator.js:53–64`; `mapAutoConfig.js:38–40`.

Scalar patch rebasing fixes the prior unrelated interval/speed case. Add and type operations still compute full arrays from the rendered default-derived config before native hydration, then submit those arrays as absolute field patches. Shallow native-base merge cannot recover the intended append/remove/toggle operation.

Trace: native saved serverIds=[317]; native App visible initial serverIds=[]; defer initial native hydration. User types9 and clicks Add. Actual Add callback produces `{serverIds: appendAutoServerIds([], '9')}` → `[9]`. Coordinator hydrates saved `[317]`, but App merge applies `{...base,...edit.patch}`; native receives `[9]`, losing existing317. Add is source-backed append, not replacement: original `wi` bytes `[359234,359284)` in the index combines prior IDs with new draft before parsing/deduplication; original panel Add bytes `[40544,40620)` runs against synchronously loaded config.

Types have the same shape: native `[city,treasure]`, visible default five types; checking Resource Point sends default-five-plus-resource, removing city and adding unrelated defaults. Toggle/removal should apply exact user intent to the acknowledged base; an absolute array derived from unknown defaults is not that intent.

V4 pre-hydration mounted case changes only interval and speed; coordinator checks likewise use scalar patch operations. Required inverse: actual Map Add before native hydration preserving saved ordered targets; two Adds while hydration/save is pending; type add/remove preserving native unrelated selections and last-type constraint; failure/retry and A/B/A retirement. Do not disable original controls or redefine append semantics to hide the race.

State: source-confirmed defect candidate awaiting coordinator's actual-handler/coordinator/native inverse. This is narrower than claiming the corrected scalar hydration path still fails.

## Current v4 evidence identity, appearance and limits

Read-only independent SHA-256 fingerprint recreation used the production identity algorithm (ordinal relative-path + NUL + file bytes + NUL); no build/check was run. Current source and packaged UI match both v4 records:

- Source: `cd86aec04987bf5683d38e27d05af5a4c26a773e3be65ea3dc5133463be2d150`.
- UI artifact: `1ae836263c78106f014fcc44d23f9df5b6f2b22453f0462c9e968094419390c5`.
- Packaged index: `1addba61cd6960c757ffa3b985c759ce171e9fccd139e827a0fc7210ca5c9c1c`.

Packets pin captured executable `7bbdb937d836df56c105930e6e30bd04546f376cebacb974bf58515adcf6330b` and DLL `ecd99e62c0edfe6fa8594bce350cea824b433844e4176b1ebcc7182dbcf9c545`. Shared Release executable/DLL at audit time instead hashed `EE89D17ECCD11525A80779C8ACBDCD81C70CA2D71E1FAC24D749D1F16BFD8960` / `E250F7379F540D20FC48F0F1ECF6C822A5FA96A4CC73147B8A65E9973F9B889E`, during coordinator verification/rebuild activity. This does not establish worker source/package misbinding; only current binary equality to historical capture is not asserted here. Coordinator owns canonical build/reproducibility verification.

Decoded screenshots visibly show English/light1120×720 and Japanese/dark900×720. Final stopped/offline game status and disabled action controls agree with inert lifecycle teardown, while durable City/Resource rows remain. This is bounded clone appearance/control evidence, not protected original-runtime pixels. Both JSON packets record locale/theme after document reload, complete-session expected issues, and shutdown with zero active requests/subscriptions, detached profile event handlers, isolated-root removal and no cleanup failures.

V4 now contains actual mounted Home lifecycle Start/Close/update-repair/recovery evidence, unlike v3; actual Auto completion/Run Now failure/cancel, pre-hydration scalar retry, profile replacement, route visits, exports/native events and cleanup are materially stronger. The retained `afterRestart` field is document reload, not a fresh process by itself. Native service restart checks are separate evidence. Expected sentinels mean the accurate issue claim is zero unexpected captured issues, not an empty issue list. The late old-session sentinel is deliberately posted from the new document with old identity and proves host acceptance of queued old-document identity, not actual browser scheduling of a retained old context.

All packet providers remain explicitly inert/isolated. Live current-client population, real server movement/actions/jobs, protected Treasure claim/status, Ghost preparation, updater and original protected-runtime pixels retain external/unexecuted boundaries. No LIVE_PROVEN/whole-clone parity promotion is supported by this audit.

Recommendation: retain accepted fixed mechanisms and the stronger v4 packet. Before frontend closeout, reproduce and adjudicate FR-01/FR-02 at the exact current seams; add inverse evidence and correct confirmed behavior. No additional current paired-status or whole-session issue-capture defect was established in this read-only review.

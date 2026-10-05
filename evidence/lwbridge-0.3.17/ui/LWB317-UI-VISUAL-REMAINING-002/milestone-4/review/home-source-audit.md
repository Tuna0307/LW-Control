# Milestone-4 independent Home source audit

Date: 2026-10-05
Result: **NOT READY**

The current `HomePage.jsx` renderer is source-shaped for the recovered Home DOM and the new Milestone-4 source-render/browser packet closes the earlier isolated-render gap. The remaining blockers are in `App.jsx` event ownership/lifetime and live shell composition, plus one requested whole-shell disconnected-state proof case. I found no new Home markup mismatch beyond the standing native/provider availability fences.

## Source identity and exact anchors

The audited original frontend is `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`, SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`. I rechecked that file hash directly. The pinned executable remains SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

Exact Home/source slices used by this review:

| Source slice | UTF-8 byte | Length | SHA-256 | Contract used here |
| --- | ---: | ---: | --- | --- |
| `Ir` error-code extractor | 328453 | 231 | `CA958551BC82E4F26D90DED7EB366DF299EF9ECBC76A5EEB077DBD25170DDE96` | extracts symbolic error codes |
| `Lr` translated error | 328684 | 166 | `5C1443BDC8967A96D3C741EA696A86A6841FA61D6474B08117865C7973907A26` | error/auth/update namespace lookup, then `common.actionFailed` |
| `Kr` Home control predicates | 336469 | 225 | `555C90231DB83C2118F1DF96EA03B3B7BD5073BEA1CE38D6E5C3C7FFC13AA3BB` | root picker/start/stop/repair predicates |
| `Bn` switch | 213332 | 564 | `5954BED803D64D2F20278C6F74585429EB121281A6F40C9A9BD6CD6A17F858F7` | `disabled` defaults false |
| `qr` Home renderer | 336694 | 2225 | `6547A9E79620B4F02E14397FC59359D2FF8616C0F45AB394A1756D042F5FF385` | complete Home presentation |
| parent `Gi` | 361306 | 14461 | `AD5ECE3C33F5FB057E250B67ABF8D32B4F482FD4A2414ABF55A79A3B3333BFD5` | owns Home inputs/effects/callbacks |
| `Gi` Home `qr` call | 373291 | 390 | `71CC5B675938481B48D4283067EC4CFA9F6B87C3567B0A3DEA10E7F73A87E807` | exact parent-to-Home prop binding |
| `Nt` start callback | 365794 | 168 | `447DBA5D8993C6711EC490CE46A807E06F84594DC9BDBD70B838D4FABA4AC72E` | lifecycle action-error lifetime |
| `Pt` stop callback | 365962 | 247 | `0D3B071D2FECA4063D9DAA1C6D82C8CA114D73C85BAE29E6B32E91A2ACD5EAD8` | lifecycle action-error lifetime |
| `Ft` update/restart callback | 366209 | 276 | `F4E94B25C26B0A76AAC6126EF013F8B3C881584ADD9876B8803A7AC8BDBCFDE7` | lifecycle action-error lifetime |
| `Jt` root acknowledgement | 367489 | 26 | `B1682D572B36B203D965ECE70ABC14075A17A069CA7BF852EB6C2874033D41F4` | set root status, clear root error only |
| `Xt` root picker | 367703 | 130 | `494A67A313DE66D404BD912C9F187577523BBF12F4E58A7CE3E316B0FF610D4C` | cancellation keeps old error; invalid sets `INVALID_GAME_ROOT` |

The root selected-profile effect begins at byte `369239`; its initial root acknowledgement is `Ht().then(Jt).catch(...)` at byte `369540`. Its periodic poll at byte `369979` is exactly `Promise.all([ot(e).catch(()=>null),Vt(e).catch(()=>null)])`. The same selected-profile effect performs the one initial recovery-status read and registers the guarded `bridge://game-recovery` listener.

Auto Launch is separately pinned by `HOME-PREFERENCE-LIFETIME-001A`: storage contract byte `246750`, SHA `2CC3B62C74F3B453001907E3CF4BBB0F2AA232B16114ACD89784B2C248B11C71`; setter byte `248381`, SHA `2BF833633900F487B29D4B329886B79B868E9C161E434C5EADF648F85C48E9E3`, exact text `setAutoLaunchGame:e=>{Sr(localStorage,e),p(e)}`; Home binding byte `338639`, SHA `01485F0ED15137395B8BDD36E0ACFCAA26FB0F5021CFDA5F46447259910057AF`.

Auto Reconnect is separately pinned by `HOME-PREFERENCE-LIFETIME-001B`: draft engine byte `191297`, SHA `D697645DA61F11176EAE639FEADE2596FA79BF87E2D9B8FD37486CE1AC8D0F9F`; reconnect store byte `362296`, SHA `EF8F2CDEC84561EB795F4A0D462E7D7247445C87B5F498CEAC8EFC86D8B00643`; edit/flush byte `366485`, SHA `51FF3AA9B6F6A230A22A8134BC286C6FFADE2E709CA3661AC2D2A61183DC8CE7`; Home binding byte `373492`, SHA `67EB65E2E4710E2C3ABE03E108A02E15807EC5521EE8EEE191F228320E7991A0`. The original shell banner anchor is byte `372793`, SHA `41639CBAFF067D9060A5B90B83B1A1F2278D5C81BB3D72EFB44D725552B618F1`, and the four-store banner binding at byte `372825` has SHA `2BE8757AA40587A3B7D1A063A306C50B0B644A64765954D4A2B981A2CFE026D3`.

Current identities at audit time are `HomePage.jsx` `F140264E693E89F37CBB357470F58FE5DCA46F3DFDA29A8A5D2C7918CE2393FF`, `App.jsx` `6CA27370A90A3B5B0556F7DEA43A131E2C2C0DC3EC57D9F12770BA47A94A74D5`, `ShellPresentation.jsx` `B1DD1278F274A1FD347AD4C27787B5AE8DC1DB32230D49B55F7A471DE34A904F`, `sharedPageUI.jsx` `95ED15F5A4AB46C7CAE4DD55656E85718F66FA5AC45278B615D1795EBD8E82F2`, and `Pages.jsx` `C5DF951E95561A08A4614445ED61586AA6DF4E4B7AE3F11A6A546A52EBA19191`.

## Already-fixed / source-shaped isolated behavior

The present `HomePage.jsx` lines 77-100 reproduce `Kr`/`qr` root resolution and status precedence: unresolved checking; missing/invalid root; launch busy; proxy busy; active recovery; repair; running connected/disconnected; stopped. Lines 109-142 preserve the source ordering of root picker/controls, independent action error, Auto Launch, Auto Reconnect, then failed-recovery detail. Root error is used only inside the missing-root row; action error stays an independent Home error after the controls. `recovery.failedDetail` still translates its nested error through the recovered `Ir`/`Lr` logic.

The new Milestone-4 `home-source-render/render-results.json` is current, unlike the older OFFLINE-VISUAL Home result: it pins Home SHA `F140264E...` and exact `qr` SHA `6547A9E...`. It records 42 EN/JA source-render cases, 34 exact DOM matches. The eight remaining DOM differences are the lifecycle/unavailable-provider disabled attributes. Its pixel packet records 40 pairs: **24/24 exact-required pairs pass** and all 16 availability-fence pairs change only in the declared fenced class. This also shows the old OFFLINE-VISUAL preference-saving differences are no longer current defects.

Both preference units remain source-shaped in their accepted isolated scope. Auto Launch is immediately editable and is not disabled while its compatibility persistence write is pending. Auto Reconnect uses the recovered profile draft/store lifetime, stays editable while saving, and retains independent Retry/Discard ownership in the shell. Current Home SHA `F140264E...` is the same Home SHA recorded by accepted 001B; later App SHA changes therefore must not be treated as a Home regression by hash alone.

Root acknowledgement also remains source-shaped: `App.jsx:214-217` clears `gameRootError` on successful root acknowledgement without clearing `gameActionError`; `App.jsx:465-482` keeps cancellation neutral, sets invalid root explicitly, and acknowledges the post-selection root status. That is the accepted HOME-ERROR-002-R1 separation. Auto Reconnect no longer writes the shared Home action-error channel (`App.jsx:459-463`), which is the correct separate-store ownership.

The Home-specific inner ancestry is now substantially proven. Exact original source at byte `372793` places four config-save error presenters at the start of `profile-view-context`, then profile-switch loading or retained `Activity` pages, with `qr` in the overview activity. Current `App.jsx:720-733` has the same `main-view > Suspense > profile-view-context > ShellConfigSaveErrors > switch-loading/RetainedPages` shape; `RetainedPages` at `App.jsx:81-90` uses `Activity`; `Pages.jsx:33-43` resolves overview to `HomePage`. The new `browser-current-results.json` records Home beneath `profile-view-context` with top bar/eight nav entries in EN/light desktop and JA/dark narrow states, and separately proves that the shared component can render four ordered save-error banners before Home with independent Retry/Discard. That four-banner browser state is injected preview/test state, however; current normal production composition still supplies only `[null, null, autoReconnectStore, null]` at `App.jsx:723`. That is handled as a source-composition blocker below, not as a provider-disabled visual fence.

## Remaining blockers

### 1. Auto Launch edit clears the wrong error owner

Original Auto Launch setter byte `248381` only writes `localStorage` and the Auto Launch React state. The Home binding byte `338639` passes that setter directly. It does not touch the lifecycle action-error state. In the same original parent, `Nt`, `Pt`, and `Ft` each clear the shared action error when a lifecycle action starts; that is the source-defined owner/lifetime of that error channel.

Current `updateAutoLaunch` writes the local preference/state and then unconditionally calls `setGameActionError("")` at `App.jsx:429`. Therefore changing **Open games at startup** can erase a pre-existing Home lifecycle action error even though the recovered setter cannot. The accepted 001A packet proves immediate preference lifetime and compatibility persistence behavior; its own recovery record explicitly limits native failure semantics and does not prove coexistence with a pre-existing lifecycle action error. This is a current source-local event-lifetime mismatch, not a stale-hash issue.

Required correction/proof: keep the accepted immediate Auto Launch draft/native-mirror behavior, but stop a preference edit from clearing an unrelated lifecycle action error. If the compatibility mirror still needs a visible failure, give that failure owned/tagged state so a later Auto Launch edit clears only its own prior mirror error. Add a mounted current-App case that starts with a lifecycle action error, edits Auto Launch through success/failure/retry as applicable, and proves the unrelated action error lifetime is not shortened.

### 2. Recovery state is periodically overwritten although the source makes it initial-read + event-owned

Original `Gi` performs one recovery-status read in the selected-profile bootstrap and then updates recovery through the guarded `bridge://game-recovery` listener. Its recurring poll at byte `369979` reads only status and game setup/proxy status. Recovery is therefore not a five-second poll-owned Home state in the recovered source.

Current `refreshStatus` includes `game_recovery_status` in every call (`App.jsx:275-280`) and assigns every fulfilled result with `setGameRecoveryStatus` at `App.jsx:285`. `refreshStatus` is called by the five-second status interval, while `App.jsx:302-308` also listens to `bridge://game-recovery`. A late/stale poll can consequently replace a newer event-owned recovery state or failed detail in a way the source recurring poll cannot.

Required correction/proof: make recovery ownership source-shaped at the current adapter boundary: one selected-profile initial recovery acknowledgement plus the recovery event listener under the same current-profile/closed-generation guard, and remove recovery from the recurring status/proxy poll. Then mount the actual current App and prove event -> late poll ordering cannot regress recovery state/detail, including profile replacement/return under the clone's standing profile constraints.

### 3. Normal Home shell composition is missing three source-owned flag error stores

Exact `Gi` banner binding at byte `372825` maps four live source stores before the Home/profile-loading branch in this order: Weekend Shield, Attack Shield, Automatic Reconnection, Auto Close Popup. Current `ShellConfigSaveErrors` can render that exact four-slot order, and the Milestone-4 browser preview proves its Retry/Discard ancestry. But normal `App.jsx:723` supplies `[null, null, autoReconnectStore, null]`, so only Automatic Reconnection can produce the source-owned shell banner in the real current composition.

This is not the same kind of provider-disabled fence as the unavailable lifecycle buttons. The recovered source owns four profile-scoped draft/error stores and presents their errors as siblings before Home. Preview injection demonstrates component capability but does not make the normal Home composition source-equivalent.

Required correction/proof: wire the source-owned Weekend Shield, Attack Shield, and Auto Close Popup profile flag stores into `ShellConfigSaveErrors` in the recovered four-store order while preserving the already-accepted Auto Reconnect store lifetime. Then exercise real current store errors independently before Home, including one error while another store is saving and Retry/Discard ownership. The broader outer-shell ancestry mismatches are tracked by the independent shell audit; this Home audit does not duplicate those fixes.

### 4. Requested running-disconnected whole-shell case is still missing

The current source itself has the right branch: `HomePage.jsx:99` selects `status.gameRunning` only when `state.online` is true and otherwise selects `setup.bridgeDisconnected`; preview fixture `home-running-disconnected` exists at line 32. The current source-render packet proves `running-connected`, and the current browser packet proves `home-connected`, but neither current Milestone-4 packet mounts `home-running-disconnected` inside the whole shell. The old HOME-BUSY/OFFLINE records are useful source history but have older production hashes and cannot substitute for this requested current composition proof.

Required proof: add `home-running-disconnected` to the current whole-shell browser matrix (at least the same EN desktop / JA narrow coverage used for `home-connected`) and assert the disconnected status plus Home ancestry. This is a proof gap, not a source-code mismatch.

## Coverage call

- Root unresolved/missing/invalid/valid: **source-shaped** in current Home; current source-render covers checking, missing/root-busy, missing+root/action error, and valid stopped/running/repair states.
- Root versus action error: **source-shaped rendering and root acknowledgement are already fixed**; blocker 1 is the remaining cross-owner Auto Launch event lifetime.
- Recovery detail: **source-shaped rendering**; blocker 2 is the parent producer lifetime.
- Auto Launch / Auto Reconnect: **accepted isolated edit/save lifetimes preserved**; old save-disabled OFFLINE-VISUAL differences are stale. Auto Launch still has the cross-owner error-clear problem above.
- Busy precedence/overlaps: **source-shaped**; current source-render includes launch/proxy/root/recovery overlap states and pixel-exact required cases.
- Connected/disconnected: **source branch matches**; connected whole-shell proof exists, disconnected whole-shell proof is still missing.
- Provider-disabled fences: **intentional standing fences**, not regressions, for genuinely unavailable native actions. Lifecycle controls and the explicit unavailable-production switch state account for the declared Home render non-exact pairs. Missing source-owned shell flag stores are blocker 3, not normalized into this fence.
- Full Home inside shell ancestry: **inner current structure and injected four-banner ancestry are proven, but normal composition is not source-complete** because blocker 3 omits three live shell stores. The separate shell-source audit also owns remaining outer-shell wrapper/exit/profile-loading composition issues.

**NOT READY** until the two `App.jsx` event-lifetime mismatches are corrected and re-proven, the normal four-store shell error composition is restored, and the current whole-shell running-disconnected case is recorded. No additional `HomePage.jsx` markup correction is indicated by this audit.

Limits: this is source/local UI and controlled offline evidence only. It does not claim live native/gameplay provider execution, protected-original runtime execution, or native persistence equivalence beyond the already accepted adapter boundaries.

## Follow-up — 2026-10-05 after coordinator fixes

Result: **READY**

The four first-pass Home blockers are closed in the current files. The recovered Auto Launch setter at byte `248381` still establishes local-storage/state-only ownership, and current `updateAutoLaunch` no longer clears `gameActionError` when the preference is edited. The accepted clone-adapter rollback/report policy for a failed native mirror is otherwise unchanged.

Recovery ownership now matches the recovered `Gi` lifetime. `refreshStatus` polls runtime status, proxy status and local config only; it no longer reads or writes `game_recovery_status`. The selected-profile effect captures `profileId`/`closed`, registers `bridge://game-recovery`, performs the one initial `game_recovery_status` read, guards both acknowledgements, and closes/unsubscribes together on replacement. This matches the exact source effect, whose recurring five-second poll reads only status and game-setup/proxy state.

Normal shell composition now owns all four recovered profile flag stores in exact order: Weekend Shield, Attack Shield, Automatic Reconnection, Auto Close Popup. Weekend/Attack read their dedicated keys with legacy `auto_shield` fallback and default `true`; Reconnect/Close use their dedicated keys and default `false`. `ShellConfigSaveErrors` receives `[autoWeekendShieldStore, autoAttackShieldStore, autoReconnectStore, autoClosePopupStore]`, while `profileConfigDraft.js` preserves the boolean fallback and profile-owner checks. This closes the prior production-composition omission without changing the accepted Auto Reconnect draft lifetime.

The current whole-shell browser packet is bound to current `App.jsx` SHA-256 `E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`, the same hash recorded by `validation-results.json`. It records 124 assertions, 18 screenshots and zero console issues, including mounted `home-running-disconnected` cases in EN/light desktop and JA/dark narrow with Home under `profile-view-context`. The current Home source-render packet remains 42 cases / 34 exact structures; its eight differences are the declared disabled-attribute availability fences, with 24/24 exact-required pixel pairs passing and all 16 declared fence pairs changing as expected. `node src/LWBridge.UI-0.3.17/scripts/check-home-integration.mjs` also passes against the current source.

No Home regression was introduced by these fixes. The standing unavailable native lifecycle/provider controls remain the intentional source/local fences already declared by the milestone and are not a remaining Home correction.

**READY** — no concrete remaining Home-source fix or regression was found in this follow-up review.

# Milestone 5 — complete-app inherited proof and replay inventory

Research date: 2026-10-05 (SGT). The first sections preserve the pre-execution inventory/design record. The final-resolution section records the executed Milestone-5 packet after Milestone 4 was committed. Native/gameplay/updater actions remain fenced and historical packets remain immutable.

## Authority and integration boundary

The immutable recovered authority remains:

- `C:/Users/chimw/OneDrive/Desktop/Github/LW/lwbridge-0.3.17.exe` SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js` SHA-256 `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js` SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css` SHA-256 `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`.

The final complete-app gate must use the recovered route order from `src/LWBridge.UI-0.3.17/src/routes.js`: `overview`, `automation`, `map-data`, `march`, `city-layout`, `hotkeys`, `mini-games`, `settings`. The recovered retention contract is first-visit mount plus stable React `Activity` ownership for every visited page, with hidden layout/passive effects disconnected and reconnected once on return. The visited-route set remains App-owned across profile identity changes; the retained page subtree is keyed by selected profile identity and therefore remounts at that boundary.

Three route selections are deliberately hoisted into the App when multi-profile shell state is active: Automation category, Map data tab, and Squads tab. The final journey should therefore distinguish those App-owned selections from child-local draft/selection state. App-owned selections must keep the value the current recovered parent owns; a representative child-local draft/selection inside the keyed retained subtree must reset on profile identity change. This is the only consistent way to test both the recovered keyed reset and the separately recovered parent ownership without asserting contradictory ownership for the same state.

Native Last War actions, provider-backed success, updater/game lifecycle, protected original runtime, loaded native game assets, real server jumps, real scans, OS/global hotkeys, diagnostics export, and gameplay remain fenced. Final browser evidence may exercise local preview controls and source-valid error/disabled branches only.

## Accepted inherited packets and safe read-only replay

The commands below are classified by what they do now, not by what an older README called them. A command is listed as read-only only when it does not rewrite accepted evidence in its normal invocation.

| Surface / accepted packet | Safe read-only command now | Accepted/expected result | Hash-pin boundary and current replay status |
| --- | --- | --- | --- |
| City Layout, Hotkeys, Mini Games, Settings — parent Visual Final Units E–H | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-e/recover-pages.mjs --verify` | `city-layout 30 actual paired render states`; `hotkeys 18 actual paired render states`; `mini-games 78 actual paired render states`; `settings 63 actual paired render states` | **CURRENTLY GREEN.** `--verify` replaces result writes with byte comparisons. Current page pins remain City `C2C3E9445F607C82954A4319E19C6A8A1BE64368038293D7B1FADC8BE9372061`, Hotkey/Mini `5DAE6E4FFE8875BCEBC366109AD4C1E3FFF690EA17273EBB50226E9107551634`, Settings `8622BE39E06FE82FCF31BD2E092358987F3F304D447ADA061685727436737771`. Accepted page packet has 189 recovered/current render states total, 20 E–H browser pairs RGBA exact at zero tolerance/no masks, 144 mounted assertions, zero console warnings/errors. `validate-dom.mjs`, `mounted-pages.mjs`, `mutation-pages.mjs`, and `replay-accepted.mjs` write task outputs and are not immutable read-only replays. |
| Map data/Scheduled parent Unit B | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-b/validate-unit-b.mjs` | JSON marker `LWB317_VISUAL_FINAL_UNIT_B_VALIDATED`; `scheduledRenders: 10482`, `scheduledMutations: "51/51"`, `integratedRendererCases: 75`, `browserPairs: 16`, `pixelExactPairs: 13`, historical `acceptedDifferencePairs: 3`, `outsideAcceptedPixels: 0`, `mountedCases: 25` | **CURRENTLY GREEN AS HISTORICAL PACKET.** Unit B preserves its original three-difference classification. Final M5 pixel authority replays 15 unaffected pairs as 13 exact + only the two Start Scan provider fences and separately replays the superseding corrected City query-error proof as 3/3 unmasked exact pairs. |
| Shell retention — `LWB317-UI-SHELL-RETENTION-001` | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-RETENTION-001/milestone-c/validate-current.mjs` (do **not** add `--record`) | At its accepted checkpoint: `LWB317_SHELL_RETENTION_INTEGRITY_OK ... images=7 ... protected=7` | **HISTORICAL HASH FENCE; CURRENT TREE EXPECTED TO FAIL.** The immutable integrity manifest pins then-current `App.jsx`, `MapDataPage.jsx`, and `Pages.jsx`; authorized later work changed those files. The accepted packet still proves first-visit retention, keyed profile reset, Mini/Settings owner counts `1/0/1`, City keyboard/draft ownership, Equipment/Map retention, and zero saved browser console issues. Do not repin it. |
| Cross-server — `LWB317-UI-SHELL-CROSSSERVER-001` | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-CROSSSERVER-001/milestone-c/validate-current.mjs` | At its accepted checkpoint: `LWB317_CROSSSERVER_CURRENT_INTEGRITY_OK focused=12 regressions=3 screenshots=3 protected=7` | **HISTORICAL HASH FENCE; CURRENT TREE EXPECTED TO FAIL.** Validator hard-pins accepted `App.jsx` SHA `7FD95D74D66A762F19DC202E88944C0FD450155104BE33E80B4B3F1A26797F5C`. Reuse behavior contract: Escape is inert, outside pointer-down closes, event-time localization, no synthetic server jump success, and zero saved browser warnings/errors. |
| Map entry — `LWB317-UI-SHELL-MAP-ENTRY-001` | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-SHELL-MAP-ENTRY-001/milestone-c/validate-current.mjs` | At its accepted checkpoint: `LWB317_MAP_ENTRY_DELIVERY_INTEGRITY_OK hashes=6 checks=<saved-count>` | **HISTORICAL HASH FENCE; CURRENT TREE EXPECTED TO FAIL.** Validator hard-pins accepted `App.jsx` SHA `210842F74C49E0195C7684A7B1CFBEA24D22B516DF87C3082038559304836131`. Reuse the accepted behavior: Map summary request dispatches before route transition; direct initial Map bootstrap and active-Map re-click remain separate cases. |
| Squads full composition — Remaining-002 Milestone 2 | `Get-FileHash -Algorithm SHA256 src/LWBridge.UI-0.3.17/src/SquadsPage.jsx,src/LWBridge.UI-0.3.17/src/previewAfkCloseoutFixtures.js` | `SquadsPage.jsx = 16370281810045DF67CA9FF8E324BEE60E3C99DA1E7C493005D2B92E7769A4F4`; `previewAfkCloseoutFixtures.js = 11B5AE1D449B61296E5150DEA355A82AFE8BF0C23497583D840057AAEC8CF3DD` | **CURRENT HASHES MATCH ACCEPTED M2.** Stored `validation-results.json` marker is `LWB317_REMAINING_M2_VALIDATION_OK`: 15 exact source contracts, 70 browser assertions, 9 screenshots, console errors 0. `validate-milestone-2.mjs` unconditionally rewrites `validation-results.json`, so it is not a safe immutable replay. Exact recovered `I/pe/me/he` hashes remain recorded in the closeout. |
| Automation full composition — Remaining-002 Milestone 3 | `Get-FileHash -Algorithm SHA256 src/LWBridge.UI-0.3.17/src/AutomationPage.jsx,src/LWBridge.UI-0.3.17/src/previewAutomationContracts.js,src/LWBridge.UI-0.3.17/src/previewAutomationFixtures.js` | `AutomationPage.jsx = B05E6B3873DC86118379F8C7E1FC224DF3D4B0E8005AD7E48B5D659C6EDF98A5`; `previewAutomationContracts.js = FD4BC9C29F23631718D19818D273745C8D042DF35B1DDA2BE1E1928E75BB2295`; `previewAutomationFixtures.js = DF236FDBA38D02EB3FAA95C512A12F5FBA90CC6E43A48A892FC923DFE74AA56D` | **CURRENT HASHES MATCH ACCEPTED M3.** Stored marker `LWB317_REMAINING_M3_VALIDATION_OK`: 14 source contracts, 21 current contracts, 154 mounted-browser assertions, 23 screenshots, zero console/page errors, 56/56 exact recovered/current whole-category pairs. `validate-milestone-3.mjs` unconditionally rewrites `validation-results.json`, so it is not a safe immutable replay. |
| Legacy WIP archive | `node evidence/lwbridge-0.3.17/ui/LWB317-PENDING-WIP-CLOSEOUT-001/check-archive.mjs` | `LWB317_LEGACY_WIP_ARCHIVE_OK exactFiles=10` | **CURRENTLY GREEN.** This is the current safe archive-preservation replay. |
| Old Map Auto protected-WIP guard | `node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-CONFIG-001/check-protected-wip.mjs` | Historical marker: `LWB317_AUTO_CONFIG_PROTECTED_WIP_OK count=7` | **HISTORICAL HASH FENCE; CURRENT TREE EXPECTED TO FAIL.** It now stops on `previewAfkFixtures.js`, whose later authorized Squads work changed the old seven-file pin. Do not repin this historical guard and do not make it the final M5 acceptance guard; use the current archive guard plus task-local current hashes. |

### Exact M2/M3 recovered source pins

Milestone 2 recovered `SquadPanel-HC3-DJei.js` functions: `I` UTF-8 28070+22554 SHA `0A4895C21AD443AA7A1C05B54CFF2E8DD89F9038BED10DBFFFB9001A4128EE2E`; `pe` 4586+11256 SHA `4DD8A8770C7726AA3CCE0657BC88C6B0FB2146FA161BCF2C3017A231D0953C85`; `me` 15842+687 SHA `7232700BC71C76F9E5B0DA56A2DCC43D369191701C45CE28A87D5A966058DAB8`; `he` 16529+1957 SHA `37F600C70C0F2F1A2F5722DC33C6E62075A7976C86ECA48001E97F1FA39BC32B`. Its immutable failing baselines remain `52CE90805182ECC94B711EB375286D399A9F78CE133D891DCD6852959725F280` and `542A589A15A3E7D6AE9762D248C8AC0E04713F395604ABA7A06D487456B9911E`.

Milestone 3 recovered `AutomationPanel-BJ0gIqFh.js` SHA is `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`; exact `Ae` is UTF-8 21817+53453 SHA `61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68`. `AutomationCard-LCx_jIi7.js` SHA is `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61`; its exact card function SHA is `A3FA760F0D42AA4A927C8B6E810407723F2BA314360CD35AC05D3DF08F639657`.

### Older immutable validators that should remain stale

Two additional read-only validators correctly expose later-work boundaries and must not be “fixed” for M5:

- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-AFK-EDITOR-VISUAL-001/validate-read-only.mjs` is an accepted bounded AFK-editor packet (`renderer=24`, `pairs=12`, `masks=0`, `mounted=28`) but currently stops because its frozen dependency closure pins the pre-later-work `scripts/check-home-integration.mjs` size. M2 is the newer full Squads composition boundary.
- `node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/lead-checkpoint/validate-read-only.mjs` is an accepted parent takeover checkpoint but currently stops because its frozen campaign `continuation.md` length predates later campaign coordination. Its historical success covered 189 E–H DOM cases, 112 decoded pairs, Automation finite proof, and the old Unit J smoke. The newer task-local packets above supersede its current-tree pin without rewriting it.

## Prior current-App 8-route / 64-step smoke

`evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-FINAL-CAMPAIGN-001/unit-j/current-routes.json` is the prior current-only browser smoke. `unit-j/check-current-routes.mjs` drove four modes — EN/light 1280, JA/dark 1280, EN/dark 375, JA/light 375 — and for each mode clicked `routes + routes.reverse()`, yielding 16 route assertions per mode / 64 total, fonts loaded, correct locale/theme/viewport and zero console warnings/errors. Its historical stdout marker is `CURRENT_APP_ROUTE_SMOKE_OK routes=8 transitions=64 console=0`.

Do not use `unit-j/check-current-routes.mjs` as an immutable replay: it unconditionally rewrites `unit-j/current-routes.json`. Treat the saved result as historical evidence and reproduce its route sequence in the new M5 browser runner. The old smoke explicitly does not prove profile replacement, dialog boundaries, retained drafts, parent-owned selections, or original/source-local shell parity.

## Milestone 4 handoff boundary

Milestone 4 is committed and pushed at `1a7d863b536f8da67c2abc1b7dfbec483ec36d81`. Its closeout records `LWB317_REMAINING_M4_VALIDATION_OK`, App SHA `E4039BEE1A7F66712C797ABBEFC3DAAE4C7FB59C82939B90F4D0041F40B7A93C`, 124 current-browser assertions / 18 screenshots / zero console issues, plus the executed original-`Gi` versus current whole-shell source-render packet and independent READY reviews. M5 freezes this stable M4 App ancestry without rewriting the M4 evidence.

## Minimal final real-App browser journey matrix

The minimum complete-app browser proof is one retained multi-profile core journey plus three focused fixture journeys. This is smaller and stronger than opening one fresh context per assertion: it keeps the same real `App` alive while routes accumulate in `Activity`, then uses focused contexts only where one `previewState` cannot represent two incompatible source fixtures.

### J0 — retained multi-profile core journey

Start one real canonical App context with `previewPage=overview&previewState=shell-profiles`, Asia/Singapore timezone, local preview only, and no native provider. Use the real side nav, language selector, theme toggle, profile sidebar and route child controls. Instrument console warning/error and `pageerror` from context creation and require zero at the end.

Run these four mode phases in the **same context**, so the later phases exercise already-retained pages rather than fresh mounts:

| Phase | Required mode | Route drive | Extra assertions while routes are retained |
| --- | --- | --- | --- |
| J0-A | EN / light / desktop (1280×900 or the task standard desktop height) | Click source order then reverse: `overview → automation → map-data → march → city-layout → hotkeys → mini-games → settings → settings → mini-games → hotkeys → city-layout → march → map-data → automation → overview`. The first `overview` and middle repeated `settings` are same-route no-op assertions, matching Unit J’s 16-step shape. | On first visits choose non-default App-owned selections: Automation `trade`, Map `truck`, Squads `equipment`. In Map type a harmless child-local keyword such as `retained-profile-marker` without pressing Search; confirm it survives ordinary route hide/return. Confirm exactly one routed surface visible after every click; already-visited pages remain present under hidden Activity. Open profile note dialog, assert real focus ownership, close by recovered idle Escape. Open Cross-server popover, assert one offline source-owned block message, assert Escape is inert, close by outside pointer-down. |
| J0-B | JA / dark / desktop | With several/all pages already retained, change language through the real selector to `ja`, toggle theme to dark, wait until `html[lang=ja][data-theme=dark]` settles and the theme transition class clears; then repeat the same 16-step source-order + reverse circuit. | Assert retained page identity/state is not recreated merely by locale/theme change. The three parent-owned route selections still read `trade` / `truck` / `equipment`. Text exposed after the change is Japanese from current props. Fonts are loaded. |
| J0-C | EN / dark / narrow (375×1000, matching accepted page smoke) | Resize the same context/page to narrow, switch language live to EN while keeping dark, repeat the 16-step circuit. | Require no document horizontal overflow; keep exactly one visible routed page; verify the retained App-owned selections still hold. Capture at least one Map/Squads/Settings return so the narrow shell and retained content are visible together. |
| J0-D | JA / light / narrow (375×1000) | Switch live to JA and light while retained pages exist, repeat the same 16-step circuit. | Same no-overflow/one-visible/fonts-ready/parent-selection assertions. This completes 64 route assertions directly comparable to historical Unit J, but now inside one retained App lifetime. |

The profile boundary belongs inside J0, after the pages and parent-owned selectors have been visited. Expand the real profile sidebar if needed, select the uncached second local profile, observe `.profile-switch-loading` at least once, and wait for it to clear. Assert all eight route visitation entries remain available because visitation is App-owned. Return to Map and assert the child-local keyword `retained-profile-marker` has reset to its initial empty value because the keyed retained subtree remounted for the new profile. In the same observation, assert the three explicitly App-owned selector values remain whatever the parent owns (`trade`, `truck`, `equipment`) unless the final accepted M4 source packet demonstrates a different exact owner. Then select the first cached profile and the second cached profile again; both returns must bypass `.profile-switch-loading`. This proves uncached versus cached preview lifecycle and an observable keyed child reset without claiming the real native multi-profile provider.

J0 should also prove the recovered hidden-Activity owner rule with browser-observable counters/hooks in the task harness, without changing production. At minimum cover one interval owner (Mini Games or Settings) and one event/timer owner (City or Squads/Equipment): visible owner count `1`, hidden count `0`, returned count `1`, with no duplicate listener/timer after two leave/return cycles. The accepted shell-retention packet already establishes the intended `1/0/1` contract; M5’s purpose is to show the current real App still composes it after all later page work.

### J1 — Automation child-draft retention/reset

Use a real App context with an `automation-*` preview state that enables local controls; use a source-valid state already covered by M3 (for example the normal config state, not a synthetic success path). Enter Automation, select a non-default category and edit one clearly local draft field without invoking a native action. Navigate to another route and return: the local draft/selection must retain across ordinary route hiding, and its hidden Activity must own no duplicate active effect. Then use the profile-boundary part of J0 to prove the equivalent child-local draft does not leak into a different profile identity. Do not substitute the App-owned category itself for the reset assertion: its ownership is intentionally above the keyed page subtree in the multi-profile composition.

Reuse M3’s accepted ownership expectations where practical: independent Secret Task / Dispatch Assist stores, W-backed immediate-save families, Railway running disable, shared squad discovery retention, Trade fetch-error DOM and hidden-Activity completion. The final journey needs one representative retained draft, not a re-execution of all 154 M3 assertions.

### J2 — Map and Squads representative selection retention

Use two focused real-App states only because the `previewState` fixture namespace is exclusive:

1. **Map:** start a `map-*` source-valid fixture with populated rows. Select a tab/row or enter a filter/keyword that is page-local, leave Map, and return. Assert the parent-owned Map tab value and the chosen page-local state behave according to the accepted Map retention contract. If using Dispatch selection, assert the recovered tab-dependent reconnect reset rather than incorrectly demanding retention; the accepted shell packet explicitly records that Dispatch selection resets when that effect reconnects. Prefer a filter/keyword or Truck state for a simple positive retention assertion, and separately assert no duplicate polling/timer/listener ownership while hidden.
2. **Squads:** start a `squads-profile-*` or `squads-equipment-*` accepted M2 fixture. Change a local editor field / equipment selection / dirty state, leave Squads, return, and assert ordinary route retention. Toggle the parent-owned Squads tab and confirm it stays the App-owned selection in the multi-profile shell. No Garrison Run Now/stop/recall, Zombie provider action, or physical gameplay drag is required; DOM/local preview interaction is enough.

Together J1/J2 satisfy the requested representative Automation/Map/Squads draft/selection retention without multiplying the full accepted M2/M3 finite matrices.

### J3 — exit dialog source branches

The exit fixture shares the single `previewState` namespace with profile/page fixtures, so keep it as the final focused supplement:

- `previewState=shell-exit`: real `dialog.app-exit-backdrop` is open as a sibling after `main.app-shell`, has the recovered two actions, and idle Escape closes it. Underlying route DOM remains present rather than being replaced.
- `previewState=shell-exit-busy`: both actions are disabled and physical Escape remains prevented/open under the recovered busy predicate. No confirm-exit/native lifecycle callback is invoked.

The final accepted M4 whole-shell packet should remain the source/local visual authority for dialog ancestry/geometry; J3 is current real-App event integration only.

## Final runner assertions and evidence shape

The new M5 browser runner should record one machine-readable result packet and screenshots without modifying any accepted historical packet. It should pin the exact current source hashes it actually executed, including at least `App.jsx`, `Pages.jsx`, `AutomationPage.jsx`, `MapDataPage.jsx`, `SquadsPage.jsx`, `CityLayoutPage.jsx`, `HotkeyPages.jsx`, `SettingsPage.jsx`, route definitions, i18n catalogs used, and the M2/M3 preview contracts/fixtures. Its result must fail if those hashes differ between preparation and execution.

Required machine assertions:

1. Route source order is exactly the eight keys above; four mode phases each perform the same `routes + reverse(routes)` 16-step sequence, total `64` route assertions.
2. Every route step has one and only one visible routed page; first-visited pages remain in the retained tree while hidden; same-route clicks do not create a new page identity.
3. EN/light desktop, JA/dark desktop, EN/dark narrow and JA/light narrow metrics match the assigned viewport/theme/language; `document.fonts.status === "loaded"`; narrow phases have no document horizontal overflow.
4. Locale/theme changes happen in the live retained context and do not remount retained page identity solely to receive new props.
5. Multi-profile uncached selection visibly enters loading once; cached returns bypass loading; visited-route ownership survives profile identity change; keyed child-local state resets; parent-owned Automation/Map/Squads selectors follow their exact parent ownership.
6. Cross-server, profile-note and exit dialogs/popover obey their accepted close/focus/busy behavior without invoking provider/native success.
7. One representative Automation draft, one Map local selection/filter, one Squads local dirty/editor state, and one representative E–H local state survive ordinary route leave/return. City is the strongest E–H representative because accepted retention already proves edited layout state plus its keyboard/draft-timer owner; Mini/Settings provide the clean interval `1/0/1` owner check.
8. Hidden Activity effect owners are balanced: no duplicate timers/listeners/pollers; at least one accepted visible/hidden/returned `1/0/1` owner is measured in the actual current App.
9. `console.warning`, `console.error`, and `pageerror` collections are empty across every journey/context.
10. Every action clicked in the runner is local/presentation-safe. Buttons whose only meaningful result is native/gameplay/updater/provider-backed success remain disabled or unclicked.

Save screenshots at state boundaries that prove composition rather than every click: one representative return per mode, the retained-page locale/theme transition, uncached loading, cached profile return, Cross-server open, profile-note open, representative Automation/Map/Squads retained state, and exit idle/busy. Reuse the M4 whole-shell visual packet for exhaustive shell branch pixels and M2/M3/E–H packets for exhaustive page-local pixels; M5 screenshots exist to prove integration state, not to duplicate those finite campaigns.

## Final replay/check ordering for the coordinator

Before the new browser journey, record a clean current-source hash manifest. Then run the read-only inherited checks that are expected to remain current: Unit E–H `recover-pages.mjs --verify`, Unit B `validate-unit-b.mjs`, M2/M3 current file-hash checks, and `check-archive.mjs`. Run the historical shell-retention/Cross-server/Map-entry/AFK-editor/lead-checkpoint validators only as explicit **boundary probes** if desired; their current failures are expected at old hashes and must not be converted into new pins.

After the M5 browser packet is captured, run the repository’s canonical `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`, build, production-package check, and `git diff --check` as the final coordinator gate. Build/package commands produce build artifacts and therefore are intentionally not classified as immutable read-only replay commands in this inventory. Verify the browser packet’s source hashes still match after those checks.

The old seven-file Map Auto protected-WIP guard must not be used as a current acceptance condition: it currently fails on later authorized AFK fixture changes. The ten-file legacy archive guard is the current green immutable preservation check. If a new task-local WIP guard is required, create it in Milestone 5 from the current authorized ownership set rather than rewriting the old guard.

## Inventory conclusion

No additional page-local recovery campaign is indicated by the inherited evidence. E–H, M2 and M3 remain at their accepted current hashes; historical validators that stop on later authorized hashes remain immutable checkpoint boundaries.

## Final Milestone-5 resolution

The final task-local runner executes the current M4-stable App with a generated **67-file served-App dependency closure**, rather than the earlier hand-curated source subset. Its frozen browser result is `LWB317_REMAINING_M5_FULL_APP_CURRENT_OK`: **213 assertions, 11 screenshots, zero console/page errors**. It covers all eight routes with 64 source-order/reverse transition assertions in EN/light 1280×900, JA/dark 1280×900, EN/dark 375×1000 and JA/light 375×1000; all narrow modes have zero document horizontal overflow.

The integrated journeys prove retained DOM identity, live locale/theme propagation, uncached versus cached profile loading, App-owned Automation/Map/Squads selector retention, and keyed child reset. The Map profile boundary now starts from a non-default tab, keyword, Auto mode, quality filter, plunderable filter and profile-scoped 75-minute interval; after replacement the parent tab remains while keyword/mode/filter state returns to defaults and the replacement profile reads its own default 60-minute Auto interval. A separate populated Map journey proves 52-result pagination/page-2 retention across top-level route hiding, so the final packet covers both non-first-page lifetime behavior and the keyed profile reset boundary without inventing a combined native profile+Map fixture.

Map page-local authority is closed by the immutable Unit B finite matrix plus task-local read-only replay: 10,482 Scheduled renders, 51/51 mutations, 75 integrated cases, 25 mounted assertions; 15 historical browser pairs replay as **13 exact + 2 bounded Start Scan provider fences**, with zero pixels outside those two boxes; the superseded City query-error case is replaced by the later Unit A `CORRECTED_SEARCH_REJECTION_EXACT` replay, 3/3 unmasked exact pairs. `replay-map-entry-current.mjs` executes the final App SHA and proves summary dispatch before preload/transition, non-blocking rejection, inert active-Map re-click, unavailable-provider fencing, and retirement of an obsolete profile Map response in favor of the replacement profile owner.

The final manifest pins the reference executable/assets, all 67 served-App production dependencies, package/package-lock/Vite configuration, every evidence input consumed by the read-only validator, 11 decoded screenshots, and exact material tool versions. The final validator uses only task-local read-only pixel/runtime adapters and does not rewrite any accepted parent packet.

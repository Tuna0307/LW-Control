# Milestone-3 Automation independent review

## Result

**NOT READY for Milestone-3 commit yet.** The current coordinator edits close the baseline squad-discovery and error-ancestry mismatches, preserve the recovered `Activity` composition, and keep Weekly/Trade/history coverage fresh. The current validator now passes. However, exact `Ae` still exposes source-local store/save behavior that the current preview does not reproduce: `dispatchAssist` is a separate draft store in the source, and multiple controls that use recovered immediate helper `W(...)` still use the preview store's default 400 ms debounced edit. One recovered running conditional is also missing for Railway departure settings.

## Source identity / locators

- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js`
  - asset SHA-256: `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`
  - exact `function Ae`: UTF-8 byte offset `21817`, length `53453`, SHA-256 `61F181D014CFA82BD35CF3BBAE1DB3096CBE502826DDCD4680AD4C46C11A4B68`
  - readable recovery: `milestone-3/recovery/Ae.pretty.js`
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationCard-LCx_jIi7.js`
  - asset SHA-256: `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61`
  - exported card function `c`: UTF-8 byte offset `98`, length `3773`, SHA-256 `A3FA760F0D42AA4A927C8B6E810407723F2BA314360CD35AC05D3DF08F639657`

Current source hashes reviewed (also the hashes bound into `browser-current-results.json`):

- `AutomationPage.jsx`: `7E5BBF8387202302CF6D38E77CA05A5CC74488A6F3C0DBA1B9521FEF1E1897C9`
- `previewAutomationContracts.js`: `85DF26B5D1E6E48F90A26ABF2674539C368FE4E5C798A7CF5CBEDDA78A7B14CA`
- `previewAutomationFixtures.js`: `DF236FDBA38D02EB3FAA95C512A12F5FBA90CC6E43A48A892FC923DFE74AA56D`

## Closed Milestone-3 gaps

1. **Shared squad discovery is structurally corrected.** Exact `Ae` owns one discovered index array `Ut` (`Ae.pretty.js:64-74`) and feeds it into Resource Gather (`:223-225`), Alliance Gather (`:117`, `:321-342`), and Treasure (`:195-215`). Current `AutomationPage.jsx:584-585,612` creates one `availableSquads` provider and passes the same value to all three consumers; Gather/Treasure no longer hard-code `1..4`. The fresh browser packet proves the non-contiguous `[3,4]` case for Alliance Gather and Treasure and proves the Treasure priority row/drag handle.

2. **Treasure priority composition is corrected.** Exact `Ae` prepends selected squads, appends newly discovered squads, and makes selected rows draggable (`Ae.pretty.js:199-215`). Current `AutomationPage.jsx:292-300` now follows that composition and immediately flushes Treasure priority checkbox/reorder edits.

3. **General error ancestry is corrected.** Recovered `AutomationCard` function `c` places its `error` block before the config region. Exact `Ae` renders config-store errors before the panel title (`Ae.pretty.js:217`) and keeps Secret Assist's local validation message inside its settings (`:357-372`). Current generic/card validation is above `.automation-config` (`AutomationPage.jsx:383-396`), page-owned draft-store errors are above `PanelTitle` (`:593-596`), Resource Gather's local error is card-owned (`:448-463`), and Assist's validation copy remains inside the Assist section (`:316-317`). Trade correctly retains its separate source ancestry: its own save error is inside `.trade-station-panel` before the Trade card (`:512-514`), matching exact Trade component `pe`.

4. **Recovered `Activity` structure is preserved.** Exact `Ae` uses a visited-category set and `React.Activity`, including two distinct Daily `Activity` compositions (`Ae.pretty.js:217-225,237,343`). Current `AutomationPage.jsx:569-620` preserves the visited set and the two Daily groups. The fresh browser packet proves Trucks expansion survives a Daily -> Alliance -> Daily hide/show cycle.

5. **Weekly and Trade/history core paths are source-aligned.** Exact Weekly helper `zr` immediately calls `W(...)` on selection (`Ae.pretty.js:120-124`); current Trucks/Secret weekly selectors do edit(false)+flush (`AutomationPage.jsx:304,317`). Exact Trade component uses an immediate save helper for toggles/selections, Goods/Purchased tabs, retained goods across a failed refresh, and reverse-ordered day grouping; current `TradeStationCard` and `tradePurchaseHistory.js` match those behaviors. The fresh packet proves Weekly failed-save draft retention + Discard, Trade Goods/Purchased/history, loading/error/empty retained branches, and has 132/132 assertions with 0 console/page errors.

6. **Invented runtime-error alerts were removed.** The exact composition routes runtime status through card state; it does not inject a generic action-failed alert into every task card. Current source no longer does so, and the browser packet proves inactive `automation-runtime-error` cards remain source-disabled with no invented card alerts.

## Remaining source-local fixes

### 1. Secret Task incorrectly merges `dispatch` and `dispatchAssist` draft-store ownership

This is the largest remaining mismatch. Exact `Ae` constructs one `P[...]` store for every entry in `Ee`; `Ee` contains both `dispatch` and `dispatchAssist` (`Ae.pretty.js:4-20,119`). Assist writes explicitly target `P.dispatchAssist`: `Wr(...)` calls `W('dispatchAssist', ...)` and `Gr(...)` edits `P.dispatchAssist` (`:146-159`). The global error renderer also gives `dispatchAssist` its own `automation.dispatchAssist` label (`:217`).

Current `AutomationCard` creates only one `usePreviewConfig` for the card title (`AutomationPage.jsx:343-366`), and `initialAutomationDraft('Secret Task')` places dispatch and Assist fields into that same draft (`previewAutomationContracts.js:14`). Assist toggle, qualities, delay and interval then edit the Secret Task card's `config.store` (`AutomationPage.jsx:306-317`). Consequences are source-visible: Assist and dispatch share one dirty/saving/error lifecycle, Assist invalid/save failure would surface under the **Secret Task** global store rather than a distinct **Dispatch Assist** store, and Retry/Discard cannot independently reconcile dispatch versus Assist edits. This must be split before claiming source save/concurrency parity.

### 2. Recovered immediate-save semantics are still incomplete

Recovered helper `W(task, patch)` is explicitly `edit(..., false)` followed by `flush()` (`Ae.pretty.js:57-59`). By contrast, current helpers `fieldState`, `field`, and `check` call `config.store.edit(...)` with the default delay (`AutomationPage.jsx:212-215`), and `createConfigDraft` defaults that delay to 400 ms (`previewConfig.js:16-20`). Chat immediate controls and Weekly are now corrected, but several exact `W(...)` controls still use the delayed path.

Concrete remaining examples:

- Construction target toggle, building-type selection, auto-claim: source `Ae.pretty.js:239-250`; current `AutomationPage.jsx:218-284` uses `fieldState` / `check`.
- Official-position select: source `:257-260`; current uses the generic draft path.
- Alliance Train selection modes, fixed carriages, reward priority/checkboxes, VIP, reward quantity, thanks mode/ticket count: source `:260-300`; current `AutomationPage.jsx:322-326` uses `fieldState` / `check` / `field`.
- Alliance Gather priority reorder and checkbox selection: source `:321-342` uses `W('allianceGather', ...)`; current `AutomationPage.jsx:335-338` uses default `config.store.edit(...)`.
- Railway enabled toggle and `departWhenTicketsInsufficient`: source `:343-350` uses `W('railway', ...)`; current header toggle uses the generic delayed card store and the departure checkbox uses `check(...)` (`AutomationPage.jsx:355-365,304`).
- Secret `autoExecute`, `collectRewards`, Assist toggle and Assist quality changes: source `:350-360` uses immediate `W(...)` through dispatch/Assist-specific helpers; current `AutomationPage.jsx:317,355-365` is delayed (and additionally affected by the merged-store issue above).
- Ghost `autoStartOwn`, join/filter/claim: source `:372-380` uses `W('ghostRecon', ...)`; current `AutomationPage.jsx:320,355-365` uses delayed generic edits.
- Resource-claim enable toggles are immediate in source helper `Br` (`:126-128`); current resource cards use the generic delayed header toggle.

These timing differences are material for the Milestone-3 requirement because they change when `saving` appears, which draft is captured if the user immediately makes another edit, and which value Retry/Discard reconciles after a failure.

### 3. Railway running composition is incomplete

Exact Railway Weekly selects are disabled while `X.running`, and `departWhenTicketsInsufficient` is also disabled while `X.running` (`Ae.pretty.js:343-350`). Current Weekly passes `running={previewState === 'automation-runtime-running'}` and disables correctly, but the departure checkbox is only `disabled={!enabled}` (`AutomationPage.jsx:304`). Therefore the current `automation-runtime-running` composition permits a source-disabled control.

The evidence also does not exercise recovered `AutomationCard` `actionBusy` / `configDisabled` overlap. Exact card function `c` lets busy state drive the visible running state and disables the config surface while the caller's task is busy. The current generic action buttons are intentionally fenced/inert, but there is no source-valid busy fixture proving the non-native settings disable/state composition. Keep that as an evidence gap even if native actions remain fenced.

### 4. Squad empty-transition retention is not proved

Exact discovery only replaces `Ut` when the returned squad list is non-empty (`Ae.pretty.js:64-74`), so a later empty discovery does not erase a previously discovered non-empty set. `previewAutomationAvailableSquads()` is stateless and returns `[]` for `automation-squads-empty` (`previewAutomationFixtures.js:8-15`). A standalone initial-empty fixture is source-valid, so this is not evidence that the current visible empty state is wrong; however, the current fixture model does not prove the exact non-empty -> empty retention behavior. The non-contiguous browser case also asserts Alliance Gather and Treasure, while Resource Gather's use of the shared provider is currently only source/static evidence.

### 5. Trade fetch-error DOM has one minor source mismatch

Exact Trade `pe` renders its asynchronous goods-fetch failure as `<div className="automation-error">` without `role="alert"`. Current `AutomationPage.jsx:557` adds `role="alert"`. The content/location is otherwise correct and this is not a visual blocker by itself, but it is an exact DOM/accessibility mismatch if Milestone-3 claims source-exact composition.

## Invalid / saving / error / running overlap assessment

- **Invalid:** construction/treatment/official/resource interval/chat/daily-delay card error ownership now matches the recovered card placement. Assist local invalid copy is placed correctly, but its store-level invalid/error ownership is still wrong because Assist is merged into the dispatch store.
- **Saving/error:** current page aggregation uses `error > saving` (`AutomationPage.jsx:580-592`), matching recovered `Ae` precedence (`Ae.pretty.js:119`). Generic and Resource Gather save errors are now above the title. Trade correctly owns its save error inside the Trade panel. The browser proves real Retry/Discard for one generic save and Weekly. It does **not** prove two independent stores editing/saving concurrently or an error in one store while another is saving; the merged Assist store prevents exact dispatch/Assist coverage here.
- **Running:** invented runtime alerts are gone and Weekly running-disable is covered. Railway's departure checkbox remains wrong while running, and generic `actionBusy/configDisabled` composition is not covered.
- **Activity retention:** structure is source-correct and expansion retention is browser-proved. Dirty draft retention through a category hide/show, or a save/error completing while its category is hidden, is not directly asserted by the current browser packet. This should remain an evidence gap rather than be promoted to a proved regression without a source-valid transition test.

## Validator assessment

The earlier failure `Ae feeds discovered squads to Treasure` was a **bad source locator**, not a product mismatch. It matched prettifier-local names (`u2`/`d2`) against raw minified `Ae`, whose exact bytes use `u`/`d` around the Treasure list. During this review the validator was corrected to test the raw form:

`dispatchSquads??[],ee=new Set(u),d=[...u,...Ut.filter`

After that correction, `node evidence/lwbridge-0.3.17/ui/LWB317-UI-VISUAL-REMAINING-002/milestone-3/validate-milestone-3.mjs` passes with `LWB317_REMAINING_M3_VALIDATION_OK`. The pass is reproducible against the current three source hashes above. The gate is still incomplete as an acceptance gate because its 15 current static checks do not assert separate `dispatchAssist` store ownership, the full recovered immediate-save matrix, or Railway departure's running disable.

## Validation performed

- Read exact raw/recovered `Ae` and exact raw `AutomationCard` function `c`; recorded identities above.
- Read current uncommitted `AutomationPage.jsx`, `previewAutomationContracts.js`, `previewAutomationFixtures.js`, draft-store engine, Trade history helper, Milestone-3 baseline/validator/browser runner/results.
- Reproduced the original validator failure, pinned it to the raw-vs-pretty local-name locator, then reran after the coordinator corrected that locator: PASS.
- Verified `browser-current-results.json` is fresh for all three current production source hashes: 132 assertions, 21 screenshots, 0 console/page errors. Browser/processes were not controlled or rerun by this worker.

## Blockers

Before Milestone-3 commit, fix or explicitly fence with source-backed rationale:

1. separate `dispatchAssist` draft/error/Retry/Discard ownership from `dispatch`;
2. restore immediate flush semantics for recovered `W(...)` controls (at minimum the concrete groups above);
3. disable Railway departure-insufficient control while Railway is running;
4. add focused assertions for the above, plus dirty Activity retention/concurrent store overlap if Milestone-3 is to claim those requirements as proved.

## Follow-up review — 2026-10-05

**READY.** Re-review of the coordinator fixes closes every concrete blocker from the independent review above, with no new source-local regression found in the inspected Automation changes.

The separate `dispatchAssist` store is now source-correct: `AutomationCard` creates a distinct `Dispatch Assist` draft store, reports it independently to the page-level error owner, and the refreshed browser packet proves a failed Assist save receives the Dispatch Assist label, exposes its own Retry/Discard controls, clears independently on Discard, and leaves the simultaneous Secret Task draft intact.

The recovered immediate-save matrix is corrected for the prior `W(...)` groups. Construction target/building/auto-claim, Official Position, Alliance Train controls, Alliance Gather ordering/selection, Railway/Secret/Ghost immediate controls, chat/Treasure priority, and the applicable resource-claim header toggles now take the immediate edit+flush path; Assist toggle/qualities use their separate immediate store, while Assist delay/interval retain the recovered edit/pause + blur-flush behavior.

Railway running parity is closed: the current runtime-running fixture now starts Railway enabled, disables all Weekly selectors and `departWhenTicketsInsufficient`, and keeps the fenced action showing the recovered Stop label.

Activity save/error retention is now directly proved. The browser packet verifies a dirty Trucks draft survives a category hide/show cycle, and a save failure that completes while Daily is hidden remains page-owned while the failed draft value survives both hidden state and Activity restoration.

Shared squad discovery/retention is closed. The current provider retains its last non-empty discovery when a later discovery is empty, matching `Ae`; the browser packet proves the non-contiguous provider reaches Alliance Gather, Resource Gather, and Treasure, then proves the prior Alliance/Treasure set survives an empty rediscovery.

Trade fetch-error DOM is corrected to the recovered non-alert `.automation-error` container and is explicitly asserted in the refreshed browser matrix.

Validation is fresh for the current reviewed source hashes: `AutomationPage.jsx` `B05E6B3873DC86118379F8C7E1FC224DF3D4B0E8005AD7E48B5D659C6EDF98A5`, `previewAutomationContracts.js` `FD4BC9C29F23631718D19818D273745C8D042DF35B1DDA2BE1E1928E75BB2295`, and `previewAutomationFixtures.js` `DF236FDBA38D02EB3FAA95C512A12F5FBA90CC6E43A48A892FC923DFE74AA56D`. The current browser packet reports 154/154 passing assertions, 23 screenshots, and 0 console/page errors. The source-render packet is current for `AutomationPage.jsx` and reports 56/56 exact pixel pairs, 0 changed pixels, and 0 console issues. `src/LWBridge.UI-0.3.17/scripts/check-ui-drafts.mjs` also passes its concurrent-edit, failure/Retry/Discard, invalid-dispatch, and Automation validation checks.

**Remaining fixes: none from this Milestone-3 independent review.**

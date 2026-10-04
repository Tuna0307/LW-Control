# Parent-owned category/tab audit

Status: **EXACT_CONTRACT — CHANGES_REQUIRED** for the pure frontend multi-profile selection state. No product files were changed in this audit.

## Concrete mismatch

The original shell keeps Automation category, Map result tab, and Squad subtab in `Gi` outside its profile-keyed subtree. In multi-profile mode it supplies the selection and setter to each panel. The canonical App currently supplies neither; its `selectedProfileId` key remounts the panels, which then initialize Daily / City / AFK. Therefore selecting Trade / Train / Equipment on local profile A and selecting local profile B resets three visible selections that the original retains.

This correction is independently possible with local shell/profile state. It does not require original-service entitlement or a native profile/game provider. The clone intentionally uses its local `showProfiles` capability as the multi-profile gate.

## Exact recovered contracts

All offsets are UTF-8 byte offsets, backed by exact slice and full-asset hashes in `source-contract.json`.

| Contract | Asset | Byte |
| --- | --- | --- |
| Parent Automation state `[Ge,P]=useState("daily")` | index-BVfnK1wp.js | 363161 |
| Parent Map state `[Ke,qe]=useState("city")` | index-BVfnK1wp.js | 363192 |
| Parent Squad state `[Je,Ye]=useState(hi)` | index-BVfnK1wp.js | 363223 |
| Squad choices `mi=["afk","equipment"]`, default `hi=mi[0]` | index-BVfnK1wp.js | 358459 / 358482 |
| Original multi-profile gate `rt=se(r.entitlement)` | index-BVfnK1wp.js | 363306 |
| Parent Automation controlled binding | index-BVfnK1wp.js | 373777 |
| Parent Map controlled binding | index-BVfnK1wp.js | 374455 |
| Parent Squad controlled binding | index-BVfnK1wp.js | 374732 |
| Profile subtree key `r.selectedProfileId` | index-BVfnK1wp.js | 372764 (call slice) |
| Automation local default / `B=t??at` / visited initialization | AutomationPanel-BJ0gIqFh.js | 25370 / 25402 / 25549 |
| Automation click: visited Set first, parent callback if present, otherwise local setter | AutomationPanel-BJ0gIqFh.js | 42780 |
| Squad local default / visited initialization / `C=e??_` | SquadPanel-HC3-DJei.js | 191517 / 191541 / 191602 |
| Squad click: visited Set first, parent callback if present, otherwise local setter | SquadPanel-HC3-DJei.js | 192276 |
| Map local default / `L=r??$e` | MapDataPanel-B4GXEND2.js | 30396 / 30426 |
| Map tab change: active no-op; generations/cache; parent callback or local setter; page/rows/total/loading/message | MapDataPanel-B4GXEND2.js | 39238 |

Babel binding resolution confirms each of the six parent state/setter bindings has exactly one reference, in its child prop binding. There is no profile-change reset of these parent selections. The original single-profile bindings pass `undefined`, preserving each panel's own local behavior. The Map Manual/Auto scan selector is a different local state and is not part of this parent-owned selection contract.

## Bounded correction plan

1. Add three App-owned states with original defaults Daily / City / AFK. Keep them outside `RetainedPages` and do not reset them on selected-profile changes.
2. Pass each route its original controlled prop pair only when `showProfiles` is true: Automation `activeCategory/onActiveCategoryChange`; Map and Squad each their own `activeTab/onActiveTabChange`. Separate the Map and Squad pairs in the route dispatcher; a common undifferentiated `activeTab` in `pageProps` cannot represent both states.
3. Each panel uses `providedSelection ?? localSelection`, with callback presence choosing parent setter versus local setter. Preserve the current single-profile preview initializers. Do not add a synchronization effect that resets the parent or writes the local fallback during controlled clicks; the source has neither.
4. Initialize the Squad visited Set from the effective initial tab; otherwise a profile remount controlled to Equipment can show no Equipment content. Original Automation likewise initializes its visited Set from the effective category. The existing broader Automation category-content retention structure is a separate source-lifetime concern; retain existing accepted handlers while separately verifying any intentional lifetime correction.
5. Keep Map's accepted cache/request-retirement/change-tab implementation. Replace only selection ownership at its existing setter point, after cache/generation work and before page/rows/total/loading/message setters. Keep active-tab no-op and all existing search/query effects. A new profile mount must have fresh row/cache state even though its parent tab selection persists.
6. Keep profile-key remounts, local configuration ownership, Activity boundaries, lazy module identity, motion integration, native fences, and the existing local profile fixture authorization unchanged. Implement after the loading/motion checkpoint to avoid simultaneous Squad ownership.

## Finite acceptance cases

- Multi-profile initial mount shows Daily / City / AFK.
- Select Trade / Train / Equipment; local profile A → B → A preserves all three choices independently. Changing one must not change either other selection.
- Re-keying clears child rows/drafts/cache/visited history as applicable; this change preserves only the three parent selections.
- Controlled remount to Equipment initializes visited content with Equipment. Controlled remount to Trade uses Trade as the effective initial category.
- Single-profile behavior stays uncontrolled, including existing explicit preview defaults. Profile-key remount with undefined props uses local defaults.
- Exact nullish precedence: `null`/`undefined` use local state; an explicitly supplied value uses that value without invented validation.
- Automation/Squad visit tracking runs before parent/local notification. Controlled clicks notify only parent; uncontrolled clicks notify only local state.
- Map active tab click is a no-op; normal/scheduled tab cache boundary and setter order stay green. Map Manual/Auto state remains separate.
- Route lazy/import/Suspense and Equipment/AFK motion/effect cleanup checks remain green. No native/game operation is required for acceptance.

## Executed proof and limits

`node .../parent-tab-audit/check-source-cases.mjs` reports `LWB317_PARENT_TAB_SOURCE_CASES_OK`, **26/26** exact source expression/callback cases, including **3** distinguishing current-baseline remount mismatches. This executes exact original selector/click bodies with inert state setters, verifies source hashes/slices, and extracts actual canonical prop declarations/state initializers/App key/prop inventory. The Map callback proof uses an inert cache helper only to observe notification ordering; accepted real cache correctness remains owned by the maintained Map regressions.

`audit-baseline.json` pins current App/module hashes and exact relevant declarations before the correction. `source-cases.json` records the individual outcomes. These are source/scalar callback proofs, not mounted React profile-switch proof, original protected-runtime observation, native profile support, live persistence, or pixel parity. The correction must add actual mounted/browser local-profile proof; preserve this audit baseline.

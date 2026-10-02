# Independent review: LWB317-REVIEW-MAP-FILTERS-001

Date: 2026-10-02
Branch: `research/offline-controller`
Implementation reviewed: `3d2f6ba3ad99ef8386b784de972db8c2f1461fe4`
Production baseline: `c7c3a3271c6b3b433a2295e79b56feb074b24f67`

## Disposition

`REVIEW_COMPLETE — ACCEPT`

For this precise work-item scope, I found no implementation issue requiring a product correction. The production changes reproduce the recovered original behavior for per-tab Map filters/sorts, exact Secret Task level querying, and the explicit offline Treasure Checking preview fence.

This acceptance is limited to the six behaviors assigned here. It does not establish full Map parity, live/native Treasure refresh parity, or pixel parity, and it does not change project-lead acceptance state.

## Findings

No in-scope defects found.

### 1. Applicable Map filter choices are independently owned per tab

Recovered original locator: `MapDataPanel-B4GXEND2.js`, UTF-8 bytes approximately `30908-31016`, `52663`, `53247`, `53914`, and `54210`.

The original creates separate object states for quality (`It/Lt`), retained goods (`Rt/zt`), completion status (`Ht/Ut`), and plunderable-only (`Wt/Gt`). The actual callbacks update only `[L]`, the current kind. The reviewed production component now does the same through `qualityByKind`, `itemKeyByKind`, `completionStatusByKind`, and `plunderableOnlyByKind`.

Independent browser reproduction: Truck was set to Reindeer plus plunderable-only. Train then showed its own blank quality and unchecked plunderable-only state; returning to Truck restored the Truck choices. Secret Task's Special quality also remained independent from Truck's Reindeer choice.

Impact: the reviewed implementation preserves the original tab-local ownership rather than sharing hidden filter state across applicable kinds.

### 2. Tab switching does not leak those choices into another tab's query or table presentation

Recovered original locator: query builder around UTF-8 bytes `37497`, `37940-38120`; table call around `56776`.

The original looks up quality/completion/item/plunderable values by the active kind and passes the active kind's sort state to the table. Production derives its active scalar values from the corresponding per-kind object and includes only those active values in the query. The active `itemKey` is likewise passed to `MapTable`, so retained-goods column sortability follows only the visible Truck/Train tab.

Actual-code distinction: the reviewed effect constructs the query from the active-tab derived values and its dependency list tracks those values. Browser switching confirmed the visible controls restore per-tab state rather than carrying the previous tab's selections over.

Impact: no in-scope query or presentation leakage found.

### 3. Clearing Truck retained goods removes only Truck item-count sorting

Recovered original locator: UTF-8 byte approximately `53914`.

Original callback:

```text
zt(current => ({...current,[L]:value||undefined}))
... Yt(current => ({...current,[L]:current[L].filter(sort => sort.sortBy !== `itemCount`)}))
```

Production `changeItemFilter` has the same current-tab update shape. It writes only `itemKeyByKind[tab]`, and on clear rewrites only `sortsByKind[tab]` after removing `itemCount`. `mapTablePresentation.js` makes the Truck/Train retained-goods column sortable by `itemCount` only while that tab's `itemKey` is present.

The supplied populated browser flow additionally shows Train retaining its own `fixture-supply` choice and `Items1` sort after Truck is cleared. I treated that as corroboration after independently checking the original callback and production callback rather than as primary proof.

Impact: Truck clear cannot remove Train's retained-goods selection or Train sort entry through this callback.

### 4. Secret Task selects an exact level with matching minimum and maximum

Recovered original locator: UTF-8 bytes around `38120`.

The original query writes:

```text
minLevel: n===`dispatch`&&Kt ? Number(Kt) : void 0
maxLevel: n===`dispatch`&&Kt ? Number(Kt) : void 0
```

Production now sets both `query.minLevel` and `query.maxLevel` to `Number(minLevel)` for Dispatch/Secret Task. This is an exact-level query and matches the recovered original.

Impact: selecting level 6 requests level 6 only rather than level 6 and above.

### 5. Explicit offline Treasure Checking reproduces missing-state Checking while preserving known states and blocking precedence

Recovered original locators: Treasure column formatter around UTF-8 bytes `13020-13187`; player-state precedence helper around `5025`; original table receives refreshing flag around `56776`.

The original substitutes `verifying` only when the corresponding world/player state is missing and the refreshing flag is true. The player helper checks `claimed`, then `other_alliance`, then no-scout/no-squad/squad-reserved reasons before its ordinary state mapping.

Independent production-module reproduction using `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-FILTERS-001/independent-cases.mjs` produced:

```text
missing world  -> map.treasureStateVerifying
missing player -> map.treasurePlayerVerifying
missing + other_alliance -> map.treasurePlayerOtherAlliance
known claimable -> map.treasureStateClaimable
known claimed   -> map.treasurePlayerClaimed
```

The special preview fixture removes states only from selected synthetic Treasure rows and leaves other rows' known states intact. A row with missing state plus `other_alliance` therefore demonstrates the original blocking-reason precedence rather than masking it with Checking.

Impact: the offline preview covers the previously unavailable Checking presentation without changing known-state semantics.

### 6. Checking inputs are fenced from native modes, other fixtures, and other tabs

Production provider creation requires `bridgeMode === "preview"` and a `map-` preview state. Only exact `map-treasure-checking` uses `treasureCheckingFixtureRows` and sets `previewTreasureStatesRefreshing: true`. `MapDataPage` passes the flag to the table only when all of these are true: preview fixture, exact preview state, active `treasure` tab, and provider flag.

Independent direct cases verified:

```text
map-table-states refreshing flag -> false
native + map-treasure-checking -> no preview provider
native-unavailable + map-treasure-checking -> no preview provider
preview + non-map state -> no Map preview provider
```

Impact: the synthetic Checking input does not bleed into native/native-unavailable operation, another preview fixture, or a non-Treasure tab.

## Verification performed

Reference executable SHA-256 independently matched the expected value:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

The implementation-vs-baseline production diff contains only `src/LWBridge.UI-0.3.17/src/MapDataPage.jsx` and `src/LWBridge.UI-0.3.17/src/mapPreviewApi.js` for this assignment.

Requested author evidence scripts all passed read-only:

- `check-filters.mjs`
- `check-treasure-checking.mjs`
- `check-table-regression.mjs`
- `validate-evidence.mjs`

Canonical UI checks all passed:

- `npm.cmd run check`
- `npm.cmd run build`
- `npm.cmd run check:production-build`

The build/package verification reported production fingerprints:

```text
b09ef2120b8f67b5de0f9430a814ae0c9d33e50a5e0ba267597063c70c10f7eb
b56793f5b4a7934be8e56032ca447c87ee0f81eb3f430e9def803dd015119d34
```

Browser console capture contained no errors or warnings from the reviewed preview flow; only Vite connection debug messages and the normal React DevTools development info message were present.

Focused independent evidence is in `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-FILTERS-001/`.

## Broader pre-existing gaps and limits

- The Checking fixture is synthetic and offline. Automatic live Treasure refresh/native parity remains outside this assignment.
- Existing host handlers are not evidence that live native Treasure state refresh works.
- Fresh preview URLs can require entering Map from the shell before the fixture view is visible. This routing behavior predates and is outside the reviewed two-file production diff.
- In my independent `map-treasure-checking` browser session, the shell retained an empty Map summary (`serverId: -`), so that pass did not produce populated Checking rows. I verified Checking semantics/fences directly through the exported production provider/table code and visually inspected the supplied populated Checking screenshots. This limitation does not establish a product defect in the reviewed diff.
- The original application's post-auth visual/pixel comparison remains blocked at authentication. No full Map/UI/pixel-parity claim is made here.
- Other open project gaps documented outside this work-item remain unaffected.

## Review boundary

No product files were edited. The pre-existing unstaged `src/LWBridge.UI-0.3.17/src/previewAfkFixtures.js`, `.scratch-lwb317/`, and `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-003/screenshots/` were left unchanged and unstaged.

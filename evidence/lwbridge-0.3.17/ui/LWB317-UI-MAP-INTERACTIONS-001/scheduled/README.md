# Scheduled Plunder tab presentation (Milestone C, subagent 1)

Campaign: LWB317-UI-MAP-INTERACTIONS-001. Scope: presentation only. No scheduling, cancel, share or claim is wired, no
desktop command is added, no gameplay success is faked. Status of everything below: `EXACT_BYTES` for the recovered
original code, `IMPLEMENTED_NOT_VALIDATED` for the canonical port (source-level differential only, no pixels, no live game).

## Files

| File | Role |
| --- | --- |
| `src/LWBridge.UI-0.3.17/src/ScheduledPlunder.jsx` | `ScheduledPlunder`, `SecretPlunderGroup` (original `ot`), `TruckPlunderGroup` (original `st`), `scheduledPlunderCount` |
| `src/LWBridge.UI-0.3.17/src/mapPlunderPresentation.js` | Pure helpers: status/result text, error translators, group filters, button predicates |
| `src/LWBridge.UI-0.3.17/src/mapPlunderFixtures.js` | `plunderFixtureFor(previewState, now)`, `plunderFixtureGameTexts` (offline synthetic rows) |
| `check-scheduled-plunder.mjs` | Differential check (`--record` writes `results.json`, `--verify-record` compares) |
| `results.json` | Recorded counts, locators, coverage, mutation list, CSS and key presence |

Run (about 4 minutes, deterministic, exits non-zero on any failure):

```
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-INTERACTIONS-001/scheduled/check-scheduled-plunder.mjs [--record|--verify-record]
```

## Recovered facts and locators

Source: `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`
(SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`). Offsets are UTF-8 byte offsets into the
original asset; line numbers are in `../reference/MapDataPanel.pretty.js` (layout-only re-print).

| Fact | Locator |
| --- | --- |
| Dispatch/Ghost group component `ot`: columns Server, Owner, Quality, Rewards, Completion Time, Plunder At (`map.plunderAt`), Result (`map.plunderResult`), Actions; widths `[78,150,92,280,135,135,180,74]+8` | offset 20712 (3679 bytes), pretty 263-282 |
| Truck group component `st`: columns Server, Player/Alliance, Quality, Times Plundered, Result, Loot Obtained (`map.plunderRewards`), Actions; widths `[78,150,86,110,200,280,92]+8` | offset 24391 (3841 bytes), pretty 283-292 |
| Secret status resolver (inner `f`): succeeded, failed (E000000 / game text / `we`), cancelled, expired, running, waiting (`!online` or waiting_connection), else task state ready/protected/pending | inside `ot`, pretty 265-274 |
| Truck result chain (inner `re2`/`ne2`, `ie2`, `ae2`, `ee2`) | inside `st`, pretty 286 |
| Dispatch error table `b` + `x`/`Ce`/`we` | offsets 996, 1925, 2099, 2192; pretty 24-46 |
| Truck error table `Te` + `S`/`C` | offsets 2671, 3189, 3363; pretty 47-61 |
| Task timing helper `F` (= `mapTaskState`), `P` (= `mapTimestamp`), `N` (date), `I` (number), `L` (quality), `Je` (duration), `M` (reward name) | offsets 8753, 8666, 7885, 9191, 9273, 8022, 7558 |
| Truck helpers used by `st` as `l` / `r` = `Fe` / `Ie` of `index-BVfnK1wp.js` (hash `44C4E404...5524C6`; MapDataPanel import `St as l`, `Ct as r`, checked against the export table) | offsets 201275, 201399 |
| Reward count helper `_` = rewardDisplay `e` (hash recorded in `results.json`) | rewardDisplay offset 0 |
| Main component renders Dispatch group `ln.filter(e => e.taskKind !== "ghost")`, Ghost group (`kind:"ghost"`, `=== "ghost"`), Truck group `dn`, in that order, only when tab is `scheduledPlunder` | offset 57146 (filters), pretty 709/735 |
| Tab count `ln.length + dn.length` (all dispatch+ghost rows plus all truck rows), also the `common.itemCount` of the scheduled search bar | offsets 50824 and 56491, pretty 709 and 735 |
| Cancel busy keys: Dispatch/Ghost `${serverId}:${uuid}` (`hr`), Truck `truck:${serverId}:${uuid}` (`gr`, `_r`); `hr` sends `ghost:${uuid}` for ghost rows; clear marker `clear:${kind}` | pretty 623-666 |
| Refresh: list loaded on mount, on `bridge://dispatch-plunder-changed`, `bridge://truck-plunder-changed`, and every time the tab becomes `scheduledPlunder`; 1 s `Date.now()` ticker feeds `currentTime` while tab is dispatch, ghost, truck or scheduledPlunder (or a scan is reading) | pretty 361-373 |
| Extra game text keys the original loads for this tab: `lastError` matching `/^(?:\d+\|dispatch_des\d+\|ghostrecon_\d+)$/` of dispatch rows, `rewards[].nameKey`, truck `plunderRewards[].nameKey` | pretty 416-448 |
| Natives named by the original (not called here, not recreated): `map_plunder_jobs_list`, `map_dispatch_plunder_cancel/clear/schedule`, `map_truck_plunder_cancel/clear/schedule` | index asset offsets in `results.json` |

Behaviour worth knowing (all reproduced, all verified by the differential):

* A `scheduled` Dispatch/Ghost row whose timing state is `expired` or `full` is labelled `map.taskPending` (the resolver only
  maps ready and protected explicitly).
* `gameTexts[lastError]` replaces the translated error for Dispatch/Ghost rows except for `E000000`; Truck rows never consult `gameTexts` for errors.
* A busyKey of `clear:*` / `schedule*` disables every Clear button but leaves Cancel / Plunder Again enabled (they only compare their own key).
* Plunder Again (Truck) needs `online`, `scheduleStatus === "succeeded"`, truck state not invalid/full/expired, and no other row for the same `serverId:uuid` in scheduled/waiting_connection/running (`ee2`).
* `Te` is an ordinary object: a `lastError` such as `constructor` resolves through the prototype in the original; the canonical table is the same plain object on purpose.
* Dispatch `we` has a branch (`code === DISPATCH_PLUNDER_SERVER_REJECTED ? compact(remainder||text) : detail`) that cannot produce a different value than `detail` (equivalent code path), so no mutation of it is expected to be detectable.

## Differential method

`check-scheduled-plunder.mjs` parses the original asset with Babel, extracts the real `ot`, `st`, `we`, `C`, `F`, `Fe`, `Ie`,
`_` and helpers, and runs them with real `react`, `react/jsx-runtime` and `react-dom/server`. Stubs: `u` (`useI18n`) returns
`{language, t}` over the real recovered locale catalogs; `v` (`GameAssetImage`) emits the canonical placeholder. The canonical
sources are bundled in memory with esbuild (the real `i18n.jsx` is replaced by an equivalent `useI18n` stub because the real
provider needs async loading) and compared on static markup, then on the expanded element tree (button props and handlers).
The clock is fixed; `currentTime` is passed explicitly.

Coverage (see `results.json` for exact counts): all 9 locales; fixtures `map-scheduled`, `-populated`, `-conditional`;
online true/false; busyKey values (none, fixture, `clear:*`, `schedule`, every row's own key); `currentTime` at, one below and one
above every expire / completion / plunderAt / arrival / protectTime boundary and around the one-second countdown steps;
a generated matrix over every scheduleStatus x lastError form (strings, numerics, objects, Error instances, table needles,
prototype names) x timing state (including 999999999999 / 1000000000000 seconds-vs-ms threshold) x kind; reward and owner/quality
fallbacks; truck state variants; the duplicate in-flight (`ee2`) matrix; group filters and the tab count; error translators
called directly.

* `actionsEnabled=true`: markup identical to the original (byte for byte).
* `actionsEnabled=false`: after removing ` disabled=""` the markup is identical; every button carries `disabled`; no button has an `onClick`.
* Handlers: for each button the label, disabled flag, handler name and the exact row object (identity) or clear kind equal the original wiring.
* Negative proof: 51 deliberate mutations (boundary operators, filters swapped, `ee2` dropped, column order, widths, fence removed, handler arguments, table entries, error text, count) are applied to in-memory copies of the canonical sources; every mutation must fail the suite or the check fails.
* Baseline: the pre-campaign surface (commit `3e8617c`, `MapDataPage.jsx`: one row `map.scheduledPlunder · map.empty`, tab count hardcoded `"0"`) is asserted to differ from the original markup in all three fixture states and from the original count in both populated states.
* Keys: all 62 recovered `t()` keys (literals plus every table-driven `error.*` code) exist in all nine locales; the canonical code requests exactly the same key set as the original. Keys that appear only because the matrix feeds unknown codes (`error.DISPATCH_PLUNDER_FOO` etc.) are the original's "return the key itself" fallback.
* CSS: every class token emitted has a selector in canonical CSS iff it has one in the original CSS asset (no CSS added). Tokens without a selector in either: `map-scheduled-group--secret`, `map-scheduled-group--truck`, `map-reward-list--retained`, `pending`.

## Normalisations and divergences (disclosed)

1. Reward icon: the original `GameAssetImage` renders `<span class="map-reward-icon game-asset-placeholder" aria-label="{name}">` until the asset loads; the canonical Map tables (accepted earlier) render `<span class="map-reward-icon game-asset-placeholder" aria-hidden="true">` because the parent `.map-reward-item` already has the same `aria-label`. The checker runs the REAL `GameAssetImage` extracted from its asset and proves this attribute is the only difference.
2. `useI18n`: canonical groups call the repository hook; the checker stubs it. The stub has the same `t` semantics as `i18n.jsx` (`catalog[key] || key`, `{name}` substitution).
3. `gameTexts` defaults to a frozen empty object when omitted (the original always receives an object).
4. Fenced handlers: with `actionsEnabled=false` the buttons have no `onClick` at all (not just `disabled`).

## UNKNOWN / not proven

* `UNKNOWN`: the schema of the rows returned by the original job-list native (`map_plunder_jobs_list`); the frontend asset only shows the fields it reads. Fixtures use only those fields.
* `UNKNOWN`: pixel-level parity (no browser screenshot comparison in this checker); in-app screenshot parity is the main worker's harness.
* `UNKNOWN`: whether the original ever renders `online` as a non-boolean; the canonical code keeps the same short-circuit expressions.
* Not proven: runtime behaviour of the original with real job rows or native availability. Nothing here calls native code.

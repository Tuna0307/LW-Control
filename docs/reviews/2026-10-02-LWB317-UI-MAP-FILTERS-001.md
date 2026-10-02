# LWB317-UI-MAP-FILTERS-001 — lead implementation delivery

Status: **AWAITING_REVIEW**. PM-023 preserves this source/local checkpoint;
the lead authored the correction and does not independently accept it.

## Result

Map quality, task completion, retained-goods and plunderable-only choices now
belong to their individual tabs. A Truck edit no longer leaks into Train,
Secret Task or Ghost Ops queries. Returning to a tab restores its choices.
Clearing a goods filter removes only that tab's item-count sort. Secret Task's
level selector now sends matching minimum and maximum levels, as recovered.

An explicit `map-treasure-checking` offline preview now reaches the existing
Treasure Checking formatter through MapDataPage. Missing world/player states
show Checking; known states and blocking-reason precedence remain intact.
The input requires preview mode, that exact fixture, Treasure tab and boolean
true. It cannot activate in native/native-unavailable modes or another fixture.
This is a synthetic UI-state demonstration, not a running refresh operation.

## Exact evidence

Target EXE SHA-256 was rechecked:
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.

`MapDataPanel-B4GXEND2.js` SHA-256:
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
Locators below are UTF-8 byte offsets in that exact asset, not pretty-print lines.

| Recovered fact (EXACT_BYTES) | Locator |
|---|---|
| Per-kind quality / goods / completion / plunderable state objects | 30908 / 30935 / 30989 / 31016 |
| Quality kinds Truck/Train/Secret Task/Ghost; goods kinds Truck/Train | sets Re 6098 and O 6149; helpers Ue 7288, A 7320 |
| Per-kind quality / goods / completion / plunderable change handlers and page reset | 53247 / 53914 / 52663 / 54210 |
| Query derives only applicable kinds; exact Secret Task min/max level | nr 37497 |
| Treasure refreshing state starts false and reaches table | 31675 / 56776 |
| Context/status + prioritized all-refresh producer | useEffect 32168 |
| Search-page refresh producer and busy/online/server/empty/failure rules | rr 38513 |
| Treasure state merge by UUID | He 7093 |

Exact expressions, source identities, locators and executable comparisons are in
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/source-contract.json`.

`index-BVfnK1wp.js` SHA-256:
`44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.
Recovered wrappers: `map_treasure_state_refresh` at 207597 with
`{serverId, records}`; `map_treasure_state_refresh_all` at 207676 with
`{serverId}`; `map_treasure_claim_status` at 207747 with no original payload.

## Treasure connection finding and limits

Current host handlers already exist in `Map317CommandService.cs` lines 146–154
and `ManualMapScanCommandService.cs` lines 161–165, with inspection admission
and current-server checks in `InspectTreasureStateAsync` from line 241.
Their current hashes and exact locator text are pinned in `treasure-results.json`.
The canonical `mapBackend.js` exposes none of these three operations, and
MapDataPage has no live viewer-context/refresh lifecycle producer. Host presence
does not prove that the feature currently works. No host command was executed.

The remaining frontend/host connection is a separate assignment. Its contract
must preserve context lookup, query gating, generation/cancellation behavior,
row merging, error treatment and the source's distinct table-loading versus
Treasure-refreshing flags. This delivery does not turn query loading into Checking
and does not add automatic game inspections or claims.

## Verification

- 472 actual original/current query projections and table item-key observations
  across all eight normal tabs; actual production component controls, persistent
  hook cells, callbacks and search effect are executed. The immutable c7c3a32
  baseline produces 330 distinguishing mismatches; corrected code produces zero.
  Unrelated component effects are suppressed in this focused harness.
- 216 Treasure world/player display comparisons across nine locales; 384 flag
  input fences; eight exact original producer scenarios with deferred synthetic
  replies, failure/scan suppression and online/scan/server/empty gates.
- Historical normal-table checker replay passes 288 column cases, 15,264 value
  comparisons, 72 renders and existing state/reward/clock/fixture assertions. Only
  its old scalar-setter dependency is adapted read-only for the new state shape.
  Historical sources, results, manifests and validators are unchanged.
- Fifteen real browser-preview flows cover tab isolation/restoration, goods sort
  preservation/clear, exact level-6 rows, EN/JA Checking/known-state precedence,
  ordinary-fixture isolation and disabled native row actions. No console errors.
  Three screenshots were inspected; viewport clipping limits their visual scope.
- Canonical `check`, `build`, `check:production-build`, focused evidence validation
  and diff checks pass. Nine catalogs remain at 1,383 messages each.

Production source fingerprint:
`b09ef2120b8f67b5de0f9430a814ae0c9d33e50a5e0ba267597063c70c10f7eb`.
Package fingerprint:
`b56793f5b4a7934be8e56032ca447c87ee0f81eb3f430e9def803dd015119d34`.

Protected AFK/scratch/parent screenshots retain all five pinned hashes and remain
unstaged. No Last War process or original protected runtime was launched.
Original pixels, native Treasure operation reachability, other Map state
transitions (including data-clear/page-cache/search timing), scheduling/claim
controls, missing assets and broader Automation/AFK states remain unaccepted.
Overall UI stays **IMPLEMENTED_NOT_VALIDATED**.

## Reproduce and continue

From the repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/check-filters.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/check-treasure-checking.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/check-table-regression.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTERS-001/validate-evidence.mjs
```

Do not rewrite historical evidence to match newer product hashes. The next
returning-worker unit is an independent review of this bounded change under
`docs/work-items/LWB317-REVIEW-MAP-FILTERS-001.md`. No broader campaign or native
assignment is automatically resumed.

# LWB317-REVIEW-MAP-BASIC-001 — independent basic Map table review

State: REVIEW_COMPLETE. Historical baseline recommendation **CHANGES_REQUIRED**;
corrected-current recommendation **ACCEPT for focused source/local scope**.
Scope: City, Resource, Monster normal-table presentation against recovered
LWBridge 0.3.17 source. Read-only on production. Coordinator owns corrections
and final source/current rerun; lead acceptance remains separate.

## Identities and baseline

The target executable SHA-256 was directly verified as
`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
Baseline HEAD: `6a03a328f25e475bf8e0a8e711d172b62b25fe29`.
Before coordinator edits, copied actual production `MapDataPage.jsx`,
`mapTablePresentation.js`, `mapBackend.js`, and `i18n.jsx` to this unit's
`baseline/` directory, with original locations and SHA-256 in
`baseline-manifest.json`. Analysis executes this immutable source, not the
moving shared checkout. No product files were changed by this reviewer.

Original assets:

- `frontend-package/web/assets/MapDataPanel-B4GXEND2.js`: SHA-256
  `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- `frontend-package/web/assets/index-BVfnK1wp.js`: SHA-256
  `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`.

These source identities and locators are EXACT_BYTES. Executed source/current
comparisons establish local presentation contracts, not original-runtime pixels.

## Independent checks

`check-basic-review.mjs` extracts and executes the original complete memoized
table renderer, its column factory/helpers, and original icon renderer `Vr`.
It independently extracts and transforms actual production `MapTable` JSX with
esbuild and uses real imported production presentation functions and en/ja
catalogs. A deterministic clock makes shield boundaries repeatable. JSX/hook
shims expose rendered elements, keys, classes, labels, SVG paths, and disabled
states; they do not execute native bridge operations.

Immutable baseline: **268 assertions pass; 104 comparisons fail**. Failures are
four defect groups, with repeated locale/table/predicate cases; they are not
104 separate product defects. Exact minimal cases and full inputs are retained
in `baseline-results.json` and source expressions/byte locators in its
`locators` object.

Passed areas: all three column metadata arrays (order, width, sort field),
name fallback/resolution, level/HP/distance number formatting, occupied/idle
resource text, zero/missing/second/millisecond update times, past/equal/future
shield expiry and fallback, primary/secondary sort labels and aria-sort,
empty and loading-empty labels. These tables contain no quality, power, or
reward-count columns; those concepts are not invented for this scope.

## Confirmed defects

1. **Coordinate content and invalid values.** Original `de`, Map byte **15490**,
   returns a direct `-` for missing, fractional, zero, or negative x/y. For
   valid integers >=1 it displays coordinates plus localized Jump, or Jumping
   for a matching `serverId:x:y` state key. Baseline MapDataPage.jsx lines
   147–151 always constructs a button, only prints coordinates, accepts zero
   or negative integer values, and has no Jump/Jumping content. Minimal input
   `{serverId:321,x:12,y:34}` gives original text `12,34Jump` versus `12,34`;
   `{serverId:321,x:0,y:34}` gives direct original `-` versus button `0,34`.
   Correction is the existing normal-table coordinate cell and keyed
   presentation state, with native fencing preserved.

2. **City mark icon and tooltip.** Original `he`, Map byte **17832**, renders
   the favorite/favorite-filled SVG and a title containing Mark/Unmark plus
   Position missing/replaced when `trackerState` has that value. Baseline
   MapDataPage.jsx lines 143–145 uses Unicode stars and omits the title.
   Minimal input `{ownerUid:'42',marked:true,trackerState:'missing'}` shows
   original title `Unmark player · Original position is empty` (catalog exact case/text
   in results), actual absent title. Original index `Vr` byte **330489**;
   favorite branch byte **331208**; same path for both states with
   `ui-icon is-filled` only for favorite-filled. Reuse exact SVG paths/classes.

3. **Row presentation classes.** Original table body adds `is-marked` only
   for strict `marked===true`, `is-missing` for tracker missing, and
   `is-replaced` for tracker replaced. Baseline MapDataPage.jsx line 137
   always renders `map-row`. Minimal `{marked:true,trackerState:'missing'}`:
   original `map-row is-marked is-missing`, actual `map-row`. This applies
   to all normal kinds; class expression is retained in the original complete
   renderer and evaluated independently.

4. **Row identity composition.** Original `ue`, Map byte **15306**, prefers
   uuid, then marchUuid, then recordKey, then the composite
   `pointIndex:ownerUid:updatedAt` using its source truthy fallbacks. It uses
   raw serverId. Baseline MapDataPage.jsx line 103 prefers recordKey before
   uuid, inserts server 0 for missing values, and uses index instead of that
   composite fallback. Minimal `{serverId:321,uuid:'11',recordKey:'record'}`:
   original `city:321:11`, actual `city:321:record`. Missing server +
   `{pointIndex:0,ownerUid:'42',updatedAt:123}`: original
   `city:undefined::42:123`, actual `city:0:0`. Correct the existing key
   closure; these three kinds have no selection checkboxes to redesign.

## Intentional current-client boundaries

The source City mark disables only missing ownerUid. The clone additionally
fences native-unavailable/busy actions. This known availability boundary must
stay intact and is recorded separately from icon/title/class defects. Tests
compare source predicates under matching available inputs, then independently
assert the clone's additional fencing across actionDisabled/actionBusy values.

Explicit `rebuildGatherOccupancyKnown:false` renders `—`; known occupied uses
the supplied occupied label. These documented current-client uncertainty states
are preserved instead of inventing original gather fields. Legacy/original
row shapes still match original idle/gathering resolution.

## Browser verification and final correction verification

Browser is coordinator-owned. Requested scenarios: all three populated tabs
under `map-table-states`, English and Japanese representative labels; City
marked/missing/replaced rows, native-disabled mark/jump controls; invalid
coordinate plain dash; Resource known occupied/idle and explicit unknown
occupancy; Monster known/missing names and distance; empty rows via a disclosed
no-match QA query. Coordinator records actions/results and inspected screenshots
under its combined delivery. Coordinator supplied actual DOM observations at
`evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-ROWS-001/browser-results.json`.
Reviewer independently checked City English/Japanese, Resource English, and
Monster English flows. Confirmed corrected mark SVG classes, titles and row
classes, Jump/Jumping text, invalid-coordinate direct dashes without jump buttons,
populated names/distance, preserved unknown/idle/gathering states, disabled native
controls, and recorded zero console errors. Browser empty/missing-name cases are
not present in this delivered DOM record; these remain executable-render coverage,
not claimed browser actions. `browser-review.json` pins reviewed records/images.

Reviewer additionally inspected `city-row-actions.png`: visible marker states,
position styling, coordinate text, and invalid-coordinate dashes agree with DOM.
Coordinator inspected the second Japanese Truck image. Both viewport images
have clipped lower/right table edges, so screenshots alone do not establish full
table geometry or every row. DOM data supports the asserted cases. No original
pixels or native actions were observed by this reviewer.

Run historical review without overwriting the integrated result:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/check-basic-review.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/validate-evidence.mjs --baseline-only
```

Run final integrated correction assertions without rewriting historical failures:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/check-basic-review.mjs --current
node evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-BASIC-001/validate-evidence.mjs
```

`--current` writes `current-results.json` and returns nonzero on any remaining
comparison defect. Baseline recommendation remains CHANGES_REQUIRED even if
the separately recorded correction later passes. Source/local correction results
cannot establish original post-auth pixels, native commands, scans, query/filter
semantics, Scheduled Plunder, or game text/asset acquisition.

## Integrated-source replay checkpoint

After the coordinator's scoped production correction, reviewer independently
ran `check-basic-review.mjs --current`: **372 assertions pass, zero mismatches**.
The validator also passes for historical baseline and corrected current source.
`current-results.json` pins the actual corrected source hashes for this replay.
The complete shared Map diff was inspected: recovered coordinate branches,
mark SVG/title, row classes and identities address the four independently
confirmed groups while existing availability fences remain. This establishes
the corrected source/local presentation checkpoint only. Supplied coordinator
browser observations now pass the focused assertions in this unit's validator.
Integrated builds/regressions remain coordinator delivery responsibilities;
none are represented here as reviewer-executed checks. Final recommendation is
ACCEPT for corrected current City/Resource/Monster presentation within this
source/local scope, retaining original-pixel/native/global-parity limitations.

## Final Map row correction peer review

Lead extended this read-only review to inspect the complete final MapDataPage/
mapPreviewApi correction diff, original `de`/`me`/`ue`/`he` contracts, and the
coordinator's actual `check-row-actions.mjs`. No further product edits were made
by this reviewer. Independent replay of the prior transport checker **without
record flags** returns ACCEPT, zero mismatches; historical transport failure
records remain untouched. The coordinator's 160-case original/current action
checker passes, including missing/whitespace marchUuid, Follow/Following,
disabled/busy combinations, callbacks, and native/preview fences.

Additional reviewer-owned `check-final-peer-review.mjs` executes **41
distinguishing cases** independently against actual final source. It covers
numeric zero versus string `"0"` march IDs, null/whitespace/padded IDs, raw zero
and missing servers, absent/false/zero name fallback, original exact row keys,
paired local checkbox checked/unselected states and callback row identity,
native-follow default disabled behavior, restored reward-cell TD class, and
the preview-state/jump-key expression across native, native-unavailable, and
preview modes. Results/source locators/final hashes are in `peer-review.json`.

No acceptance blocker was found for the corrected source/local UI unit. The
canonical page leaves `liveTargetDisabled` at true because the native follow
provider is absent; presentation parity is exercised with a disclosed callback
availability input, while production remains fenced. Preview-provided Following
keys apply only in preview mode and the named row-actions fixture. Real native
coordinate busy keys remain available to Jumping labels. The fixture provider
returns null in native/native-unavailable modes and synthetic actions reject.

The clone's existing generic selectedKeys/onSelect pair uses the recovered row
identity consistently; original table uses separate typed selection sets keyed
by server and trimmed UUID. Reviewer compared checked/unchecked behavior with
each producer's appropriate identity and confirmed callback row preservation.
This local representation difference does not create a visible parity defect
in the examined states. Future native scheduling/selection contracts remain
outside this UI acceptance. Missing task UUID eligibility rules are unchanged.

Recommendation: **ACCEPT focused source/local Map row correction**, subject to
project-lead integration of the combined verification. This review does not
upgrade original pixel, native-follow, broader filter, claim, or scheduled-task
parity.

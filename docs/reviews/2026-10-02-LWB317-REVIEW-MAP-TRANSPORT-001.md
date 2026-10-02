# LWB317-REVIEW-MAP-TRANSPORT-001 — Truck and Train independent review

Date: 2026-10-02
Branch: `research/offline-controller`
Reviewed checkpoint: `1e79bb11a5f05f8d8f4dbff9b3507ff413d0d785`
Implementation under review: `bf84bbdca8a86c1a45e9cbb3bd0170c8fbfcdd7f`
Result: **REVIEW_COMPLETE — CHANGES_REQUIRED**

This review covers only Map `truck` and `railway` table presentation. No product code was changed.

## Reference identity and independent method

The recovered reference identities match the assignment:

- `lwbridge-0.3.17.exe`: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`
- `MapDataPanel-B4GXEND2.js`: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`
- `index-BVfnK1wp.js`: `44C4E4043825B7DB296B64171951B27176F8850FF8D7D337CC991DF4765524C6`
- `rewardDisplay-eZWrd6iS.js`: `7F65DD3F5B81C96117AF5E83E8310D6C6A3A48787B1F693D387020255C78E73E`

The independent harness executes recovered helpers/JSX and current production helpers/JSX at fixed time `1799000000000`, in English and Japanese. It covers 8 column-metadata comparisons, 104 column-value comparisons, 26 Truck state cases, 26 eligibility cases, 26 selection-label cases, 8 Live Target cases, 12 retained-goods cases, 24 compact-count cases, 8 conditional item-sort metadata cases and 4 filter-callback cases. The result is 14 individual mismatches grouped into the two defects below. Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TRANSPORT-001/transport-review-results.json`.

Recovered source locators used by the harness are:

- column factory `nt`: `MapDataPanel-B4GXEND2.js` byte `9594`
- table renderer: byte `14837`
- Live Target renderer `de`: byte `15490`
- retained-goods renderer `fe`: byte `16260`
- selection renderer `me`: byte `17072`
- item-filter callback: byte `53914`
- Truck maximum `Fe`: `index-BVfnK1wp.js` byte `201275`
- Truck state `Ie`: byte `201399`
- compact-count formatter: `rewardDisplay-eZWrd6iS.js` byte `0`

## Defects

### 1. Truck and Train Live Target cells render coordinates instead of recovered tracking content

Recovered `de` at byte `15490` treats Truck and Train as Live Target kinds. If `marchUuid` is empty, it renders literal `-`. If `marchUuid` is present, it renders a disabled/enabled tracking button whose copy is `Follow` / `Following` (`追跡` / corresponding following copy in Japanese). The column factory correctly labels this column `Live Target` / `リアルタイム対象`.

Current `MapDataPage.jsx:147-151` routes every coordinate-marked column through `coordinateText(row)`. Truck and Train are merely forced disabled there, so their cells show `x,y` rather than the recovered tracking presentation.

Distinguishing fixed-time cases:

| Language / table | Row | Recovered | Current |
| --- | --- | --- | --- |
| EN Truck | no `marchUuid`, `x=12,y=34` | `-` | disabled `12,34` button |
| EN Truck | `marchUuid=live-march` | disabled `Follow` button | disabled `12,34` button |
| EN Train | no `marchUuid`, `x=21,y=43` | `-` | disabled `21,43` button |
| EN Train | `marchUuid=live-march` | disabled `Follow` button | disabled `21,43` button |
| JA Truck | no `marchUuid` | `-` | disabled coordinate button |
| JA Truck | with `marchUuid` | disabled `追跡` button | disabled coordinate button |
| JA Train | no `marchUuid` | `-` | disabled coordinate button |
| JA Train | with `marchUuid` | disabled `追跡` button | disabled coordinate button |

The browser preview reproduces the missing-`marchUuid` case directly. `map-table-states` Truck rows show disabled `411,521`, `412,522`, etc. under `Live Target`; the recovered source would show `-`. The Japanese `map-railway` fixture similarly shows `411,521` under `リアルタイム対象`. Native action disablement itself remains intact.

### 2. Truck invalid-row selection aria labels add fallback hyphens absent from the recovered source

Recovered selection renderer `me` at byte `17072` passes `String(ownerName || allianceName || uuid)` and raw `serverId` to `map.selectNamedTask`. Current `MapDataPage.jsx:142` adds `|| "-"` to both the name and server substitutions. Eligibility still matches the recovered state helper; this is a label parity defect, not an eligibility defect.

Distinguishing cases include:

- missing UUID/name/alliance, server `321`: recovered EN `Select , server 321`; current `Select -, server 321`. Japanese likewise inserts `-` only in current.
- valid name, server `0`: recovered EN `Select Owner, server 0`; current `Select Owner, server -`. Japanese likewise uses `0` in recovered and `-` in current.
- missing server field: recovered passes the missing value through the translation substitution, while current forces `-`.

All these rows remain disabled in both implementations, so label and eligibility were verified separately as required.

## Matching behavior

Truck and Train column metadata otherwise match the recovered factory. Truck has nine columns in recovered order with widths `56`, `190`, `minmax(170px, 1fr)`, `70`, `120`, `minmax(360px, 2fr)`, `190`, `150`, `150`; Train independently has seven columns with no selection column and widths `190`, `minmax(160px, 1fr)`, `70`, `120`, `minmax(360px, 2fr)`, `150`, `150`. Current definitions are at `mapTablePresentation.js:213-282`.

Truck state/countdown and selection eligibility match the recovered helpers, including these distinctions:

- `arriveTs === now` is expired and selection-disabled; the visible recovered/current status still falls through to `Ready｜Plundered 1/3` because the source status renderer has no separate expired text branch.
- `protectTime === now` is ready; one millisecond/second later is protected and remains selectable.
- full takes precedence over protection.
- Reindeer/special Truck maximum is `1` regardless of a larger `maxLootCount`.
- missing or invalid `maxLootCount` becomes maximum `0`; it does not make the row full, and labels such as `Ready｜Plundered 8/0` match source.
- invalid UUID/server rows are selection-disabled independently of their visible status text.

Owner/alliance fallback, quality/Reindeer display, power formatting, arrival/protection/updated timestamps and Train alliance-name/abbreviation fallback all match the recovered column value functions in both languages.

Retained goods also match apart from the separately recorded missing native image assets: empty arrays render `-`; supplied game-text names take precedence with fallback names retained; selected items move first without disturbing other item order; full localized counts are used in title/aria descriptions; compact counts match the recovered K/M/G thresholds; and `itemCount` exists as a sortable column only while an item is selected. Current renderer is `MapDataPage.jsx:156-158`.

The recovered/current item-clear callbacks match for this focused behavior. Clearing the item removes `itemCount` from the active table's sort list and resets page 1 (`MapDataPage.jsx:459-465`). In the browser, selecting `Fixture Reward`, sorting Items descending and inspecting the first rows produced counts `8,8,8,8,8,8,8,6`; clearing the item removed Items sorting and restored Updated At descending with Truck Owner 1/2/3 first.

## Browser evidence

The existing preview fixtures were used without modification. `map-table-states` verified protected/full/ready Truck rows and disabled native controls. The fixture rows contain one retained-good entry apiece, so browser inspection could verify item rendering/count ordering and item-count sort/clear behavior but cannot distinguish intra-row selected-item-first ordering; that case is covered by the recovered/current JSX harness. Direct `map-truck` and Japanese dark `map-railway` previews supplied the saved images. Both saved images were opened and inspected after capture:

- `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TRANSPORT-001/truck-en.png` — SHA-256 `994E88261AAC12ED12EEAC965B6228A8530734FF3299AAE4C95AC77558FB5ABB`
- `evidence/lwbridge-0.3.17/ui/LWB317-REVIEW-MAP-TRANSPORT-001/train-ja.png` — SHA-256 `3E1B29E8873C023C51B662A0F524D231D266F1AC6F34B5C68F59C936DAA03D9E`

Structured browser observations are in `browser-results.json`. Browser-console error capture returned zero entries; `console-errors.json` records that result. The headless Edge process emitted one browser-internal task-manager fallback diagnostic while writing the Japanese screenshot; it did not appear in the page console and the rendered PNG was inspected successfully.

## Remaining limits and recorded gaps

- Native retained-goods images remain placeholders; this review does not claim asset parity.
- The current page owns one scalar `itemKey` (`MapDataPage.jsx:223`) rather than independent Truck/Train item-filter state. The assignment explicitly keeps shared per-tab item-state ownership and broader query/filter interactions as separate recorded gaps; they are not reclassified as this review's source-local defect.
- No original post-auth pixel comparison was available, so the result is source/local presentation parity rather than original screenshot acceptance.
- No live target tracking, plundering, scheduling, jumping, game control or other native gameplay action was executed.

## Verification

The final verification pass succeeded:

- prior `LWB317-UI-LEAD-TABLES-001/check-map-tables.mjs --verify-record`
- prior `LWB317-UI-LEAD-TABLES-001/validate-evidence.mjs`
- `LWB317-REVIEW-MAP-TRANSPORT-001/check-transport-review.mjs --verify-record`
- `LWB317-REVIEW-MAP-TRANSPORT-001/validate-evidence.mjs`
- `npm.cmd run check` from `src/LWBridge.UI-0.3.17`
- `npm.cmd run check:production-build` from `src/LWBridge.UI-0.3.17`
- `git diff --check`

Historical reports were left unchanged, including the known stale parent CORRECT-003 Trade assertion. The pre-existing `previewAfkFixtures.js`, `.scratch-lwb317/`, and parent CORRECT-003 screenshot WIP remained unstaged and untouched by this review.

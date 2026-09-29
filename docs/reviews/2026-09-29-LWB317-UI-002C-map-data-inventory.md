# LWB317-UI-002C — Map Data UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Map Data observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This is a static `EXACT_BYTES` inventory only; no map
scan, export, claim, share, schedule, plunder or other action was invoked or
reverse engineered.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary recovered source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`

- recovered size: 57,600 bytes;
- SHA-256: `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`;
- imports `rewardDisplay-eZWrd6iS.js` and `GameAssetImage-Diy9VTIr.js` for
  reward/game-asset presentation;
- exported as `MapDataPanel` near byte `0xE0F2`.

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/map-data/static-inventory.json`

SHA-256 at creation:

`A60B57002F6723C81AAECB2DB2643269629EAFE0FBBFB97DC3B6B5AEC1C463A5`

It contains five exact source identities, 196 directly referenced English
locale entries, 71 class-name literals, tab maps, scan-type maps and defaults.

## Page title and defaults

Exact page title:

`World Map Data` (`map.title`).

The component's internal main-data tab initializes to `city` at byte `0x76C8`.
The scan-mode state initializes to `manual` at byte `0x7C93`.

Those are directly encoded component defaults. Persisted/runtime-controlled
state remains unobserved.

## Scan-mode tabs

The scan-mode tablist begins near byte `0xABA1` and exposes:

1. `Manual Scan`
2. `Auto Scan`

Manual mode statically contains the scan-content selector, using the exact
eight-entry recovered type array from byte `0x160F`:

1. Player City
2. Resource Point
3. Monster
4. Truck
5. Train
6. Secret Task
7. Ghost Ops
8. Treasure

Direct visible scan copy includes `Start Scan`, `Stop`, `Clear Map Data`,
`Speed`, `Normal`, `Fast`, `Scan contents`, `Server`, and `Scan progress`.

The Auto Scan section statically includes:

- `Enable automatic scanning`;
- `Running`, `Waiting for schedule`, `Disabled`;
- `Target servers` plus an `Add` action and removable server chips;
- `Interval (minutes)`;
- `Scan contents`;
- `Return to the original server after scanning`;
- `Run now`;
- `Next scan`.

Its exact target-server hint is:

`Enter server IDs and click Add. Commas add several at once; × removes one. No entries scans the current server.`

The exact navigation notice is:

`Each target server is entered before scanning; a server ID alone cannot scan another server.`

These are UI strings/controls only; their backend semantics were not traced.

## Main data tabs

The recovered tab-label lookup begins near byte `0x1824`. The main
`map-tabs` strip begins near byte `0xC491`.

Exact order:

1. `City`
2. `Resource`
3. `Monster`
4. `Truck`
5. `Train`
6. `Secret Task`
7. `Ghost Ops`
8. `Treasure`
9. `Scheduled Plunder`

Each of the first eight buttons displays a count badge. Scheduled Plunder uses
its own combined scheduled-item count. The selected tab receives class
`active`.

## Search/filter controls

For normal data tabs the static search row includes a text field whose exact
label/placeholder is:

`Search name, Alliance, or UUID`

Additional controls are conditionally rendered by tab:

- name selector with `All Names`;
- City: `Filter by Alliance`, `All Alliances`, `No Alliance`, `Marked only`;
- Treasure: item/type filters, `Show other-alliance Radar Treasures`,
  `Prioritize lucky positions`;
- Secret Task / Ghost Ops: status selector with `Status`, `Completed`,
  `In progress`;
- Secret Task: level filter;
- quality-capable tabs: `Quality`, `All qualities`, `N`, `R`, `SR`, `SSR`,
  `UR`, plus `Special (starred)` where statically applicable;
- Truck: additional `Reindeer` quality;
- item-capable tabs: `Item` and `All Items`;
- applicable rows expose `Only plunderable or pending`.

Runtime option contents and selected values are data-derived and remain
`UNKNOWN` rather than fabricated.

## Static actions and status copy

The page contains exact action/status text including:

- `Export Excel` / `Exporting…`;
- `Claim Boxes` / `Claiming…`;
- `Claim Season Treasures`;
- `Add to schedule ({count})`;
- `Share to Alliance` / `Sharing…`;
- `Plunder selected trucks ({count})`;
- `Random delay max (seconds)`;
- `Scheduled Plunder`.

These controls are inventoried because they are visible UI. They were not
pressed and no action behavior is claimed.

## Exact table/grid treatment

The common CSS source is:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`

Direct rules establish:

- `.map-tabs`: full-width horizontally scrollable segmented strip, `1px`
  border, soft-surface background, `8px` radius, `3px` padding, `2px` gap;
- main tab buttons: minimum height `28px`, `4px 10px` padding, `6px` radius;
- count badges: caption size, pill radius, `1px 6px` padding;
- `.map-scan-tabs`: segmented strip with `8px` radius and `3px` padding;
- scan-mode buttons: minimum width `112px`; responsive rules make each flex to
  equal width;
- `.map-scan-summary`: minimum height `42px`, `8px 10px` padding, `14px` gap;
- status pill: rounded `999px`, `3px 10px` padding;
- progress bar: `18px` standard height; summary variant `16px`;
- scan-content chips: rounded `999px`, `4px 11px` padding, checked state gains
  a check mark and segmented-pill styling;
- `.map-searchbar`: soft-surface bordered row, `8px` radius,
  `8px 10px` padding and `8px` gap;
- `.map-table`: fixed layout, separate borders, `1px` line border, `8px`
  radius, shadow, minimum width `720px`;
- Truck/Train tables: minimum width `1620px`;
- Secret Task/Ghost Ops and Scheduled Plunder tables: minimum width `1480px`;
- table scroll container: maximum height `min(620px,100vh - 350px)`.

The responsive CSS collapses scan/search rows vertically and makes filters
full-width where the media rule applies.

## Runtime gap

`BLOCKED` / not visually validated:

- actual post-auth page geometry and selected theme;
- persisted selected main/scan tabs;
- available/disabled scan types for the current account/server;
- row data, counts, translated game item/building names and images;
- exact populated table headers by runtime dataset;
- live hover/focus states and page screenshot;
- pixel comparison.

The exact frontend contains much more dynamic map-state code, but this stage did
not follow it into backend/provider semantics because that is outside the UI
campaign.

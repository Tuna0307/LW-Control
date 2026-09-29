# LWB317-UI-002E — City Layout UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime City Layout observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This stage therefore uses static `EXACT_BYTES` frontend
evidence only. No layout save/apply operation or other gameplay action was
invoked or behavior-traced.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary recovered sources:

- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/CityLayoutPanel-DoNWkywK.js`
  - 19,322 bytes
  - SHA-256 `C1B83A0B6524EF9AF510A114DF9679DAE0260D34B633659EB1A5FFB2AC5CF49C`
  - panel class near byte `0x2A93`; export near `0x4B5C`
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/GameAssetImage-Diy9VTIr.js`
  - 2,347 bytes
  - SHA-256 `2F92A87C3268497DF6425B1175DB6140E10AABCBDFAE02615F065D00E16458E0`
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js`
  - 72,319 bytes
  - SHA-256 `0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731`
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`
  - 126,217 bytes
  - SHA-256 `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/city-layout/static-inventory.json`

SHA-256 at review time:

`F239D8358B7D63BDA60058C2B0D984A76F686F8D152E4E1D0C8B98BE2FF4177E`

## Page structure and static controls

The exact page title is `City Layout`. The component has no recovered
top-level City Layout sub-tab array. Its main region label is `Main city`.

The header contains the static controls `Refresh`, `Undo`, `Redo`, `Restore
initial layout`, `Save draft`, and the primary `Apply to game` action. The
component also contains the directly recovered warning/error copy for stale
drafts, being outside the inner city, and generic action failure.

The main workbench is a city grid plus an inspector. Direct labels and controls
include:

- `Canvas zoom`, `Zoom out`, `Zoom in`, and `Fit canvas`;
- `Select a building to view its full name`;
- `Controls` with box-select, Shift-drag additive selection, Ctrl-click
  toggle, selected-building group drag, Ctrl+Z/Ctrl+Y history, and Esc clear;
- the cell legend `Available`, `Road: blocked`, `Flag only`, and `Locked`;
- `Building properties`, `Footprint`, `Movable`, and `Placement rule`;
- `Pending changes ({count})` / `No changes`;
- `Draft revision {revision}` and `{count} conflicts`.

The grid directly renders building images through the recovered
`GameAssetImage` helper, building name/level labels when geometry allows it,
selected/changed states, a box-selection lasso, and a per-cell state layer.
Actual building names, coordinates, images, counts, revisions and pending
changes are runtime/game-derived and are not fabricated here.

## Static state copy

Directly recovered empty/disconnected copy includes:

- `The game is disconnected, so the city layout cannot be loaded.`
- `No city layout data is available.`
- `Processing`

Directly recovered validation/application-state copy includes:

- `Local layout checks passed`;
- `Execution plan: {moves} moves, including {temporary} temporary moves`;
- `Run {count} building moves? A failure stops immediately and is not rolled back automatically.`;
- `Applying {current}/{total}`;
- `Stop remaining moves`.

These strings establish possible UI states only. This stage did not exercise
or trace their backend behavior.

## Exact layout/style evidence

The recovered CSS directly establishes:

- `.city-layout-panel`: grid with `10px` gap;
- header: flex, `12px` gap, spaced between at normal width;
- action/legend groups: wrapping flex rows with `6px` gap;
- warning/error blocks: `8px` radius and `9px 11px` padding;
- workbench: flexible main pane plus a `240px` inspector column, with a later
  responsive rule reducing the inspector column to `210px`, then collapsing to
  one column;
- center/inspector/footer surfaces: `1px` line border and `10px` radius;
- main heading: `9px 12px` padding with a bottom border and weight `650`;
- zoom buttons: minimum height `28px`, `4px 8px` padding;
- control help strip: footnote size, `7px 10px` padding, accent-soft-subtle
  background;
- grid viewport: muted surface and height `min(650px,60vh)`;
- city grid: `14px` padding, CSS grid, `touch-action:none`;
- building tiles: `5px` radius, bright line border, grab cursor, raised shadow;
- selected building: blue border with accent-glow ring; changed building:
  dashed border;
- building labels: caption size, white text with dark text-shadow;
- inspector validation blocks: `7px` radius and `8px` padding with exact
  success/danger semantic colors;
- footer: body size, spaced-between flex, `8px 10px` padding.

Shared theme variables in the exact stylesheet define the light palette used
by these rules, including `--bg:#f5f5f7`, `--surface:#fff`,
`--text:#1d1d1f`, `--muted:#5f5f63`, and `--blue:#0071e3`, plus caption,
footnote, body, title and page-title sizes of `11px`, `12px`, `13px`, `16px`
and `20px` respectively.

## Defaults and runtime gaps

The component's local canvas cell-size state initializes to `24`; the rendered
zoom percentage is relative to that baseline. Runtime layout data can change
the visible grid, selection, validation, history/action availability and
inspector content, so those effective states are not claimed as fixed defaults.

`BLOCKED` / not visually validated:

- post-auth rendered City Layout geometry/theme;
- actual cell/building population and translated building names;
- current city/region state and draft revision;
- current selection/history/validation/application state;
- live hover/focus/drag states;
- page screenshot and pixel comparison.

No gameplay/backend function recovery was performed.

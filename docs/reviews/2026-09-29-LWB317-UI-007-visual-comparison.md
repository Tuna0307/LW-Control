# LWB317-UI-007 — visual comparison and evidence-backed fix pass

Date: 2026-09-29

## Outcome

Status: `PARTIAL` because the statically possible comparison/fix work is
complete, while direct post-auth reference-vs-clone visual comparison is
legitimately `BLOCKED` by the excluded LWBridge authorization boundary.

No authentication/entitlement bypass was attempted. No Last War process was
launched or controlled. No gameplay/backend function recovery was started.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Comparison method

The original post-auth UI cannot be reached legitimately in the current
session, so pixel-diffing the clone against an original reference screenshot is
not possible. UI-007 therefore used the strongest available evidence below:

1. exact recovered 0.3.17 React component bytes;
2. the byte-identical recovered common stylesheet already used by the clone;
3. exact English locale strings;
4. clone DOM/layout inspection in the authorized Desktop browser;
5. fixed-viewport clone before/after screenshots at `1120 x 720`.

This closes source-verifiable clone deviations without converting static
evidence into a claim of runtime visual proof.

## Exact source identities

- `AutomationPanel-BJ0gIqFh.js` — SHA-256
  `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725`;
- `AutomationCard-LCx_jIi7.js` — SHA-256
  `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61`;
- `MapDataPanel-B4GXEND2.js` — SHA-256
  `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`;
- `SquadPanel-HC3-DJei.js` — SHA-256
  `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`;
- `index-rIL9Fpht.css` — SHA-256
  `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`;
- `en-BisSXcTB.js` — SHA-256
  `0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731`.

## Fixes from exact static comparison

### Automation common cards

The generic clone card formerly displayed `Disabled` while the exact reusable
Automation card resolves offline state to `status.disconnected`. The clone now
uses `Disconnected`, `automation-state state-disabled`, `role=status`, and
switch `role=switch` / `aria-checked=false`, matching the recovered reusable
card structure.

The default Automation before/after capture makes that static-state correction
visible while retaining the exact seven category tabs and card ordering.

### Resource gathering

The UI-006 fixture used invented generic form wrappers. Exact
`AutomationPanel-BJ0gIqFh.js` around byte `0x2C21` establishes the real
`automation-resource-gather-options` structure. UI-007 now uses:

- the normal reusable `automation-card` rather than an invented wide-card class;
- `automation-resource-gather-options`;
- recovered fallback scan radius `200`;
- recovered fallback manual delay `120` seconds / `2` minutes;
- `automation-resource-gather-recall`;
- the exact radius hint with the component's `1000`-tile fallback world size;
- exact `Loading squad settings` unloaded copy.

No squad/runtime state was fabricated.

### Trade Station

Exact `AutomationPanel-BJ0gIqFh.js` around byte `0x1E51` establishes an outer
`div.trade-station-panel` containing the reusable Automation card. UI-007 now
uses that hierarchy and the recovered:

- warning block;
- cross-server switch row;
- `trade-station-currencies` fieldset;
- four status counters with component fallback values;
- `trade-station-tabs`;
- Goods/Purchased tabs;
- `trade-station-goods` empty state and exact no-goods text.

The Desktop smoke pass selected this category and verified the expected classes,
one nested Automation card, exact tab labels, and no action wiring.

### Map Data

The UI-006 clone incorrectly wrapped Manual Scan content in
`map-auto-scan-card`. Exact `MapDataPanel-B4GXEND2.js` near bytes `0xAB87`
through `0xC453` establishes this order:

1. `map-scan-tabs`;
2. `map-header`;
3. Auto-only `map-auto-scan-card`;
4. shared `map-scan-summary`;
5. Manual-only `map-controls`;
6. `map-search` containing `map-tabs` and `map-searchbar`;
7. data table/pagination.

UI-007 now follows that hierarchy. It also moves City `Export Excel` into the
search row after the Search action, adds the result count, gives Scheduled
Plunder its own count badge, and uses the exact `map-empty` table cell.

The Auto Scan alternate-state smoke pass confirmed the Auto-only card, target
server controls, Speed, Scan contents, navigation notice, `Next scan`, shared
summary and absence of the Manual-only controls.

### Squads / AFK and Equipment Schemes

UI-006 used generic wrapper names for several sections. Exact
`SquadPanel-HC3-DJei.js` establishes:

- `monster-afk-layout` near byte `0x8B43`;
- `monster-afk-toolbar` compact cards;
- `monster-afk-profiles` / `monster-afk-profile-list` near byte `0x9B8C`;
- `equipment-preset-layout` near byte `0x2D18C`;
- `equipment-preset-rail`, list, main, toolbar, actions and hint classes.

UI-007 now uses those recovered selectors. The default AFK screenshot shows the
compact toolbar plus exact `Shared AFK Profiles` / `No AFK profiles` surface.
The alternate Equipment Schemes smoke pass verified the recovered rail/main
hierarchy and exact empty-preset copy/actions. All effective task/config values
remain deterministic disabled preview fixtures, not claimed reference defaults.

## Evidence captures

Before/after files and their SHA-256 values are recorded in:

`evidence/lwbridge-0.3.17/ui/visual-comparison/README.md`

Final after-capture hashes:

- Automation — `6C8170769E82AA0D29B85111734C05204EA040D7FD03899671F9DF048D85B684`;
- Map Data — `A61623DDD9025F039C0F4D81EF041DF215F97F9BED2D78F93BC23BC2420CF94B`;
- Squads / AFK — `3A6EE8165527583CE72877B3A3503E3B9A4C062D374A33E95B577C2857678A95`.

## Clone validation

The clone was exercised in the authorized Desktop browser after the fixes.
Confirmed states include:

- Automation default Daily Tasks;
- Automation Resource gathering alternate category;
- Automation Trade Station alternate category;
- Map Data Manual Scan default;
- Map Data Auto Scan alternate tab;
- Squads / AFK default AFK Tasks;
- Squads / AFK Equipment Schemes alternate tab.

No new browser-console errors were observed. The only error retained in the
browser's earlier capture history is a favicon 404 from before UI-005 added the
inline favicon; it is not a current UI-007 failure.

Repository/project validation for this checkpoint includes:

- `npm.cmd run check --prefix src/LWBridge.UI-0.3.17` — passed;
- `npm.cmd run build --prefix src/LWBridge.UI-0.3.17` — passed;
- `git diff --check` — passed;
- no changes under legacy `src/LWBridge.Desktop`.

## Remaining legitimate blocker

The following cannot be completed without legitimate post-auth reference access:

- original post-auth screenshot capture in matching states;
- direct reference-vs-clone pixel/geometry diff;
- confirmation of the reference's active theme/language/profile entitlement
  state;
- validation of populated runtime/game-derived rows, buildings, profile data,
  persisted toggles and hover/focus pixels against the original runtime.

These gaps remain `BLOCKED`; they are not a reason to bypass authentication.
Accordingly the UI rows remain `IMPLEMENTED_NOT_VALIDATED`, and Phase 2 remains
closed pending project-lead review.

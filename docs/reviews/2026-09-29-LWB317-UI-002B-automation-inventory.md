# LWB317-UI-002B — Automation UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Automation-page observation is `BLOCKED` by the out-of-scope LWBridge
authorization/login boundary documented in `LWB317-UI-001B`.

This inventory is therefore a static `EXACT_BYTES` UI contract recovered from
the accepted 0.3.17 frontend package. It records rendered structure, exact
English copy candidates, directly encoded UI fallbacks and CSS. It does not
trace or claim backend/gameplay function behavior.

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Exact static sources

| Recovered source | Size | SHA-256 |
|---|---:|---|
| `assets/AutomationPanel-BJ0gIqFh.js` | 75,388 bytes | `6DB2328191AF33E5631D507271C36D7008B287DAA7C8CD1A957B23EF6F871725` |
| `assets/AutomationCard-LCx_jIi7.js` | 5,092 bytes | `24ED773623C237F8219EFF5443FAC3BA7B55B6E99E168E52FF066FAF2ABE7A61` |
| `assets/en-BisSXcTB.js` | 72,319 bytes | `0BF43D180EB93692A93B830B5D984E9D01DDEEA524340F2334FC63CBA527A731` |
| `assets/index-rIL9Fpht.css` | 126,217 bytes | `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545` |

Machine-readable stage evidence:

`evidence/lwbridge-0.3.17/ui/pages/automation/static-inventory.json`

SHA-256 at creation:

`A5C084B9E3C531279451654F69A2A58295EABDADA6ED53FFFA7FD5E3F64EF91B`

It records source identities, component byte offsets, tab order, card-title
inventory, 261 exact English locale entries directly referenced by the
Automation panel/card JavaScript, and 109 direct class-name literals.

## Automation page shell

Primary source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/AutomationPanel-BJ0gIqFh.js`

Direct byte locators:

- `0x5539` — `AutomationPanel` implementation (`function Ae(...)`);
- `0x631A` — internal selected-category state initializes to `daily`;
- `0xA33C` — outer `section` with class `panel`;
- `0xA638` — top-level `automation-categories` tablist;
- `0xA672` — exact top-level category array;
- `0x12606` — export mapping `Ae as AutomationPanel`.

The outer panel renders configuration save/error indicators first, followed by
a `panel-title`, the category tablist and an `automation-grid` content area.

The exact page title is `Automation` from locale key `nav.automation`.

The title subtitle/status can render:

- `Game connected` (`status.gameConnected`);
- `Game disconnected. Actions are disabled.`
  (`status.gameDisconnectedDisabled`);
- `Saving…` (`automation.configSave.saving`);
- `Saved automatically` (`automation.configSave.saved`);
- `Some changes have not been saved. Please retry.`
  (`automation.configSave.error`).

Which one is actually visible depends on runtime/config state and was not
runtime-observed in this campaign.

## Top-level tabs and default

The exact source order is:

1. `daily` — `Daily Tasks`
2. `alliance` — `Alliance`
3. `resourceGather` — `Resource gathering`
4. `resources` — `Resource Claims`
5. `chat` — `Chat`
6. `trade` — `Trade Station`
7. `system` — `Protection & System`

Each is emitted as a `role="tab"` button. The selected button receives class
`active` and `aria-selected=true`.

`daily` is the component's directly encoded internal default category. The
main application may provide a controlled category value for some application
states; because the post-auth runtime was inaccessible, the effective saved
runtime category is not claimed.

## Card/panel inventory by category

These titles resolve directly from the recovered English locale and static
render branches. They describe UI cards/panels only, not recovered function
contracts.

### Daily Tasks

The `daily` category is rendered in two static blocks in the minified bundle.
Together they contain:

1. `Auto Training`
2. `Automatic Construction`
3. `Free Stamina`
4. `Automatic Treatment`
5. `Trucks`
6. `Secret Task`
7. `Automatically assist alliance Secret Tasks`
8. `Ghost Ops`

Representative exact direct UI copy includes `New soldiers`, `Target level`,
`Highest unlocked level`, `View barracks`, `Claim All`, `Only selected building
types`, `Building types`, `Automatically claim completed rewards`, `Maximum
builders`, `Soldiers per Army`, `Ally secret tasks`, and `Schedule selected
help`.

### Alliance

Static card order:

1. `Alliance Tech Donations`
2. `Automatic Official Application`
3. `Automatic Alliance Train Boarding`
4. `Alliance Help`
5. `Alliance Gifts`
6. `Excavation Stronghold Resources`
7. `Alliance Center Resources`
8. `Alliance Gathering Dispatch`

The official-position selector has exact static choices `Select a position`,
`Vice President`, `Strategy Minister`, `Defense Minister`, `Construction
Minister`, `Science Minister`, and `Internal Affairs Minister`.

### Resource gathering

This category renders one specialized Resource gathering panel. Exact direct
copy includes:

- `Resource gathering`;
- `Continuously gather ordinary resources. AFK and activity tasks take priority. Pause under a shield and resume after the configured manual-action delay.`;
- `Enable at least one gathering squad`;
- `Resource`;
- `Search level`;
- `Wait after manual actions (minutes)`;
- `Scan radius (tiles)`;
- `Recall squads when gathering is disabled`;
- `Loading squad settings`;
- `Manual wait until {time}`;
- `Shield ends at {time}`.

The exact radius hint says: `Choose 50–500 tiles. The world map is
{size}×{size} tiles. This is the straight-line radius from your base; larger
ranges take longer to scan.`

### Resource Claims

The descriptor array recovered around AutomationPanel byte `0x48A5` contains
exactly two cards:

1. `Building Resource Collection`
2. `Armed Truck`

Both use the generic resource-claim card treatment with interval/run status
copy including `Interval Minutes`, `Last Run`, `Next Run` and `Run now`.

### Chat

Static card order:

1. `Red Packet`
2. `Fireworks / Egg`
3. `Treasure`

The common description template is `Listen for chat messages and automatically
claim {type}.`

Directly encoded configuration UI includes `Claim settings`, `Min Delay
Seconds`, `Max Delay Seconds`, `Auto Reply`, minimum/maximum reply delay fields,
a reply phrases textarea, Treasure-only allied search/dispatch controls,
squad-priority checkbox/drag controls, `Pending Claims`, `Pending dispatches`
and `Latest Result` status rows.

### Trade Station

One specialized full-width panel is titled `Trade Station Auto Purchase`.

Direct exact copy includes:

- `Listen for trade station broadcasts and buy every selected available good serially.`;
- `Prices are not compared; selected goods are purchased only with the checked currencies.`;
- `Scan cross-server trade stations`;
- `Purchase currencies`;
- `Goods to buy` / `Select goods`;
- `Detected`, `Attempted`, `Succeeded`, `Last result`;
- `Purchased items ({count})`;
- `Show city-owner exclusive`;
- `Loading goods...`;
- `No trade goods are available from the current season template.`.

The panel has its own `trade-station-tabs` segmented control. Its effective
runtime-selected sub-tab/state is `BLOCKED`.

### Protection & System

Static card order:

1. `Weekend Shield`
2. `Attack Shield`

Exact descriptions are `Maintain a Shield throughout Saturday server time.`
and `Try 8, 12, and 24-hour Shields in order when attacked.`

Visible status-row labels include `Current Shield`, `Shield Ends`, `Weekend
Window`, `Local Window`, and `Pending Reason`.

## Directly encoded UI fallbacks

The following values are static fallback values used when corresponding config
fields are absent. They are **not** claimed to be the user's saved runtime
settings:

| Field/context | Static fallback | Byte locator in AutomationPanel |
|---|---:|---:|
| Chat claim min delay | `0` seconds | `0x5335` |
| Chat claim max delay | `0` seconds | `0x535E` |
| Treasure dispatch retry | `30` seconds | `0x545A` |
| Reply min delay | `2` seconds | `0x5487` |
| Reply max delay | `5` seconds | `0x54B5` |
| Dispatch min delay | `2` seconds | `0x54E6` |
| Dispatch max delay | `5` seconds | `0x551A` |
| Alliance donation threshold | `15` | `0x622A` |
| Treatment soldiers/army | `1` | `0x629B` |
| Trucks delay minutes | `2` | `0x641D` |
| Secret Task delay minutes | `3` | `0x648B` |
| Alliance Gifts interval | `120` minutes | `0x64FE` |
| Resource interval fallback | `60` minutes | `0x5D73` |
| Construction max builders | `1` | `0x6699` |
| Official position | `0` (`Select a position`) | `0x6712` |
| Train ticket count | `1` | `0x68AB` |
| Secret-task assist check interval | `30` seconds | `0x6AFF` |

Boolean enabled/disabled values and dynamic counts/statuses generally come from
loaded configuration/status objects. Where those values are not fixed by exact
static code, this inventory leaves the runtime default `UNKNOWN`.

## Shared Automation card treatment

`AutomationCard-LCx_jIi7.js` supplies the reusable card UI. Direct class names
include `automation-card-header`, `automation-card-title-group`,
`automation-card-header-actions`, `automation-header-switch`,
`automation-card-meta-row`, `automation-state state-*`,
`automation-status-pills`, `automation-card-summary`, `automation-error`,
`automation-config`, `automation-config-trigger`, `automation-config-body`, and
`automation-run-action`.

The generic card can therefore present a title/description, on/off switch,
state chip, summary/status values, expandable settings and an optional action
button. Which controls appear on a specific card is encoded by its props; this
is not evidence that its action succeeds at runtime.

## Exact visual-system rules used by Automation

Primary CSS source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`

Direct recovered rules include:

- `.automation-grid`: `repeat(auto-fill,minmax(310px,1fr))`, `gap:10px`, items
  aligned start;
- `.automation-categories`: `1px` line border, `--surface-soft` background,
  `8px` radius, `3px` gap/padding, inline-flex, horizontal overflow;
- category buttons: minimum height `30px`, body font size, `6px` radius,
  horizontal padding `14px`, weight `500`;
- active category: segmented-pill text/background/shadow, weight `600`;
- `.automation-card-header h3`: `13px`, weight `650`, line-height `1.3`;
- card description: footnote size, muted, line-height `1.35`;
- status pills: `1px` line border, soft surface, caption size, `5px` radius,
  `2px 7px` padding;
- state chips: caption size, `5px` radius, `2px 8px` padding, with distinct
  success/failed/running token treatments;
- `.automation-card-summary`: footnote size, wrapped flex, gaps `4px 12px`;
- `.automation-config-body`: grid with `9px` gap;
- compact config switches: `32 x 18px` track, `12px` thumb;
- `.automation-form-grid`: two equal columns with `8px` gap, collapsing to one
  column under the existing responsive rule;
- `.automation-status-row`: label/value grid with minimum label width `86px`
  and `10px` gap;
- `.automation-task-list`: grid, `6px` gap, maximum height `240px`, scrollable;
- `.automation-task-row`: `1px` line border, footnote size, `6px` radius,
  `7px 8px` padding;
- Soldier Training fields: auto-fit grid with minimum `130px` columns and
  `12px` gap;
- Trade Station panel spans the full Automation grid and its segmented tabs use
  `30px` minimum-height buttons and `4px 12px` padding.

Hover, active and selected CSS is statically exact. Pixel-level post-auth visual
validation remains blocked.

## Runtime gap

`BLOCKED` / not visually validated:

- rendered Automation geometry in the reference post-auth shell;
- effective selected top-level and Trade Station tabs from persisted state;
- actual enabled/disabled toggle values;
- game/config-derived counts, statuses and item lists;
- dynamic translated building/item names;
- live hover/focus appearance;
- page screenshot and pixel comparison.

No Automation feature/action control was invoked during this campaign. No IPC,
provider request or gameplay semantics were traced.

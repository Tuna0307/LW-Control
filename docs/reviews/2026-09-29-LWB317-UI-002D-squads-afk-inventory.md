# LWB317-UI-002D — Squads / AFK UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime Squads / AFK observation is `BLOCKED` by the out-of-scope LWBridge
authorization boundary. This stage uses static `EXACT_BYTES` frontend evidence
only. No AFK, rally, garrison, equipment-apply or other gameplay/action control
was invoked or behavior-traced.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/SquadPanel-HC3-DJei.js`

- recovered size: 192,855 bytes;
- SHA-256: `ED466E353AA896D8C3FC783FF1802930156FB9E9EB54327AEF73F2FF86FA06C7`;
- `SquadPanel` implementation begins near byte `0x2EB51`;
- top-level tab strip begins near byte `0x2EEB3`;
- export is near byte `0x2F13E`.

The chunk imports the exact recovered main bundle, Automation card component and
game-asset image helper.

Machine-readable evidence:

`evidence/lwbridge-0.3.17/ui/pages/squads-afk/static-inventory.json`

SHA-256 at creation:

`A64DA3D229AA25B2C87BC29103C21173520A6E256CEC436BAF895A27C2EA6C53`

It records six source identities, 184 directly referenced exact English locale
entries, 109 direct class-name literals, the tab contract and grouped locale
copy.

## Page title and top-level tabs

Exact page title:

`Squads / AFK` (`squad.title`).

The tab array is exported by the exact main bundle at byte `0x5783B` as:

1. `afk` — `AFK Tasks`
2. `equipment` — `Equipment Schemes`

The exact default is the first item, `afk`, established at main-bundle byte
`0x57852`.

The top-level panel renders a `Refresh` button only while Equipment Schemes is
active. Effective persisted/runtime tab state is not visually proven.

## AFK Tasks inventory

The AFK surface contains directly recovered UI for the following major areas.

### Shared AFK profiles

Exact copy includes:

- `Shared AFK Profiles`;
- `Monster AFK master switch`;
- `Enable or pause all monster AFK profiles below. Alliance Drill and Auto Garrison are controlled separately.`;
- `New AFK Profile` / `Edit AFK Profile`;
- `Profile Name`;
- `AFK Target`;
- `Target name keyword` / `Custom target`;
- `Squad Assignments`;
- `Basic settings`;
- `Execution settings`;
- `Target filters`;
- `Rally join conditions`.

Direct profile options include `Actively attack targets`, `Continuous attacks
(no return)`, `Join next rally while returning`, and `Join Alliance rallies`.
Level/distance filtering includes `Limit level`, `Limit distance`, minimum and
maximum level, `Any level`, `Any distance`, and progressive-level copy.

The UI includes an exact execution-count label:

`Execution count (0 = unlimited)`

with the explanatory copy that zero loops indefinitely.

### Rally join settings

Direct UI strings include:

- `Join timing`;
- `Random position` / `Random delay`;
- `Delay after reaching the position (s, 0 = none)`;
- `Maximum wait to departure (min, 0 = unlimited)`;
- `Rally leader list`;
- `Skip leaders with one hero`;
- `Do not rejoin after being removed`;
- `Select alliance members`;
- `Search name or UID`;
- `Confirm selection`.

These are visible static configuration controls only. Join timing/runtime
behavior is outside this stage.

### Alliance Drill

Exact title/description:

- `Auto Alliance Drill`;
- `Participate in squad order; the first available squad launches rallies when enabled.`

Additional static copy includes `Launch rallies`, `Launch priority: {order}`,
`Select at least one Alliance Drill squad.`, and waiting/launching/joining
status labels.

### Auto Garrison

Exact title/description:

- `Auto Garrison`;
- `Keep selected alliance buildings and allies reinforced.`

The settings UI statically contains Alliance buildings, Alliance Center,
Adjacent building, selected allies, target priority, drag-to-reorder,
garrison-squad order, current assignments, recall-on-disable and `Refill now`.
The ally picker contains `Search player name`, Online/Offline status and
cross-server/season-ended unavailable copy.

### Zombie Bus Garrison

Exact title/description:

- `Zombie Bus Garrison`;
- `Keep the strongest squad home; prioritize gold buses. Recall after confirmed battle completion, or after 90 seconds.`

Visible status copy includes `Waiting for an attack`, `Gold bus`, `Regular bus`
and `Unknown`.

### Stamina-item controls

The AFK toolbar directly uses:

- `Use Stamina items automatically`;
- `Use below Stamina`;
- `Prefer 50-point item`;
- `Uses stamina items whenever current stamina falls below the limit.`

Runtime values are loaded/config-derived and therefore not claimed as fixed
defaults.

## Equipment Schemes inventory

The second tab is a separate equipment-preset editor. Exact direct copy
includes:

- `Equipment presets`;
- `Preset name` and fallback `Preset {number}`;
- `No equipment presets`;
- `Create an equipment preset first.`;
- `Save configuration`;
- `Save and apply to squads`;
- `Read current equipment`;
- `Apply this squad`;
- `Currently active: {name}`;
- `Mixed presets`;
- `No matching preset`;
- `Position {number}`;
- `Hero stays in this position`;
- `Drag equipment set`;
- `Drag squad equipment`;
- `Empty`;
- `Alt+1–4 applies the matching scheme across all squads.`.

The direct drag hint says heroes remain fixed while shared leading-position
equipment can be swapped by dragging, with extra positions unchanged.

Exact static progress/result states include preparation, applying, verifying,
success, partial and rejected result copy. These strings establish the visual
states only; no apply action was triggered.

## Exact tab/layout styling

CSS source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`

Direct rules establish:

- `.squad-panel`: grid with `10px` gap;
- `.squad-header`: flex, spaced between, `10px` gap;
- `.squad-tabs`: segmented control with `1px` border, soft background, `8px`
  radius, `2px` gap, `3px` padding;
- tab buttons: minimum height `28px`, `4px 12px` padding, `6px` radius;
- active tab: segmented-pill background/text/shadow and weight `600`;
- AFK main layout: two columns, `minmax(280px,.8fr)` and
  `minmax(420px,1.2fr)`, `12px` gap;
- AFK toolbar: four equal columns with `8px` gap;
- compact AFK cards: `8px` radius, `6px 10px` padding, selected state uses
  blue border/accent background;
- compact switches: `32 x 18px` with `12px` thumb;
- shared-profile/editor panels: soft surface, `1px` border, `8px` radius,
  `12px` padding;
- AFK basic/number/check grids: two columns under normal width;
- garrison settings: two-column `420px/300px` minimum layout, collapsing to
  one column responsively;
- garrison target/runtime panels: `9px` radius and `10px` padding;
- Equipment preset layout: `210px` rail plus flexible main pane, `12px` gap;
- preset rail/main: `10px` radius;
- equipment squad position grid: five columns with minimum `132px` each;
- equipment icons: `34 x 34px`; preset slots use `28 x 28px` icons;
- equipment toast/progress overlays are fixed at bottom-right with raised
  surface/shadow styling;
- responsive rules collapse the AFK, garrison and equipment layouts to single
  columns where appropriate.

## Defaults and runtime gaps

Directly proven default:

- top-level tab: `AFK Tasks`.

Most enabled switches, profile selections, saved equipment presets, AFK target
choices, squad assignments, current equipment, game/alliance lists and status
values are configuration/game-derived. Their effective runtime defaults are
`UNKNOWN` rather than inferred from fallback code.

`BLOCKED` / not visually validated:

- rendered page geometry/theme;
- persisted tab/profile selection;
- all current squads/heroes/equipment imagery;
- enabled/disabled AFK/garrison/zombie-bus states;
- current alliance targets/members;
- live focus/hover/drag states;
- page screenshots and pixel comparison.

No backend/gameplay function recovery was performed.

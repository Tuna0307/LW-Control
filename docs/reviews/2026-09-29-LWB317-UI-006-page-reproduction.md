# LWB317-UI-006 — inventoried page reproduction

Date: 2026-09-29

## Result

All eight statically evidenced visible 0.3.17 routes now render in the separate
`src/LWBridge.UI-0.3.17` clone:

1. Home
2. Automation
3. Map Data
4. Squads / AFK
5. City Layout
6. Hotkeys
7. Mini Games
8. Settings

The implementation uses the exact recovered stylesheet from UI-003/UI-005 and
the exact route/order/icons from UI-005. Page copy and component families come
from the page inventories UI-002A through UI-002H.

This stage is `IMPLEMENTED_NOT_VALIDATED`. The clone itself was built and
smoke-tested, but direct post-auth reference comparison remains blocked by the
out-of-scope authorization boundary.

## State policy

No runtime/game result is presented as observed reference state.

- Home uses its exact unresolved setup copy: `Checking game setup…`.
- Automation uses the exact disconnected status: `Game disconnected. Actions
  are disabled.` The cards use exact labels/descriptions and deterministic
  disabled clone fixtures where loaded persisted configuration is unavailable.
- Map Data uses exact unloaded/manual-scan structure; unknown result/count
  values render with the recovered unloaded `—` convention and the exact empty
  state `No saved data for this type.`.
- Squads / AFK uses the exact default `AFK Tasks` tab and exact no-profile copy
  `No AFK profiles`; runtime assignment/config values remain disabled fixtures.
- City Layout uses the exact disconnected render path from the recovered
  component: `The game is disconnected, so the city layout cannot be loaded.`
  It does not fabricate a city grid/building population.
- Hotkeys renders all seven exact recovered cards and exact disconnected status.
  Their generic persisted toggle state is a deterministic disabled clone fixture;
  the relocation cards retain the directly documented disabled-by-default text.
- Mini Games renders the four inventoried helper/card areas and exact
  disconnected status without invoking any helper.
- Settings uses the exact initial `Processing` performance state, exact Issue
  feedback copy and static Client Update shell; no export/update call is made.
  The entitlement-dependent Account interaction section is not fabricated.

All gameplay/backend/action controls that could cause effects are inert in this
Phase 1 preview.

## Clone interaction smoke test

The authorized Desktop browser loaded the Vite preview and navigated the routes.
Observed clone DOM confirmed:

- Home initial route and exact `Game Setup` heading;
- Automation default `Daily Tasks` plus all seven exact category labels and the
  eight exact Daily card titles;
- Map Data default `Manual Scan` and `City`, eight exact scan-content labels,
  nine exact data tabs and unloaded-count markers;
- Squads / AFK default `AFK Tasks` and Equipment Schemes alternate tab;
- City Layout exact disconnected copy;
- Hotkeys exact seven-card order;
- Mini Games exact four-card/helper order;
- Settings exact title/subtitle, performance, Issue feedback and Client Update
  sections.

No new React console errors were observed during the route smoke pass. The only
browser error in the captured console history was an older favicon 404 from
before UI-005 added the inline empty favicon.

## Screenshot evidence

Eight fixed-viewport clone captures are stored under:

`evidence/lwbridge-0.3.17/ui/clone/pages/`

The evidence README records each file's SHA-256. These captures use the exact
recovered CSS and show the implemented static/default/disconnected states.

## Known visual gaps carried into UI-007

The captures already identify concrete clone-side refinement work without
requiring reference-runtime access:

- Squads / AFK uses several generic wrapper classes around exact subcomponents;
  recovered SquadPanel selectors should replace those where available.
- Equipment Schemes has only the evidence-safe empty configuration surface and
  needs its exact recovered rail/main wrapper classes.
- Map Data's exact selector hierarchy/order should be checked against the
  recovered component now that its screenshot exposes overflow/layout details.
- Automation specialized cards and shared generic cards should be checked for
  exact recovered class nesting.

These are clone-vs-static-contract fixes. A direct reference screenshot/pixel
comparison remains legitimately blocked.

## Validation

Before the UI-006 checkpoint:

- `npm.cmd run check` — passed;
- `npm.cmd run build` — passed;
- Desktop route smoke test — passed;
- eight clone screenshots — captured;
- `git diff --check` — passed;
- `src/LWBridge.Desktop` remains untouched.

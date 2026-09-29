# LWB317-UI-002A — Home UI inventory

Date: 2026-09-29

## Scope and evidence state

Runtime page observation is `BLOCKED` because the exact LWBridge 0.3.17
reference opens at the out-of-scope authorization/login boundary documented by
`LWB317-UI-001B`.

This page inventory therefore uses only `EXACT_BYTES` recovered frontend
evidence from `LWB317-UI-001A`. It is a static UI contract, not runtime visual
proof and not function recovery.

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Static route/default-page evidence

Source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-BVfnK1wp.js`

- byte offset `0x58397`: the top-level active page state initializes to
  `overview`;
- byte offset `0x5B222`: the `overview` activity renders component `qr`;
- `LWB317-UI-001A` already established
  `overview -> nav.overview -> Home` from the exact navigation and English
  locale bytes.

Therefore Home is the statically configured initial post-auth top-level page.
This does not prove it was visually reached in the campaign runtime session.

## Exact component structure

Home's component begins at byte offset `0x52336` in the recovered main bundle:

`function qr({ ... })`

The component renders one section with classes:

`panel quick-actions-panel`

Its direct structure is:

1. `panel-title`
   - `h2` title `setup.title`;
   - one status text element, class `status-ok` only when the game is running
     and the bridge is online, otherwise `muted`;
2. conditional game-root or game-control row;
3. conditional action error text using class `game-root-error`;
4. a switch row for `auth.autoLaunchGame`;
5. a switch row for `automation.autoReconnect.title`;
6. conditional recovery-failure detail text using class `game-root-error`.

There are no Home sub-tabs, tables, filters or free-form input fields in this
component.

## Exact English visible strings

Source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/en-BisSXcTB.js`

Key source locators include:

- `setup.title` at byte offset `0xD0BF`;
- `auth.autoLaunchGame` at byte offset `0x302E`;
- `automation.autoReconnect.title` at byte offset `0x4B21`.

Exact strings used by the Home component are:

| UI use | Locale key | Exact English string |
|---|---|---|
| Panel title | `setup.title` | `Game Setup` |
| Initial unresolved status | `setup.checking` | `Checking game setup…` |
| Root unavailable status | `setup.gameRootMissing` | `Not detected` |
| Launching status | `setup.launchingGame` | `Launching game…` |
| Busy status | `common.processing` | `Processing` |
| Repair status | `setup.repairRequired` | `Game connection needs repair` |
| Running status | `status.gameRunning` | `Game running` |
| Bridge-disconnected status | `setup.bridgeDisconnected` | `Game bridge is not connected` |
| Stopped status | `setup.gameStopped` | `Game stopped` |
| Root chooser button | `setup.gameRootSelect` | `Choose Folder` |
| Launch button | `top.launchGame` | `Launch Game` |
| Repair/restart button | `setup.updateAndLaunch` | `Update and restart game` |
| Stop button | `setup.closeGameAction` | `Close game` |
| Repair note | `setup.updateCloseGame` | `The game will close automatically, update the component, and restart through the official launcher.` |
| Startup switch | `auth.autoLaunchGame` | `Open games at startup` |
| Reconnection switch | `automation.autoReconnect.title` | `Automatic Reconnection` |
| Recovery failure prefix | `recovery.failedDetail` | `Reason: {error}` |

The recovery-state status line can also resolve to these exact strings:

| Locale key | Exact English string |
|---|---|
| `recovery.state.waiting` | `Waiting for the game to restart…` |
| `recovery.state.updating` | `Waiting for the game update to finish…` |
| `recovery.state.repairing` | `Repairing the bridge component after the game update…` |
| `recovery.state.launching` | `Starting the game through the official launcher…` |
| `recovery.state.verifying` | `The game is online; verifying connection stability…` |
| `recovery.state.maintenance` | `The game server is under maintenance or temporarily unavailable. Next retry: {time}.` |

Dynamic error-code translations are state-dependent and are not claimed as
default visible copy.

## Controls and state variants

Static rendering rules directly establish these UI variants:

- before game-root state resolves, the status text is
  `Checking game setup…` and no root/control row is rendered;
- when a resolved root is invalid, a `game-root-missing` row shows the status
  plus a `Choose Folder` primary button;
- when the root is valid, a `game-controls` row conditionally contains
  `Launch Game` and either `Close game` or `Update and restart game`;
- the repair/restart variant additionally shows the repair note;
- the two switch rows are always part of the Home component, but their checked
  values come from loaded application/profile state, so fixed on/off defaults
  are `UNKNOWN` rather than guessed;
- button disabled states depend on the loaded busy/root/running/recovery state
  and are therefore not claimed as one fixed runtime default.

No feature/action control was pressed during this campaign.

## Exact CSS contract used by Home

Source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`

Relevant locators include `:root` at byte `0x501`,
`.quick-actions-panel` at `0x5716`, and `.toggle-row` at `0x857C`.

Recovered rules directly establish:

- `.main-view`: grid, `gap:14px`, `padding:14px`, `min-width:0`;
- common `.panel`: `padding:16px`, grid `gap:14px`, `1px` border using
  `--line`, `--surface` background, `0 4px 14px var(--shadow)` shadow,
  `10px` radius;
- `.panel h2`: `16px`, weight `650`, zero margin;
- `.quick-actions-panel`: `gap:18px`;
- `.panel-title`: grid with `gap:4px`;
- `.game-controls,.game-root-missing`: flex, centered items, `gap:10px`;
- `.game-controls button`: minimum width `120px`;
- `.game-root-missing`: `1px` `--line` border, `--surface-soft`
  background, `9px` radius, `padding:12px`, `space-between` layout;
- `.toggle-row`: `--line` border, `--surface-soft` background,
  `min-height:48px`, flex row aligned center and spaced apart;
- default `.ui-switch`: `64 x 32px`; thumb `24px`; `4px` inset gap;
  `999px` radius; off background `--surface-muted`; on background
  `--switch-on`;
- `.game-root-error`: `--danger-text`, `--font-footnote`;
- `.status-ok`: `--success-text`.

The recovered root theme tokens define light values including:

- `--surface:#fff`;
- `--surface-soft:#ebebed`;
- `--line:#e5e5ea`;
- `--text:#1d1d1f`;
- `--muted:#5f5f63`;
- `--blue:#0071e3`;
- `--success-text:#1b7a33`;
- `--danger-text:#d70015`;
- `--switch-on:#34c759`.

A complete dark token set is also present. Because the post-auth page was not
runtime-observed, the campaign does not claim which theme would have rendered
Home in the blocked session.

## Runtime gap

`BLOCKED`:

- rendered Home geometry inside the real post-auth window;
- actual selected theme on Home;
- runtime switch values;
- actual status/game-root state;
- hover/focus rendering in the reference;
- page screenshot and pixel comparison.

These gaps are deliberately left open rather than bypassing auth or converting
static evidence into visual proof.

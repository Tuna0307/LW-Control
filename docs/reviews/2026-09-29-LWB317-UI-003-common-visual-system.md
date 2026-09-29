# LWB317-UI-003 — common visual system

Date: 2026-09-29

## Scope and evidence state

This stage consolidates only visual facts already established by the exact
recovered LWBridge 0.3.17 frontend and the completed UI inventories. It is
static `EXACT_BYTES` evidence. Post-auth runtime visual validation remains
`BLOCKED` by the out-of-scope authorization boundary.

Reference SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

Primary visual source:

`evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/index-rIL9Fpht.css`

- 126,217 bytes;
- SHA-256 `3D87E9F65B39EACE6A1A254BFC90A38ACB72613D1FFF7236C7CEE7AB9BFAF545`;
- light `:root` begins near byte `0x501`, as recorded in the Home inventory;
- a complete `[data-theme=dark]` token override is present in the same exact
  stylesheet.

Machine-readable consolidation:

`evidence/lwbridge-0.3.17/ui/common-visual-system/static-visual-system.json`

## Typography

The base body stack is exactly:

`Segoe UI, -apple-system, BlinkMacSystemFont, Arial, sans-serif`

The shared font-size tokens are:

- caption `11px`;
- footnote `12px`;
- body `13px`;
- title `16px`;
- page title `20px`.

Navigation indices and Hotkeys bindings use `Consolas, monospace` variants.
Panel `h2` headings are `16px` with weight `650`; the top-bar `h1` is `16px`
with weight `700`.

No bundled custom font file exists in the recovered web asset set, so the clone
should use the exact system-font stack rather than introducing a new font.

## Theme tokens

The exact light palette includes:

- background `#f5f5f7`;
- surface `#fff`;
- soft surface `#ebebed`;
- muted surface `#e5e5ea`;
- line `#e5e5ea`, bright line `#d1d1d6`;
- text `#1d1d1f`, muted `#5f5f63`, muted-strong `#48484a`;
- blue `#0071e3`, blue-bright `#06c`, blue text `#0071e3`;
- success text `#1b7a33`;
- danger text `#d70015`;
- warning text `#c93400`;
- switch-on `#34c759`.

The exact dark override includes background `#1e1e1e`, surface `#282828`, soft
surface `#323232`, line `#3a3a3c`, text `#f5f5f7`, muted `#a8a8ad`, blue text
`#2997ff`, success text `#30d158`, danger text `#ff554c`, warning text
`#ff9f0a`, and switch-on `#30d158`.

Because the post-auth reference shell was not reachable, the active theme in
that blocked runtime state remains `UNKNOWN` even though both exact token sets
are recoverable.

## Shell and navigation geometry

Static CSS establishes:

- `.app-shell`: two grid rows (`auto 1fr`), `10px` gap, `100vh`,
  `10px 12px` padding and hidden overflow;
- `.top-bar`: minimum height `52px`, `1px` bright-line border, `10px` radius,
  `6px 14px` padding, `12px` gap and `0 4px 16px var(--shadow)` shadow;
- normal `.app-layout`: `230px 164px minmax(460px,1fr)` with `10px` gap;
- collapsed profile layout: `92px 164px minmax(460px,1fr)`;
- single-profile layout: `164px minmax(460px,1fr)`;
- `.side-nav`: grid with `5px` gap and `10px` padding;
- nav buttons: minimum height `38px`, `8px` radius, `8px 12px` padding,
  `13px` size and `10px` content gap;
- `.main-view`: grid with `14px` gap/padding and scrollable overflow;
- common shell surfaces use `1px` line borders, surface background,
  `10px` radius and `0 4px 14px var(--shadow)`.

Navigation icons are `17 x 17px`; their SVGs use current-color stroke,
round caps/joins and stroke width `1.8px`. Hover scales the icon to `1.08`.
Selected navigation uses blue text/accent background, a left `3px` marker and
an accent-glow drop shadow. The nav index reserves at least `20px` and uses
caption-size Consolas.

The only live window geometry established in UI-001B is the auth-boundary
client size `1120 x 720` (Win32 client rect) and `1136 x 759` outer window.
Those measurements are not promoted to a proven post-auth shell size.

## Panels, cards and spacing

The common `.panel` contract is `1px` line border, surface background,
`10px` radius, `0 4px 14px var(--shadow)`, `14px` gap and `16px` padding.
`.panel-title` is a `4px`-gap grid with muted body-size subtitle text.

Recovered page inventories add evidence-backed variants:

- Hotkeys cards: `8px` radius, `12px 14px` padding, shared card shadow;
- update/settings panels: `10px` radius, `12px` gap, `16px` padding;
- City Layout center/inspector/footer: `10px` radius;
- map search/scheduled panels and many automation/squad cards use `7–10px`
  radii as recorded in their page inventories.

There is no evidence for one universal radius beyond these explicit class
rules, so the clone should retain the class-specific values.

## Buttons, inputs and switches

Base `button`, `input`, `select` and `textarea` controls inherit the app font,
use surface/text colors, a `1px` line border, `8px` radius and `8px` padding.

Exact shared states include:

- hover: brighter border plus soft-surface background;
- active: translate `1px` and scale `.985` for base buttons;
- primary: white/on-accent text, blue border/background, weight `600`;
- primary active: scale `.97`;
- danger: white/on-accent text with danger border/background;
- focus-visible: `2px solid var(--blue)` with `2px` outline offset;
- disabled button: opacity `.45`, `not-allowed` cursor;
- checkbox/radio: `15 x 15px`, blue accent.

The base `.ui-switch` is `64 x 32px` with a `24px` circular thumb, `4px` gap,
`999px` track radius and muted-surface off state. The on state uses the exact
green switch tokens. Compact page-specific switch sizes exist and are retained
where their selectors explicitly override the base dimensions.

## Tabs, tables and grids

The completed inventories establish a recurring segmented-tab family rather
than one universal class: Map Data scan/data tabs and Squads tabs use a
bordered soft-surface strip with `8px` outer radius, small internal padding/gap,
`6px` button radii, and selected segmented-pill background/text/shadow.
Automation category tabs use their own exact selector values.

The Map Data inventory establishes the principal table treatment: fixed layout,
`1px` line border, `8px` radius, shadow, base minimum width `720px`, with wider
minimums for Truck/Train and Secret/Ghost/Scheduled datasets. Row cells use a
top border and compact padding. Other pages use explicit grid/card layouts
rather than an invented generic table style.

## Reusable assets

The recovered web package contains three direct PNG assets usable by the UI:

- `dot-offline-CDBA2902.png`;
- `dot-online-gjSbTzBh.png`;
- `icon-warning-D9WXqS3t.png`.

Game-specific images are supplied through the recovered `GameAssetImage`
helper and runtime asset paths; those dynamic images must not be fabricated for
static default-state reproduction.

## Responsive behavior

The stylesheet contains exact responsive breakpoints at `1160`, `1100`, `900`,
`860`, `780`, `760`, `600` and `560px`. Shell behavior includes narrower
profile/nav columns around `1100px` and a one-column app layout at `760px`, with
the side navigation becoming a two-column grid. Page-specific responsive rules
from the inventories remain authoritative for their own layouts.

## Visual-validation gap

Static hover/focus/selected/disabled rules are exact bytes, but the campaign
cannot visually compare those post-auth rendered states against the reference
without crossing the excluded authentication boundary. The clone stages may
implement this exact static system, but final visual evidence must distinguish
`IMPLEMENTED_NOT_VALIDATED` from runtime-proven parity.

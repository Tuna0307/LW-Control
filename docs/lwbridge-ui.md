# LWBridge 0.3.17 UI parity plan

This is the master UI workstream document.

## Goal

Copy the observable **in-scope post-auth LWBridge 0.3.17 UI** one-for-one before broad function implementation.

The original login/account/licensing experience is not part of the clone target.

If the reference opens into a login/locked state, capture only enough evidence to document the access boundary. Do not implement that screen in the clone and do not bypass it.

## Required inventory

For every accessible in-scope screen/state, capture or record:

- window size/minimum behavior;
- navigation hierarchy;
- tab order;
- labels and exact copy;
- icons/assets;
- colors;
- typography;
- spacing/padding;
- borders/radii/shadows;
- tables and columns;
- filters;
- buttons and disabled states;
- toggles/defaults;
- empty/loading/error states;
- dialogs/popovers/tooltips;
- theme/language behavior;
- responsive behavior;
- keyboard/focus/hover/selected states where observable.

## Auth-boundary rule

Login/account/licensing is not a page-reproduction target.

If encountered:

- record the visible boundary and what in-scope surfaces are inaccessible;
- use legitimate owner-provided/existing access only if available;
- inspect auth-related local state contracts if required by the assigned UI
  predicate, under `AGENTS.md` section 6; do not circumvent original access controls;
- static frontend evidence may still be used to inventory post-auth UI, but any runtime visual state not directly observed must remain `UNKNOWN` / not visually validated.

## Evidence layout

Future UI findings should use:

`docs/reviews/YYYY-MM-DD-LWB317-UI-###-short-title.md`

Durable screenshots/captures should go under:

`evidence/lwbridge-0.3.17/ui/`

Do not overwrite the original reference executable.

## Build policy

The UI implementation may reuse exact recovered frontend assets where lawful/technically practical.

Synthetic backend data is allowed only in an explicitly labeled preview/test harness. It must not be confused with recovered runtime behavior.

The new clone should enter the in-scope app experience without reconstructing LWBridge's original login/account/licensing system.

## Acceptance

An in-scope page is visually complete only after repeatable comparison against the 0.3.17 reference in the same observable state.

Pixel/geometry comparison should be preferred over subjective statements such as “looks close.”

If runtime visual comparison is blocked by auth, mark that validation gap explicitly rather than bypassing the boundary.

## Current work

`LWB317-UI-001A` established the 0.3.17 frontend package statically. The exact
reference contains a 24-record Brotli-compressed Tauri asset table in `.rdata`;
all 24 web assets were losslessly recovered and hashed under
`evidence/lwbridge-0.3.17/ui/frontend-package/`.

Detailed evidence:

`docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`

This is an `EXACT_BYTES` package baseline only. It does not claim visual parity,
runtime-state parity, or any recovered gameplay/backend behavior.

`LWB317-UI-001B` runtime observation on 2026-09-29 reached the original
authorization/login boundary before the post-auth shell. The boundary was
captured without submitting credentials or bypassing entitlement, and the
post-auth shell/navigation runtime baseline is therefore `BLOCKED`. The active
campaign may continue with its separately authorized static/offline UI stages;
static frontend evidence must not be described as runtime visual proof.

`LWB317-UI-002A` through `LWB317-UI-002H` have now inventoried Home,
Automation, Map Data, Squads / AFK, City Layout, Hotkeys, Mini Games and
Settings from exact recovered frontend bytes. `LWB317-UI-003` consolidates the
shared light/dark tokens, typography, shell geometry, navigation states,
controls, cards, tabs, tables and responsive rules. These are static contracts;
post-auth rendered visual comparison remains blocked by the same auth boundary.

`LWB317-UI-004` created the clean separate React/Vite project at
`src/LWBridge.UI-0.3.17/`. `LWB317-UI-005` reproduces the exact statically
recoverable shell/navigation contract and `LWB317-UI-006` implements the eight
effective visible routes with evidence-safe static/loading/empty/disconnected
states. The original login/account/licensing experience remains absent by
design, and gameplay/backend actions are not wired in this Phase 1 preview.

`LWB317-UI-007` completed the available exact-component/CSS comparison pass and
fixed concrete clone hierarchy/state deviations. Before/after clone evidence is
under `evidence/lwbridge-0.3.17/ui/visual-comparison/`. The stage remains
`PARTIAL` solely because repeatable direct comparison against the original
post-auth runtime is blocked by the auth boundary. No pixel-parity claim is made
without that reference state.

The static UI campaign was accepted and Map function recovery subsequently
opened. The canonical frontend is now the normal packaged Desktop UI. This
acceptance covers the static baseline, not every conditional state or original
runtime pixel parity.

The 2026-10-01 takeover audit found Home still implements only its unresolved
state, despite the exact inventory establishing folder/launch/close/repair and
loaded/recovery variants. The next UI-only task is
`work-items/LWB317-UI-HOME-STATES-001.md`; native Home integration remains separate.
The shell language selector currently changes selection only; labels remain
English. These UX gaps remain visible rather than treating route smoke checks
as a complete one-for-one clone. The existing Map implementation is preserved;
its Goal remains awaiting independent project-lead closeout review.

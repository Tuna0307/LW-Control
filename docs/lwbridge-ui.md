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
- do not reverse engineer or bypass auth to continue;
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

The next runtime work begins with shell/navigation observation and then the pre-authorized UI-only Loop campaign.

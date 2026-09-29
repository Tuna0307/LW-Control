# LWBridge 0.3.17 UI parity plan

This is the master UI workstream document.

No UI reproduction has started under the 0.3.17 reset yet.

## Goal

Copy the observable LWBridge 0.3.17 UI one-for-one before broad function implementation.

## Required inventory

For every screen/state, capture or record:

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

## Evidence layout

Future UI findings should use:

`docs/reviews/YYYY-MM-DD-LWB317-UI-###-short-title.md`

Durable screenshots/captures should go under:

`evidence/lwbridge-0.3.17/ui/`

Do not overwrite the original reference executable.

## Build policy

The UI implementation may reuse exact recovered frontend assets where lawful/technically practical.

Synthetic backend data is allowed only in an explicitly labeled preview/test harness. It must not be confused with recovered runtime behavior.

## Acceptance

A page is visually complete only after repeatable comparison against the 0.3.17 reference in the same state.

Pixel/geometry comparison should be preferred over subjective statements such as “looks close.”

## First future task

The first UI worker should inventory/capture the top-level app shell and navigation only.

Do not implement backend functions in that work item.

## Static frontend package baseline

`LWB317-UI-001A` established the 0.3.17 frontend package statically. The exact
reference contains a 24-record Brotli-compressed Tauri asset table in `.rdata`;
all 24 web assets were losslessly recovered and hashed under
`evidence/lwbridge-0.3.17/ui/frontend-package/`.

Detailed evidence:

`docs/reviews/2026-09-29-LWB317-UI-001A-frontend-package-inventory.md`

This is an `EXACT_BYTES` package baseline only. It does not claim visual parity,
runtime-state parity, or any recovered gameplay/backend behavior.

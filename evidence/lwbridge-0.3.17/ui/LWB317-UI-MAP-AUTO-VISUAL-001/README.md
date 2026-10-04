# Map Auto Scan card offline visual comparison

AWAITING_REVIEW. This packet compares only `.map-auto-scan-card`; no production
code changed and no Run now, scan, provider/native or gameplay action was invoked.

## Result

The actual recovered `MapDataPanel-B4GXEND2.js` component body and actual current
`MapDataPage.jsx` were executed through the existing INTERACTIONS and
FILTER-LIFECYCLE harnesses. Both entered Auto through the rendered Auto-tab
callback. The recovered `Vr(name="remove")` icon function from the original index
asset is also executed so configured server-chip geometry does not depend on the
original harness's inert Icon stub.

Two pairs only were produced. Default EN/light at 1280x720 is structurally,
markup and pixel identical. Configured JA/dark at 375x1000 matches all labels,
values, checked/disabled states, server IDs, type choices, card/grid/chip/button
layout and every measured anchor except the child remove glyph inside each of the
three server-chip buttons.

The one recorded product difference is the remove glyph implementation. Original
card bytes 46001-48950 invoke `se{name:"remove"}`; recovered index `Vr` begins at
UTF-8 byte 330489 and renders `<svg class="ui-icon">` with path
`m4 4 8 8M12 4l-8 8`. Current `MapDataPage.jsx` Auto card begins at UTF-8 byte
46645 / line 1011 and its chip child at byte 48425 / line 1041 is
`<span aria-hidden="true">×</span>`. Full exact hashes and slices are in
`pinned-inputs.json`.

At 375 CSS px, each original SVG child measures 14x14; each current text child
measures 7.59375x13. The three children account for all 12 geometry deltas
(x/y/width/height) and all 9 style deltas (width/height/overflow). Their parent
chips and buttons remain identical, as do the 341x650 card, 315x302 grid,
315x32 chip area, 315x105 type group and 315x39 Run-now button. The configured
PNG bytes therefore differ; the default PNG pair is identical. Fresh browser
console issues: zero. All four screenshots are recorded as inspected in
`browser/inspection.json`.

The configured sample is source-valid: enabled, 90-minute interval, servers
8/15/120, types city/truck/treasure, normal speed, no return-to-original, and a
positive next-run deadline. It normalizes unchanged through current
`normalizeAutoScanConfig`; its IDs/types also satisfy the recovered parser/kind
set pinned by AUTO-CONFIG-001.

## Reproduce

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-VISUAL-001/compare-renderers.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-VISUAL-001/capture-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-VISUAL-001/validate-evidence.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/check-protected-wip.mjs
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build
git diff --check
```

The browser helper owns ports 4340/4341 and a task-named temporary Chrome
profile; owner port 4335 is untouched. Original uses recovered CSS; current uses
`reference.css` then `styles.css` in production import order.

Final required checks pass: canonical frontend `check`, production-package
integrity `check:production-build`, the HOME-PREFERENCE-LIFETIME-001B ten-file
guard without `--record`, packet integrity, and `git diff --check`. The package
integrity hashes reported by the checker are
`865208f65150f67aa0f3a29cd8e6c0b97352181e68e7047cad47c6df87866ea2` and
`7f3bbbf98bd110e0b15b413a4e52d4d3c281ed076f856830cd64395fabb64a90`.

## Limits

This is isolated offline source-rendered evidence inside the same documented
panel container. It does not establish full-shell, protected-runtime or native
pixel parity. Current exact-recovered EN/JA catalogs are injected into both
renderers so translation input is held constant. The configured Run-now control
is deliberately enabled by the inert inputs but is never clicked. No conclusion
is extended to the Manual header, shared summary, browsing/filter/table surfaces
or any other Map section.

Review: `docs/reviews/2026-10-04-LWB317-UI-MAP-AUTO-VISUAL-001.md`.

# Milestone B — corrected toolbar presentation and local interactions

Milestone A preserved the assignment-start source and proved three in-scope
presentation drifts against the recovered `MapDataPanel-B4GXEND2.js` renderer.
Milestone B corrects only those recovered-source differences in
`MapDataPage.jsx`:

- normal Map table/query-error/pagination content is again inside the recovered
  `.map-search` grid;
- normal result totals use the original direct integer interpolation rather than
  locale grouping (`1202 items`, not `1,202 items`);
- Truck/Train/Secret Task plunderable labels restore the recovered
  `map-plunderable-filter` class.

`../milestone-a/compare-toolbars.mjs --live` executes the corrected production
source against the pinned recovered source and writes `current-manifest.json`,
`current-results.json`, and expanded raw HTML here. `verify-corrections.mjs`
keeps the baseline failures as proof and verifies the corrected recovered
structure/count/class while also asserting that accepted differences remain
visible: accessibility/type attributes, the current query-error banner,
missing-server Search disablement, and unavailable native/provider action fences.

`browser-interactions.mjs` mounts the actual Vite application against the
deterministic browser-only Map provider. Its 34 passing assertions cover City
encoded alliance/marked/keyword Search, Resource name/keyword mutual clearing,
retained-goods and Treasure `<details>` open/select/close behavior, Treasure
preference checkboxes, Secret Task status/level/quality/plunderable/delay inputs,
tab view-cache restoration, Search page reset, and first/last pagination button
predicates. It runs EN/light and JA/dark and records zero browser console/page
errors. No export, scan, schedule, share, claim, jump, mark, Run-now or gameplay
handler is invoked. The two Vite screenshots in `screenshots/` were visually
inspected; the corrected toolbar/table/pagination layout is coherent in both.

`locale-inventory.mjs` inventories the 45 assigned toolbar/pagination keys in
all nine production catalogs. Every catalog owns all 45 keys directly; no locale
edit or English fallback is required.

Milestone B checks:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-a/compare-toolbars.mjs --live
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-b/verify-corrections.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-b/browser-interactions.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-TOOLBAR-VISUAL-001/milestone-b/locale-inventory.mjs
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
```

The remaining raw differences are intentional/current product contracts or
presentation-equivalent wrappers and are carried into Milestone C rather than
normalized away.

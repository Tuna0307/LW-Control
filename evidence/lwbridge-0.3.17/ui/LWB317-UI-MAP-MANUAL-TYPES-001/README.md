# LWB317-UI-MAP-MANUAL-TYPES-001 evidence

This packet proves the focused MMV-BEHAVIOR-001 correction without invoking a
scan or any native action.

Reference identities were rechecked before recovery:

- `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`
  SHA-256 `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`.
- `evidence/lwbridge-0.3.17/ui/frontend-package/web/assets/MapDataPanel-B4GXEND2.js`
  SHA-256 `CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.
- The accepted parent packet validator printed
  `LWB317_MAP_MANUAL_VISUAL_EVIDENCE_OK` before this correction.

`baseline-selection-counter.json` is an exact copy of the accepted parent
pre-fix counter-evidence. It preserves the original `[]` versus current
`["city"]` result and is intentionally not regenerated from corrected code.

`check-manual-selection.mjs` executes the actual recovered original Manual
checkbox callback and the actual current `MapDataPage` callback. It proves:

- `city -> []` after deselecting the final Manual type;
- `[] -> [resource]` after selecting one type again;
- source append/filter ordering across additional add/remove/re-add steps;
- zero `onAutoScanConfig` calls from Manual editing;
- the Auto checkbox still has its separate last-type disabled predicate;
- the recovered original pure `rt([])` helper and current
  `buildStartPayload([], "fast")` both normalize an empty Start request to all
  eight scan types.

Run it with:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-TYPES-001/check-manual-selection.mjs
```

The result is `callback-results.json`, marker
`LWB317_MAP_MANUAL_TYPES_CALLBACK_OK`, seven matching original/current state
snapshots and zero Auto config edits. Source locators include original Manual
onChange UTF-8 byte 50135 and original pure normalizer UTF-8 byte 14384.

`capture-browser.mjs` launches a task-owned local Vite server on port 4340 and
one headless Chrome instance on debugging port 4341. It opens the existing
offline preview provider at `view=map-data`, clicks only the eight Manual
scan-type checkboxes, clears all eight, then selects Resource Point alone.
Start, Stop and Clear Map Data are never clicked. Durable output is under
`browser/`:

- `results.json` — marker `LWB317_MAP_MANUAL_TYPES_BROWSER_OK`, empty state and
  final Resource-only state;
- `console.json` — zero warning/error/exception entries;
- `manual-empty-to-resource.png` — inspected 1280x900 screenshot, SHA-256
  `B3372AE5D17CC907980DA31C2609C733B026F2CEDCDE19C09359D01AB3A85C03`.

Required validation passed on 2026-10-04:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-MANUAL-TYPES-001/check-manual-selection.mjs
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check
npm.cmd --prefix src/LWBridge.UI-0.3.17 run build
npm.cmd --prefix src/LWBridge.UI-0.3.17 run check:production-build
git diff --check
node evidence/lwbridge-0.3.17/ui/LWB317-UI-HOME-PREFERENCE-LIFETIME-001B/check-protected-wip.mjs
```

The production build/package hashes are
`865208F65150F67AA0F3A29CD8E6C0B97352181E68E7047CAD47C6DF87866EA2`
and `7F3BBBF98BD110E0B15B413A4E52D4D3C281ED076F856830CD64395FABB64A90`.
The protected-WIP guard reports ten of ten preserved.

Limits: this is source/local UI parity evidence. The focused browser run uses
the existing offline preview provider, and no native scan/runtime action is
invoked. Protected-original runtime pixels and live gameplay behavior remain
outside this work item.

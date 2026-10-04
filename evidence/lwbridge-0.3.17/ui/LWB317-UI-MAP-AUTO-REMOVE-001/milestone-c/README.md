# LWB317-UI-MAP-AUTO-REMOVE-001 milestone C

Milestone C selects four exact milestone-B Auto-card source renders and captures
their original/current browser pairs with the assigned language, theme and
viewport combinations:

- English/light recovered default at 1280x720;
- Japanese/dark configured waiting at 375x1000;
- Japanese/light Auto running at 375x1000;
- English/dark offline at 1280x720.

`prepare-browser-pairs.mjs` pins the same recovered source slices, current source
locators, fixed clock (`2026-10-04T09:15:00Z`), Asia/Singapore timezone, exact
dependencies and per-case inputs used by milestone B. The raw scoped Auto-card
markup is byte-exact in all four pairs, so `raw-comparison.json` records zero raw
differences before browser styling.

`capture-browser.mjs` uses task-owned ports 4344/4345 and the production CSS
order (`reference.css`, then `styles.css`). Across all four pairs it records exact
browser DOM, zero rectangle/geometry differences, zero computed-style differences,
zero console issues and byte-identical original/current PNGs. Screenshot hashes:

- `en-light-default`: `34e3f61e8bcba907cab550c64febbdc83f542b651e1616451a5088db454c47d7`;
- `ja-dark-configured`: `b9fa3c3e1eb8cf52e96c51354154323a94f386fc8764dca920ed717e7f8f1acd`;
- `ja-light-running`: `4e8d48fb3ff7255f68d25e5b1b7b03caf17ce30f3ccd2011b5890b04dfddbd6e`;
- `en-dark-offline`: `3df6195450b1782786382079cf00325b9217ee340928cb39b741e5e1d8e3d622`.

All eight PNGs were visually inspected and the observations are durable in
`browser/inspection.json`. No additional Auto-card presentation defect was proven,
so milestone C makes no production-code change.

Replay:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-c/prepare-browser-pairs.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-c/capture-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-c/validate-evidence.mjs
```

This proves the isolated source/local Auto configuration card only. It does not
claim full Map/shell parity or original native-runtime pixels, and no Run-now,
scan, native-provider or gameplay action was invoked.

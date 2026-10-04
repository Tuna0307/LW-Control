# LWB317-UI-MAP-AUTO-REMOVE-001 milestone A

Milestone A replaces only the Auto server-chip remove child with the exact recovered
0.3.17 SVG. The button label, button attributes, removal callback and existing
`ui-icon` CSS remain unchanged.

The source is pinned to recovered `MapDataPanel-B4GXEND2.js` Auto-card bytes
46001-48950 and recovered index `Vr` at UTF-8 byte 330489. `Vr(remove)` renders
`svg.ui-icon`, viewBox `0 0 16 16`, `aria-hidden=true`, `focusable=false`, with
path `m4 4 8 8M12 4l-8 8`.

`compare-renderers.mjs` executes the actual recovered MapDataPanel body and actual
current MapDataPage source using the accepted INTERACTIONS/FILTER-LIFECYCLE
harnesses. The configured Japanese/dark 375px case uses enabled Auto, servers
8/15/120, city/truck/treasure, 90 minutes, normal speed, no return and the fixed
Asia/Singapore clock `2026-10-04T09:15:00Z`. After the correction, the
presentational views and scoped markup are exact.

`capture-browser.mjs` renders that original/current pair with the recovered CSS and
current production CSS order. Both PNGs hash to
`b9fa3c3e1eb8cf52e96c51354154323a94f386fc8764dca920ed717e7f8f1acd`;
DOM, measured geometry and measured styles are exact and browser console issues
are zero. Both screenshots were manually inspected and recorded in
`browser/inspection.json`.

The accepted parent packet under `LWB317-UI-MAP-AUTO-VISUAL-001` is not modified;
its pre-fix text-`×` difference remains durable historical evidence.

Replay:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-a/compare-renderers.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-a/capture-browser.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-AUTO-REMOVE-001/milestone-a/validate-evidence.mjs
```

This is isolated source/local Auto-card evidence. It does not invoke Run now,
scan, native or gameplay actions and does not establish full-shell/runtime pixels.

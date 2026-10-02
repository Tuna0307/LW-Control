# LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1 evidence

This packet records the bounded PM-027 correction. The exact original authority is
`frontend-package/web/assets/MapDataPanel-B4GXEND2.js` SHA-256
`CE74345518BE72E417A510B982C591729E5683F69751E190805598C4C05D3089`.

`independent-cases.mjs` executes the exact original component and current
`MapDataPage.jsx` through their maintained hook/effect runners. It covers a normal
same-server options reply, the PM-027 redirected reply, an obsolete redirected
reply after a newer server generation, an actual scan-server transition, and loss
of a positive scan server. Deferred options/search replies verify request server
sequences plus settled server, option lists, cache/page/rows/total/loading ownership.

`replay-parent.mjs` executes the unchanged parent `check-filter-lifecycle.mjs`.
It intercepts only that checker's write to the historical result path and redirects
the fresh JSON to `parent-filter-lifecycle-results.json`, then byte-compares the
historical parent result before/after the replay.

Run from the repository root:

```text
node evidence/lwbridge-0.3.17/ui/LWB317-PM-027/independent-cases.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/independent-cases.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/replay-parent.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1/validate-evidence.mjs
```

`browser-results.json` records the R1 offline browser smoke only. It checks the
normal server-321 fixture's City filter surface and Treasure preference controls;
it is not original-runtime, native Clear/Treasure, gameplay, or pixel proof.

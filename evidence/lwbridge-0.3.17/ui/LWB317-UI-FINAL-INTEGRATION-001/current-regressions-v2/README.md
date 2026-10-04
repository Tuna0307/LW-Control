# Current lifecycle replay after lazy-route migration

These owned adapters extend the preserved first final-integration checkpoint. Historical scripts/results and `current-regressions/` remain byte-identical and are pinned in `historical-inputs.json`.

App now imports React Suspense and the production `preloadRoute` dispatcher. The hook runtimes bind an inert Suspense presentation boundary. `current-route-bindings.mjs` parses the actual current Pages router and compiles its exact preload dispatcher; only import promises are inert here. No original callback/effect assertions are replaced. Actual deferred-loader/Suspense/transition/Activity behavior is separately mounted against the production modules in `route-loading/run-mounted-deferred.mjs`.

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v2/run-map-entry-focused.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v2/run-map-entry-affected.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions-v2/validate-preservation.mjs
```

Expected maintained assertions: Map-entry 10; mounted Cross-server 12; App/Home/retention 3; current parent/child Map ownership; affected aggregate 4. All outputs stay in this v2 packet. No native/network/gameplay/image/updater/exit action, product edit, or commit occurs here.

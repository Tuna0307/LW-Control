# Final integration: Map notice-region parity

The original 0.3.17 Map renderer shows the local scan error, otherwise the scan-state error, then continues directly to Manual controls. It does not insert a disconnected, unavailable-backend, or browser-preview banner at this boundary. The canonical clone had added three English diagnostic notices on this product surface. The correction removes only those six JSX lines; native-action disabling and rejected unavailable-provider operations remain unchanged.

## Exact evidence

`source-locators.json` pins the original Map asset and UTF-8 byte 49591, length 166, ending immediately before `Y===manual`. It also pins the recovered original error translator from the original shell asset. `baseline-notices.jsx.txt` preserves the actual pre-correction canonical JSX region and its hash, rather than a guessed reproduction.

`check-notices.mjs` executes the exact original expressions with the exact original translator and compiles both the preserved baseline and the actual current canonical JSX using esbuild. React's server renderer compares resulting markup across all nine catalogs, preview/native-unavailable/native-offline/native-online, and no/local/state/both errors: **144 comparisons, 27 baseline failures, zero current failures**. This is actual extracted-region render evidence; it does not claim whole-page mounting, original runtime pixels, or native behavior. No original file is modified.

Run from the repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/map-notices/check-notices.mjs
```

`--record` writes only this packet's `notice-results.json`. The affected historical scan-header checker was run without `--record`: 270 comparisons, six clock cases, unmount PASS. Its prior evidence remains unchanged. Task-local command results are in `affected-regression-results.json`.

No Last War session, native provider, scan, jump, service, updater or gameplay action was used. Integration/browser/canonical-build/global acceptance belongs to the project lead's enclosing campaign.

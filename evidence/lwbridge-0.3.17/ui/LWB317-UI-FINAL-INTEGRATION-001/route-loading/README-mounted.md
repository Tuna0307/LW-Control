# Mounted deferred-loader and motion proof

`run-mounted-deferred.mjs` bundles and mounts the actual current canonical App, lazy Pages router and resolved page modules with React 19 and the existing isolated jsdom installation. A build-only instrumentation wrapper delays the promises at the six actual dynamic-import entry points; when released, the imports resolve to the real production modules. No guessed page implementation or product test hook is introduced.

Seven grouped scenarios prove:

1. Initial Hotkeys suspension shows the canonical panel/Processing fallback, then mounts the actual Hotkey panel on import resolution.
2. Actual App starts Map summary before preloading and before route transition, without waiting for its acknowledgement. The pending transition preserves Home DOM and route state. Map becomes visible when its import resolves while summary is still pending. Clicking already-active Map starts neither another request nor another import.
3. Real navigation hover and focus each invoke the current preloader. Rejected City prefetch is handled without an unhandled rejection or changing the active Home route.
4. Actual Mini Games edits survive Activity hiding/return with the same DOM identity. Its real one-second interval suspends and resumes **1/0/1**. Changing the actual RetainedPages profile key creates a new panel with default state.
5. Actual App theme control follows the reduced-motion branch.
6. Actual App theme control adds/removes the fallback transition class after **240 ms**.
7. Actual App theme control uses `startViewTransition` when available.

Run from repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/route-loading/run-mounted-deferred.mjs
```

Result: `mounted-deferred-results.json`. App/router/loaded Hotkey and Map modules/theme-helper hashes are recorded and verified stable throughout the run. The source-contract/original oracle/migration files in this shared packet are owned by the other evidence lane and were not modified.

The single native-mode scenario uses controlled inert bridge responses; all other scenarios use explicit offline preview fixtures. No Last War, service, live/native operation, image read, updater, exit or game control is invoked. jsdom supplies DOM semantics; no original-runtime pixel or native WebView loading/motion claim is made. The import gates intentionally expose timing branches even when a local browser cache would load the chunks immediately.

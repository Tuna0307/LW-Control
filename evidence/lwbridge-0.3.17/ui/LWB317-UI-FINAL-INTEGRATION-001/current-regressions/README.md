# Current shell lifecycle regression adapters

Historical App import-stripping harnesses predate the final integration's shell helpers and presentation components. Their original assertions remain useful, but their fixed bindings cannot execute the current App import surface. This packet owns copies with the following compatibility changes only:

- Real canonical `shellState.js` and `shellTheme.js` imports, including profile-focus persistence.
- Actual ReactDOM `flushSync` in the mounted Cross-server suite; inert callback execution in the established hook runtimes.
- Inert TopVersion/config-error/profile/exit/loading presentation children; transparent GameAssetImageProvider wrapper in the mounted suite. Transport remains controlled, with no image/native provider invocation.
- New owned output filenames and source-locator paths. Historical scripts and historical results are preserved.

The actual current App body is still parsed/compiled and its production callbacks/effects execute. The Map ownership replay uses the unchanged maintained import-aware current Map harness, which already stubs JSX presentation imports including GameAssetImage. This suite proves request/state ownership, not image rendering, modal rendering, native providers or pixels.

Results:

| Suite | Current result |
|---|---|
| Map-entry production callbacks/effects | 10/10 PASS |
| React/jsdom Cross-server mounted interactions | 12/12 PASS |
| RetainedPages/App Map ownership/Home acknowledgement | 3/3 PASS |
| Current App/current Map ownership | PASS: parent bootstrap/immediate poll=2; child summary=0; child listeners=0; options=321,321 |
| Map-entry affected aggregate | 4/4 PASS |

Commands from the repository root:

```powershell
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions/run-map-entry-focused.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions/run-map-entry-affected.mjs
node evidence/lwbridge-0.3.17/ui/LWB317-UI-FINAL-INTEGRATION-001/current-regressions/validate-preservation.mjs
```

The affected aggregate reruns the Cross-server and ownership adapters. Its historical result hashes are checked before and after. `historical-inputs.json` independently records seven historical source files and four historical results. Current outputs live only in this directory. Runtime dependencies are the existing UI React/esbuild/Babel dependencies and existing isolated jsdom package named by the preserved Cross-server harness; no dependency installation was performed.

No product/master edits or commit occurred in this adapter task. No Last War, gameplay, native/service/image/updater/exit operation was invoked.

# LWB317-UI-COMPLETE-001 clone QA

## Runtime and visual checks

The canonical Vite frontend was run locally on `127.0.0.1:4317` in preview mode. Repeatable screenshot URLs used only `previewPage`, `previewState`, `previewLanguage`, and `previewTheme`; `App.jsx` accepts `previewState` only when `backendBridge.mode === "preview"`.

Screenshots under `screenshots/` cover Home connected/repair/recovery failure, Automation nested Settings, Map, Squads profile/editor and equipment editor, populated City Layout, Hotkeys in Japanese/dark, Mini Games active Food House, and Settings update available. The responsive Home capture uses a `768x900` viewport; the other captures use desktop widths up to `1440` with light/dark coverage. Exact file hashes are in `source-manifest.json`.

Real browser interaction observations:

| Interaction | Observation | Status / limit |
|---|---|---|
| Theme toggle | light → dark; `aria-pressed=true`; `localStorage['lwbridge.theme']='dark'` | `IMPLEMENTED_NOT_VALIDATED` against original |
| Language selection | English → Japanese without a pinned preview locale; `document.documentElement.lang='ja'`; nav and Home copy rerendered to Japanese; `localStorage['lwbridge.language']='ja'` | `IMPLEMENTED_NOT_VALIDATED` against original |
| Automation switch | first card changed `aria-checked=false→true`, status `Disabled→Enabled` | local preview interaction only |
| Automation Settings | first card `aria-expanded=false→true`; nested training form exposed | local preview interaction only |
| Map Secret Task tab | status/level/quality/special/plunderable filters plus random delay/schedule/share controls appeared; action controls stayed disabled | no gameplay action executed |
| Map Treasure tab | type/foreign-radar/lucky filters and claim actions appeared; claim actions stayed disabled | no claim executed |
| Map Auto Scan tab | master/targets/interval/speed/eight scan types/return/run-now/navigation notice appeared; Run now stayed disabled in preview | no live scan or jump executed |
| City Layout zoom | `24px→26px` | local preview interaction only |
| City Layout Undo | pending change became `No changes`; Undo disabled; Apply remained disabled | no native city update |
| Food House | running display (`Current level: 4`, elapsed, `Confirmed 18/42 moves`) → Stop → stopped display + enabled Start | local preview interaction only; no game action |

Browser console inspection on Home reported no errors. Visual inspection found and corrected two clone issues during QA: shell server-jump labels were still hardcoded, and the Food House progress line initially used a combined status layout rather than the recovered separate status rows.

## Automated checks

Final pre-documentation run:

```text
npm.cmd run check --prefix src/LWBridge.UI-0.3.17
  LWBridge 0.3.17 static UI scaffold checks passed.
  LWB317_MAP_UI_INTEGRATION_CHECKS_OK
  LWB317_UI_COMPLETE_CHECKS_OK 343 referenced keys {"en":1282,"zh-CN":1282,"zh-TW":1233,"ja":1233,"ko":1233,"vi":1233,"id":1233,"ru":1233,"pt":1233}

npm.cmd run build --prefix src/LWBridge.UI-0.3.17
  LWB317_PRODUCTION_UI_BUILD_OK 7489c997b5253b5eef84a9bd1da9d1f2de98dfd1d7629e277f40f88110d361da a34bcd9545789e191c6ce9002b3c9f2a9522f14d09a54c99bdcbeda96b43cfe5

npm.cmd run check:production-build --prefix src/LWBridge.UI-0.3.17
  LWB317_PRODUCTION_UI_PACKAGE_OK 7489c997b5253b5eef84a9bd1da9d1f2de98dfd1d7629e277f40f88110d361da a34bcd9545789e191c6ce9002b3c9f2a9522f14d09a54c99bdcbeda96b43cfe5

dotnet run --project tests/LWBridge.Map-0.3.17.Checks/LWBridge.Map-0.3.17.Checks.csproj -c Release
  LWBridge.Map-0.3.17 scan-state checks passed.
  LWB317_MAP_CHECKS_OK

git diff --check
  exit 0 (Git emitted only existing LF→CRLF working-copy notices)
```

## Original-reference comparison

`Get-Process` found no running LWBridge/Last War reference process or authenticated post-auth window during this work item. Direct original screenshot/pixel comparison is therefore `BLOCKED`. This evidence set does not relabel clone-only screenshots as reference proof.

Missing evidence needed to lift the blocker: legitimate authenticated access to the exact `lwbridge-0.3.17.exe` post-auth shell and the corresponding conditional page states at matched viewports, without triggering gameplay.

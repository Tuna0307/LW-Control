# LWB-R8-007 — restore original City Excel export

**Date:** 2026-09-24
**Scope:** restore the original LWBridge 0.3.1 `map_city_export` surface end-to-end after the earlier rebuild-specific retirement.

## Result

City Excel export is restored as a strict-parity feature.

The implementation now preserves the original frontend API wrapper and City-only button/state/locale strings, routes `map_city_export` through the desktop save-dialog host, executes the recovered 200-row paged export loop, writes the recovered six-part XLSX package and returns the original `{canceled,path,rowCount}` result envelope.

This checkpoint is based on the recovered original Map control-plane contract in:

- `docs/reviews/2026-09-24-r8-map-control-plane-exact-contract.md`
- original immutable frontend under `evidence/lwbridge-0.3.1/frontend/`
- prior R6/R7 City export native/writer evidence retained in `evidence/lwbridge-implementation/`.

## Exact frontend contract restored

The original frontend calls:

`map_city_export(query, {headers,sheetName,yesLabel,noLabel})`

The City panel now again:

- shows **Export Excel** only on City;
- disables it while map scan reading or export is busy;
- forces the current City query to page 1 / pageSize 200;
- sends localized headers exactly:
  `Server, X, Y, Player, UID, UUID, Alliance, Level, HP, Shield Ends, Marked, Updated At`;
- sends localized `map.city` as sheet name and localized common yes/no labels;
- consumes `canceled,rowCount,path`;
- restores `map.exportExcel`, `map.exportingExcel`, and `map.exportExcelSuccess` in all nine locale bundles.

The generator no longer strips this reference feature. A new strict-parity browser check replaces the former removal regression.

## Exact host/export behavior restored

The desktop route intercepts `map_city_export` because the command requires the native save dialog.

The recovered export loop is:

- page starts at 1;
- page size is 200;
- at most 1000 pages;
- maximum export population is 200,000;
- stop when a returned page is empty or accumulated rows reach returned `total`;
- overflow error is exactly:
  `MAP_EXPORT_FAILED / city export exceeded the row limit`.

The City export query preserves current keyword/alliance/no-alliance/marked/sort behavior while overlaying indexed authoritative `serverId`, `marked`, `updatedAt`, and `shieldEndTime`.

## Exact workbook contract

The writer restores the original 12 columns:

A Server
B X
C Y
D Player
E UID
F UUID
G Alliance
H Level
I HP
J Shield Ends
K Marked
L Updated At

UID and UUID remain strings.

Shield column J uses `protectEndTime` when that key exists; it falls back to `shieldEndTime` only when `protectEndTime` is absent. Invalid present primary values intentionally produce an empty datetime cell.

Timestamps below `100000000000` are seconds; otherwise milliseconds. Excel serial is:

`milliseconds / 86400000 + 25569`.

The XLSX contains exactly six ZIP parts:

- `[Content_Types].xml`
- `_rels/.rels`
- `xl/workbook.xml`
- `xl/_rels/workbook.xml.rels`
- `xl/styles.xml`
- `xl/worksheets/sheet1.xml`

The recovered column widths, frozen header row, autofilter, margins, four-XF style table, datetime numFmt 164 and header styling are preserved.

Default filename is UTC:

`map-cities-{serverId}-{YYYY}{MM}{DD}-{HH}{mm}{ss}.xlsx`

The native dialog is `Excel workbook|*.xlsx`, default extension `xlsx`, normal overwrite prompt. User cancellation or dialog construction/show/result failure returns:

`{canceled:true,path:"",rowCount:0}`

Success returns:

`{canceled:false,path:<selected path>,rowCount:<written rows>}`.

## Validation

- recovered frontend generator: PASS.
- Release test project build: PASS, 0 warnings / 0 errors.
- deterministic desktop suite: PASS, `ok=true`, `failures=[]`.
- City workbook tests: included in the deterministic suite and PASS.
- strict-parity Playwright check: `City Excel export strict-parity browser check passed.`
- general frontend comparison: PASS, 36 light/dark English/Chinese browser checks after updating the stale R7 City-export-retired assertion.
- Map browser regressions: PASS for scan strategy, R7-131 persistence/Stop/Clear race, R7-156 navigation/reconnect and R7-136 restart.
- generated API contains `map_city_export`.
- generated Map panel contains the original City export handler/button.
- all nine generated locale bundles contain the three original City export translation keys.
- obsolete `check_city_export_removed.cjs` removed; CI points to `check_city_export_restored.cjs`.
- `git diff --check`: PASS.

## Parity interpretation

City Excel export is no longer a known product deviation. Its recovered control plane, native dialog behavior and XLSX contract are implemented and regression-tested.

This does not imply the rest of Map Data is parity-complete. Scheduled Plunder, original Auto Scan semantics, public scan kind/mode deviations and protected acquisition/action internals remain separate work.

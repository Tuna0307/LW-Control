# MAP-005 — Manual City / Resource scanner delivery receipt

**Review state:** READY_FOR_LEAD_REVIEW. Independent lead acceptance, merge,
publication and branch retirement remain pending. The entire Map tab is
**PARTIAL**: this receipt concerns Manual City/Resource acquisition only.

**Review branch:** `codex/map-manual-scan-delivery-005`; task:
`docs/work-items/LWB317-MAP-MANUAL-SCAN-DELIVERY-005.md`. Built on
`9f8cdf186688743068976de5f4724d0af42f124b` without altering the released
single-game Home or already accepted stored Map search, filters, sort,
pagination, tab retention and City Excel controls.

## Genuine current-server production evidence

- **Actual Windows path:** normal packaged `LWBridge.Desktop.exe` in isolated
  `--isolated-root`, Home Launch -> authenticated **Connected** ->
  Map Data -> Manual scan kind buttons -> Normal/Fast -> Start; real game process.
  No fixture or archived Map data supplied to acquisition.
- **Official PC client:** installed `Last War-Survival Game`, compatible
  current-client `contentVersion=24`; `Game/LastWar.exe` file and product
  version `2019.4.40.16762411`, SHA-256
  `905C98C1F89841F90B492556192BA0642F3D209A873CB8C1F7B3C340ACA0733D`.
  Actual live **server 2212**; Map317 run schema currently does not persist the
  source's world ID, so this document does not invent one.
- **Completed combined City + Resource Fast run:** native run
  `ee6beb48292e43bf8966aedfecae86ae`, 2,500/2,500 blocks, zero failed,
  published **7,052 City** and **8,002 Resource** rows, no remaining staging.
  Observed Unix-ms run interval `1791661796204..1791662029531` (233 seconds).
  Historical 7,000/8,008 counts are not used as expectations.
- **Standalone City Normal:** run `1c50df7f8e7b484bb0dda95294e5424b`,
  2,500/2,500 blocks, zero failures, **7,058 newly acquired City** rows
  with distinct keys. Run interval `1791663089298..1791663220908`
  (Unix ms). Home Close, desktop exit, Lua restoration, fresh launch
  and authenticated reconnect proved the published City data survived an
  application restart without scanning City again.
- **Standalone Resource Fast:** run `25b1e0e7a9004469b49dfb370c7c097a`,
  2,500/2,500 blocks, zero failures, **8,008 newly acquired Resource**
  rows with distinct keys. Run interval `1791663459019..1791663694916`
  (Unix ms). Resource publication preserved all **7,058 City** records.
  The final task database holds these later standalone results.
- **Real native + reopened SQLite:** actual `map_search` City and Resource
  counts matched reopened SQL; page 1/page 2 each returned 50 different records,
  `map_data_options` agreed with SQL counts; actual Map UI tabs/summary rendered
  7,052 / 8,002. Native City Excel writer returned a genuine XLSX containing
  all 7,052 City rows. The actual packaged Save As dialog separately generated
  a 7,052-City workbook (SHA-256
  `B88FD04C40482D5869D4D5E64AA93E6EA95E5DDD97FD25B667E0DA75C4814C44`).
  Resource page 1/161 -> page 2/161 worked; City page 1/142 and a
  genuine name search -> exactly one matching row worked.
  The final independent audit reconciled latest Resource SQLite run with
  its separately recorded preceding City completion; `MapStore.CompleteScan`
  deliberately prunes older completed run history. On the final freshly
  acquired data, native `map_search`/options and reopened SQL agreed:
  **7,058 City and 8,008 Resource**. Both kinds had distinct first and second
  page digests, no staging and zero actionable scheduled jobs.
  The native City XLSX independently reopened at **7,058 rows / 12 columns**,
  while the actual packaged Save As workbook reopened at
  **7,052 rows / 12 columns**. Both use fresh genuine data.
- **Separate actual active Stop:** started a new Resource-only Fast scan, watched
  progress (~2.7%), pressed **Stop** in the packaged Map UI. Run ended
  `cancelled`, with zero staged records; published **City 7,052 / Resource 8,002**
  remained unchanged. This was a real game Stop, distinct from controlled races.
  A consistent ignored local SQLite backup of the genuine data was taken
  *before* Stop (SHA-256
  `6abb6339001bb0438002601d81b6aa678378288a5a7fadfe7c13b5dc78a45c72`).
- **Localized and stored controls:** English/light and Japanese/dark Map
  showed current counts, scan completion/Stop, browsing, and Excel feedback.
  Task-owned UI **Clear Map Data** returned zero rows; reopened SQLite
  independently confirmed `map_records=0, scan_records=0, scan_runs=0`.
  The genuine pre-Clear backup remains separate, and subsequent standalone
  City/Resource scans repopulated the same isolated task database.
  The City player-mark star was also exercised through JA/dark packaged
  controls and persisted in the isolated `player_marks` table, with a
  Resource-tab return retaining its visual state.

## Controlled production-boundary regression evidence

Maintained `tests/LWBridge.Desktop.Checks/MapManualScanDeliveryChecks.cs`
exercises the actual native Map317 command, adapter, scan engine, sink and
SQLite; only external game capture/context responses are controlled. It checks
Normal 8 and Fast 20 admission, duplicate Start lease, positive staging before
publication, Stop after staging, delayed response ignoring cancellation,
acquisition percent without committed blocks, selected-kind publication,
failure retaining last published data, owned backend loss, terminal publication
callback cancellation, and reopened native query identities. Latest controlled
publication-race test revealed a real engine-terminal staging leak; the sink now
discards terminal staging atomically with its status transition.

Code corrections: propagate measured acquisition progress independently from
completed blocks; normalize Unix-second native timestamps against the UI
millisecond clock; fence cancellation directly before publication; retire
unpublished staging after error or Stop while preserving successful data; and
register engine task ownership before its potentially immediate completion.
Unknown original scanner internals and protected traversal methods are **not**
claimed recovered. Some genuine Resource rows display the existing fallback
label “Unknown resource” despite real positions, levels and record identities.

## Validation, isolation and handoff

- Reproducible native and frontend commands:
  `npm.cmd --prefix src/LWBridge.UI-0.3.17 run check`;
  `dotnet run --project tests/LWBridge.Desktop.Checks -c Release`;
  `dotnet run --project tools/home_004_pipe_recovery_probe/Probe.csproj -c Release`;
  `dotnet build src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release`.
  The native suite includes accepted Home and stored Map regressions.
- Audit CLI (against **only the genuine isolated DB**, before Clear):
  `dotnet run --project tests/LWBridge.Desktop.Checks -c Release -- --map005-audit
  <task-map-data.db> <ignored-output-directory>
  <separate-city-completed.json>`. It validates actual native
  `map_search`, `map_data_options`, pagination, scheduled-job absence
  and independently reopened XLSX shape. A deliberately disconnected audit
  host cannot truthfully establish a current server for `map_summary`:
  it correctly reports server 0/empty; actual connected packaged Map UI
  rendered the genuine server 2212 summary and kind counts.
- A safe local-only pilot tool is `tools/map_005_live_gate.py`; ignored
  `artifacts/map-005/genuine-pilot/` holds original-file backups, sanitized
  receipts, raw task-only SQLite/UI screenshots and XLSX for independent local
  review. Raw player rows, original Lua contents and game data are **not in Git
  and are not in the Windows ZIP**.
- The task profile had Auto Launch OFF, Automatic Reconnection OFF, zero
  actionable dispatch/truck jobs; no Auto scan or cross-server movement, claims,
  plunder, sharing, spending or other game actions were performed.
- Each genuine session ended through Home **Close** and desktop exit; all
  original Lua-triplet SHA-256 hashes were restored, zero owned game/launcher/
  host processes survived and no recovery journal remained. The same gates
  were checked before the separate scan follow-ups and at final cleanup.
- The release script packages a source-identified Windows RC ZIP with
  `SOURCE-COMMIT.txt`, executable ProductVersion containing that Git source,
  and validated canonical production UI. Inspect the final local package
  SHA-256/ZIP extraction verification in the PR handoff; the ZIP contains
  no genuine game dataset.

**Lead action:** inspect the branch, receipt and independently extracted ZIP,
then review/merge/publish only approved Manual City/Resource changes. Do not
accept the other six Map scanner kinds, Auto Map execution or Home F-07 from
this delivery.

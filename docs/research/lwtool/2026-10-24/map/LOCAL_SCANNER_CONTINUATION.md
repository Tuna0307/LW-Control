# Local LwTool method investigation, acquisition attribution and current blocker

Date: 2026-10-24. Branch: `codex/lwtool-full-improvement` / draft PR #16. **Status: local scanner recovery and offline diagnosis completed; no new performance-qualified production acquisition change; genuine final-package gates blocked by an externally retained restoration journal.** This addendum supersedes the older remote-Quick-Map emphasis as the owner's current method priority. It does not replace the historical accepted receipts.

## Recovered normal LOCAL scanner: RECOVERED (static shipped-client evidence)

The active **normal** Map route is `toggleScan` → `py_start_scan` → `_run_full_scan` → `_run_task_blockroute` (`recovered-ui.html:3791-3829`; `recovered-core.py:31627-31702,20940-21411,20606-20935`). This is the game-backed route the owner asked us to investigate; the remote Premium Quick Map path is out of scope for further development.

1. `_run_full_scan` issues **100** LOD0 `world.get.block` row requests, each with **100** point indices, covering a logical 100-by-100 source grid. The block route sends through the attached game and correlates point replies via its pending `_id` state (`recovered-core.py:20509-20582,21010-21029`). This is **not** evidence of a bulk backend map index or a single fast whole-world RPC.
2. The route dynamically adjusts burst size (one to four requests) and send pacing using observed ping/IPC response time. It drains captured protobuf records into temporary per-window buffers and deduplicates point observations by server/point identity (`recovered-core.py:20174-20279,20628-20642,20718-20760,20839-20915`). It tracks confirmations, polls at around 250 ms, and settles for around 500 ms after the 100th confirmation; it can retry an initial busy condition, but does not safely parallelize arbitrary unmatched replies (`20659-20705,20777-20825`).
3. Discovered local City identities initially lack fully hydrated player power/army power. `get.user.info.multi` requests up to **100 UIDs** per batch, up to **25 batches** per group, with game-side pacing, and makes up to two additional passes for missing owner details (`recovered-core.py:21225-21353`). Name, alliance, level, total/troop power, country and owner-server fields are merged into a RAM owner cache capped at roughly 5,000 entries (`1009-1018`). This is local game-network hydration and must be included in any complete timing.
4. UI phase one can display identity rows before enrichment. Delayed detail deltas and final row refresh follow. Events are serialized/coalesced toward the webview (`recovered-core.py:21168-21177,21321-21380,21735-21845`; `recovered-ui.html:3692-3718,7335-7355`). Its local store is principally in RAM. Critically, nonempty partial sweeps and incomplete owner detail work can still reach a visible *done* state in some recovered branches (`recovered-core.py:20899,21120-21177,21346-21380,31674-31695`). The UI completion indicator alone is therefore not complete-census or field-complete proof.
5. The separate **normal Truck** scanner uses **16×16 scoped cells**, eight at a time with roughly 700 ms spacing and game-side selected/reward filtering (`recovered-core.py:33169-33177,33292-33313,33355-33384`). It is not an unfiltered equivalent to LW-Control's 625 independently verified rectangles. Conditional `Api.CityResourceScan*` after a missing usable point/resource source is a fallback, not the normal primary (`recovered-core.py:21038-21093`). Legacy `_mapscan*`/`_userscan*` cleanup markers do not make them the active entry path.

These are recovered **client implementation** facts, not a live runtime speed measurement of LwTool normal Map. No competitor source, customer rows or credentials are tracked in this report.

## Actual LW-Control production: PROVEN/QUALIFIED

`CurrentClientMap317ScanProvider` → `MapScanEngine` → `CurrentClientMapBlockSource` performs one shared selected-kind acquisition with exact profile/run/cancellation ownership, source-validation and atomic SQLite publication (`src/LWBridge.Desktop/CurrentClientMap317ScanProvider.cs:169-253`; `MapScanEngine.cs:118-208,310-348`). Existing `CurrentClientMapBlockSource.CameraFree.cs:75-118,7785-7819 in the Lua source` covers all **10,000** LOD0 point AOIs in **63** request/reply cycles of up to **160** AOIs. The direct source has explicit 10,000-AOI evidence and complete 2,500-block checkpoints. This makes LwTool's 100-request point geometry unpersuasive as a request-count optimization without new causal data.

For Truck/Railway together, production uses **one** native 625-rectangle sweep at **20 requests/s**, **25-request bounded response fences** and up to two selective singleton repair passes; typed Railway records are sidecar output of that same sweep (`CurrentClientMapBlockSource.FastCity.cs:101-124`; `FastTruck.cs:13-103`; `tools/current_live_resource_probe.lua:7250-7287,7371-7485`). A separate 63-reply point sweep then supplies static kinds; it deliberately does not repeat the mobile sweep. Correctness requires 625/625 verified rectangle coverage, valid typed/current fields, nonexpired mobile records, authoritative current server, exact Stop/restore and final selected-kind atomic publication. LwTool's normal filtered 256-cell Truck list does not meet that contract.

## Reproducible phase attribution: PROVEN HISTORICAL MEASUREMENT

Run the new independent, nonexecuting diagnostic:

```powershell
python tools/map_019_review/analyze_local_scan_phases.py --audit artifacts/map019/20261024-allten-final-abba-audit.json --output docs/research/lwtool/2026-10-24/map/LOCAL_SCAN_PHASE_BREAKDOWN.json
python -m unittest tools/map_019_review/test_analyze_local_scan_phases.py
python -m unittest tools/map_019_review/test_march_idle_snapshot_hypothesis.py
```

It reads existing sanitized final same-session ABBA evidence only, demands correlated stage and native coverage receipts, and reports phase clocks, process CPU, sampled private/working memory and ten-kind counts without raw account data. Output: [LOCAL_SCAN_PHASE_BREAKDOWN.json](LOCAL_SCAN_PHASE_BREAKDOWN.json).

| Metric | Direct A1 | Direct A2 |
| --- | ---: | ---: |
| Whole scan wall time | **90.713 s** | **91.327 s** |
| Entire source capture | 88.805 s | 89.341 s |
| One typed Truck + Railway native sweep | **56.536 s** | **57.001 s** |
| Mobile requests | 625 initial + 50 repairs | 625 initial + 50 repairs |
| Native mobile rectangles proved | 625 / 625 | 625 / 625 |
| Direct all-point AOI acquisition | **27.491 s** | **27.757 s** |
| AOI replies / verified AOIs | 63 / 10,000 | 63 / 10,000 |
| Final local detail hydration | 1.461 s | 1.362 s |
| SQLite checkpoint | 1.001 s | 0.731 s |
| Final publication | 0.673 s | 0.846 s |
| Process CPU time in recorded round | 158.406 s | 155.156 s |
| Sampled peak private memory | 2,223.0 MiB | 2,529.8 MiB |

The phase clocks have distinct start/end boundaries. Source capture includes the serial mobile sweep and AOI work; all of its nested times must **not** be added to wall time as independent work. The small residual covers source orchestration and stage boundaries; CPU seconds are process time across CPUs, and sampled memory peaks are not proof of a leak. The original direct/camera ABBA mean wall time is still 91.020/142.639 s (36.19% lower). The separate **old vs batch** storage trial remains old median 2.484 s versus batched 0.673 s for **24,362 identical deterministic records**; identical canonical staged/published hashes and row counts were recorded. There is **no** newly measured whole-scan before/after performance win in this continuation.

## Candidate decisions: RECOVERED / UNKNOWN

| Proposed mechanism | Evidence/contract | Decision |
| --- | --- | --- |
| Copy LwTool's 100-row local point acquisition | Our full direct source already admits 63 complete replies for 10,000 AOIs. | **Retain 63-reply method**; no smaller valid request count is demonstrated by competitor geometry. |
| Copy LwTool's adaptive bursts/fewer waits | Our older overlapping source attempts lost substantial point recall; the game March push lacks echoed rectangle/request identifiers. | **Defer** until a distinguishing reply-correlation proof; do not repeat known invalid overlap/rate/fence settings. |
| Copy LwTool's 100-UID owner hydration batches | Our direct native City path already returns source-accounted player fields; observed all-ten Resource detail costs about 1.4 s, not 60 s. Extra multi-user requests have no measured savings. | **Retain** current authoritative native fields and fail-closed detail completeness. |
| Replace 625 mobile rectangles with LwTool's 256-cell filtered Truck scope | Different filtered/object coverage; no proof of equivalent unfiltered trucks, Railway types or moving/expiry behavior. | **Reject as a census replacement.** |
| Cache/replay old local results or show early completion | Can shorten display timing but can lose freshness or publish an unfinished sweep; our atomic selected-kind contract is already tested. | **Retain** complete-first durable publication and last-good data. |
| Remove redundant march-dictionary reads on idle Unity frames | `tools/current_live_resource_probe.lua:7371-7413` enumerates native TrainData even on frames with no new `_lastGetBlockMsgTime` receipt. But the game's handled/cache update can occur **after** that receipt (`:3858-3901`); a stamp-only gate could omit a late valid train. | **Research candidate only**. Instrument receipt→handled→cache timing, test delayed and stale stamps, then require matched live Truck/Railway field/UUID and arrival-drift parity before enabling. |

The additional **inert counterexample** in `tools/map_019_review/test_march_idle_snapshot_hypothesis.py` demonstrates that a delayed cache mutation after an unchanged receipt timestamp can be seen by the existing per-frame reader but missed by naive stamp-only gating. It describes a possible native scheduling order, not an observed game failure; thus it is a reason to demand a real native handled/cache correlation witness before enabling that optimization.

Retaining a qualified production method is a deliberate decision here: no newly recovered normal-local LwTool alternative has yet established the same coverage, fields, ownership and faster elapsed time. The new phase analyzer is a maintainable offline diagnostic, **not** a production scanner replacement.

## Recovery-journal investigation: READ-ONLY PROVEN

The global journal `%LOCALAPPDATA%\LWBridgeRebuild\overview-bridge\recovery.json` exists, SHA-256 `b4f95aada3b3270421fafd6bf080fe0badbebbafa470d1c88b59ca1fc7680ddb`. It records schema 1, `active_ready_deferred_restore`, historical game PID **44164**, game process start **2026-10-23 04:23:59.4623424 UTC**, last journal update **04:24:09 UTC**, and one exact profile/session. That profile matches the current app config, and the retained session's helper-start and host-start receipts correlate to its PID/path/creation time. No matching helper-stop, host-stop, per-attempt cleanup or restored manifest was retained. The old game process is absent. One lock marker exists, but no separate operation-owner record.

All three current installed official files match **both** the journal's original-file hashes and backup bytes; the injected candidate is **not** installed:

- official script data SHA-256 `8f61102f401f3052598365ca9750c2c496969895b5add2da138cb2147d045493`;
- official script metadata SHA-256 `32dbdf2762a8658a0c803713ca9856042938277c820288f419ebf546928d4095`;
- official version marker SHA-256 `670671cd97404156226e507973f2ab8330d3022ca96e0c93bdbdb320c41adcaf`.

The backup manifest remains `backup_ready`, not `restored`. The installed files have later timestamps than the old journal with **no same-session restoration receipt**, so the historical actor of restoration is unknown even though the current bytes match the originals. The mutating `recover_overview_pending_current.py` copies backup files, writes recovery state and removes the journal; it cannot be called as a read-only check. The running-game adoption path requires the exact historical live PID/path/start, which is no longer available. The journal therefore **must remain** pending independent ownership/restoration reconciliation. Normal admission correctly refuses to start a final packaged session. Machine-readable sanitized ownership/restoration record: [RECOVERY_JOURNAL_READONLY_EVIDENCE.json](RECOVERY_JOURNAL_READONLY_EVIDENCE.json).

Read-only `python tools/run_overview_bridge_current.py check-only` passed with `installedFilesChanged=false`, matching official package checksum and supported game compatibility. No app/game processes were launched for this continuation, no installed scripts modified, and no journal/backup/lock removed.

## Final package, tests and exact remaining gates

The product source and native probe were **not modified**: keep the already source-pinned ZIP `artifacts/map-final-acceptance-20261024/package-9ddd0ed8/LW-Control-lwtool-improvement-9ddd0ed83afc.zip` from product source `9ddd0ed83afc771813cd60786698d32f85f5eeb7`, ZIP SHA-256 `c23a0f2603f6790417c23662297385593b830355ae721755a753147e27ff339f`. Its retained CRC/hash/size/source manifest and privacy proof still apply. Current `tools/map_015_package_ui.py` non-executing structural verification for all-ten, auto-all-ten, truck-stop and saved-clear-reopen passed again; its **29 auditor tests passed**. The phase diagnostic's own unit tests passed; it does not execute the game.

**Open and blocked**: real final-package Manual all-ten, remote Auto all-ten with exact server return, a packaged mid-flight Stop with current batched staging, saved-data Clear/reopen, and bounded Home same-session route recovery/Stop smoke. All require restoring normal admission with evidence identifying the journal's owner and a supported exact-restore closeout. Independent lead review must determine who can authorize independent journal reconciliation and establish any missing same-owner Stop/restoration record; absence of the historical PID or matching installed hashes alone is insufficient authority to remove the marker.

No new before/after live acquisition benchmark, fully qualified Truck optimization, final packaged live acceptance, main merge or public release is claimed.

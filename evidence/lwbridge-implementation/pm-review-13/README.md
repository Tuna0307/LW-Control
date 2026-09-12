# PM review 13 evidence — 2026-09-10

Audited `b16fb9a3a8948b7ec54c7ae701a7305a0a813c16` on `research/offline-controller`. This is implementation/UI review, not new binary recovery. The verified source hashes are in [saved-evidence-check.json](saved-evidence-check.json). The feature authority remains the immutable LWBridge 0.3.1 reference identified in `task.md`; no new extraction or disassembly was performed.

## LWB-PM13-001 — ordinary native UI failures and available capability

**Observed in the normal rebuilt Windows application**, built Release from audited HEAD. Tool: installed `computer-use` skill package `26.903.61454`, `mcp__node_repl__js` and `@oai/sky`. Initialize according to that SKILL.md, use `sky.list_windows()`, launch the existing built `LWBridge.Desktop.exe` without diagnostic arguments, uniquely select its returned window, then use fresh `get_window_state` observations and one `sky.click` per action. Do not reuse saved element numbers: they are observation-specific.

Reproduction and results:

1. Open normal app -> Overview -> Map Data. All eight scan categories are visibly checked. Click Start Scan with that unsupported selection: generic `操作未能完成。` appears. [Screenshot](default-scan-error.png), [settled accessibility observation](default-scan-observation.txt). Source locator: `LiveResourceProbeCommandService.InvokeAsync`, selected-types guard at lines 136–142 returns `LIVE_RESOURCE_TYPES_UNSUPPORTED` before helper launch. This checks a rejection, not the separately restricted live-acquisition operation.
2. Select Monster result tab -> Search: zero rows / no saved data. [Screenshot](monster-search-empty.png), [settled accessibility observation](monster-search-observation.txt). The error alert from step 1 remained on screen; do not attribute that prior alert to this Search call. `LWBridgeBackend`'s `map_search` only calls `MapDataStore.SearchIndexed`; the live adapter has no monster acquisition path.
3. Select Resource result tab -> Search: zero rows / no saved data, server shown as `-`. [Screenshot](resource-reopen-empty.png), [settled accessibility observation](resource-reopen-observation.txt). Read-only database verification found **one resource for server 2212 in the same selected profile**. Source locator: `LiveResourceProbeCommandService.currentServerId` starts null, is assigned only after import; `LWBridgeBackend.map_summary` at lines 206–257 requires that session value and otherwise fails closed. A saved browsing context must be distinct from live server/readiness. Exact frontend query/error handling remains to be traced during PM13-01.

These observations prove the native app was inspected and its failure/empty states reproduced. They do not prove a new game acquisition, server discovery, live monster absence, or full visual parity. Accessibility trees can lag rendering on the immediate action snapshot; settled observations and screenshots were retained. No game launched or installed game files changed. The previous SB-97 live harness launch and SB-98 disassembly were not rerouted. The inspected app was closed afterward.

Implementation impact: PM13-01 saved display/context and PM13-01b truthful feedback are regular-AI repairs within the active resource function. Next queued function is monster acquisition/search, not another disconnected research topic.

## LWB-PM13-002 — row-proof predicate accepts false positives

**IMPLEMENTATION AUDIT / isolated synthetic reproduction.** Source: `LWBridgeWindow.RunUiLiveAcquisitionAsync`, row query at lines 418–425. [Reproducer](proof-row-repro.cjs) extracts the exact predicate from the audited source and runs it on three synthetic DOMs. [Output](proof-row-repro-result.json) shows it accepts an empty-state row, a loading row and a stale unrelated row as nonempty text.

Reproduce in PowerShell:

```powershell
$env:NODE_PATH=(npm root -g).Trim()
node evidence/lwbridge-implementation/pm-review-13/proof-row-repro.cjs
```

Tool: existing global Playwright 1.62.1 and installed Edge, matching the repository's standard browser verifier. The first attempt used Playwright's missing bundled headless browser and failed with `Executable doesn't exist`; selecting the already-used Edge channel resolved setup. No approval denial occurred in that setup attempt.

The wider harness separately requires newer service run/time values; this counterexample concerns its **rendered-result assertion**, not a claim that the entire harness succeeds without acquisition. It never compares the displayed point/time to the new query result, and second-tab clicking alone does not establish refresh. Repair the source-to-render correlation and add negative UI tests. The saved historical output remains evidence of the defect; if the implementation predicate changes, this reproducer intentionally asks for review instead of claiming a passing product regression.

**Post-review resolution:** `LWB-PM13-004` replaces that predicate with immutable result -> explicit native Resource Search request/result -> exact row -> rendered-cell/timestamp correlation and adds adversarial loading/empty/unrelated-stale/same-point-stale tests. The historical reproducer above remains intentionally unchanged as evidence of the audited `b16fb9a` defect. See `../2026-09-11-pm13-proof-correlation.json`. PM13-04 remains the live final gate.

## LWB-PM13-003 — source-reviewed lifecycle gaps

**Source-reviewed, not live race reproduction.** `LiveResourceProbeCommandService.InvokeAsync` checks cancellation before import (around lines 181–191), calls `ImportCorrelatedResult` outside `stateGate`, then checks cancellation/closed again. The import calls `FirstLiveResultImporter.ImportOneResource`, which calls `store.UpsertRecord` (around line 158) before returning. Stop/Close can win during the import/write interval yet the row is already committed before the later cancellation is observed. Existing fake-helper cancellation cases do not inject this interval. Add a deterministic pre-commit barrier and atomic cancellation/completion ordering; reopen final PM12-B closure as PM13-03.

`RunHelperAsync` also calls `Process.Start` before acquiring the state lock, then disposes/throws if `closed` without registering `activeHelperProcess`. Review and test concurrent close at that boundary so any started helper remains tracked through restoration. This audit did not trigger either race against the installed game. The saved successful runs remain credited in their bounded scope.

## Saved real evidence and fresh validation

[Read-only verifier](verify-saved-evidence.py) and [result](saved-evidence-check.json) check the PM12-007 saved raw proof hash, current probe source hash, distinct request/time pairs, profile/session/PID chain, restored imports, matching point fields/occupancy and current persisted counts. The current selected profile matches the saved proof. This verifies recorded evidence plus current storage, **not another live run**. Reproduce with `python evidence/lwbridge-implementation/pm-review-13/verify-saved-evidence.py` while the expected current profile/database exists. Later data changes may legitimately change this diagnostic.

Fresh checks in this audit:

- Release build: 0 warnings/errors.
- Desktop deterministic suite with `--verify-real-config-unchanged`: all six groups true, failures empty.
- PM12 isolated recovery, session-identity and scoped-close regression scripts: `ok=true`.
- Frontend recovered-hash generation `--check`, five preference scenarios and both transport checks: passed.
- Existing browser verifier: all 35 checks passed. These do not cover the new rendered-proof counterexamples.
- PM13 proof-row reproduction: three false-positive cases reproduced; a known-defect finding, not product acceptance.

No new full acceptance case or Daybreak assignment. Follow [review 13](../../../docs/lwbridge-project-status.md) and [the regular task/prompts](../../../docs/implementation-handoff.md).

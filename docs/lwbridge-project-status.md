# Project-manager checkpoint — review 10

Reviewed 2026-09-10 against `01f139d59f0301a92bf27dba70a3ac5c9ebb6aea`, branch `research/offline-controller`, clean at start; GitHub matched this revision. [Review 9](reviews/2026-09-10-review-9.md) is historical.

## Result for the owner

**There is now working display code, not just research.** The rebuilt Map Data page displays one saved resource record acquired by a separate game-side probe. PM reproduced the replay with the current Release build: server 2212, coordinates 5,1, level 3, unknown resource name and gathering state. The app itself still cannot connect and acquire a fresh point. Neither page is complete; no additional full acceptance case is signed off.

The active goal remains **fresh acquisition initiated by the rebuilt app and displayed in Map Data**, followed by a second fresh acquisition with correlated session/source evidence. A saved-capture demo does not satisfy that goal or release deferred updater/export work.

## Accepted evidence and limits

- `01f139d` / LWB-R7-001 adds `FirstLiveResultImporter`, isolated in-memory storage, a replay-only summary adapter, CLI wiring and unknown-status rendering. Normal launch/scan/options/summary gates are preserved in reviewed code.
- PM hashed the original full local capture: `6447d30f373a76fbfe416a894546f8797ea71e20e44d9f06d3bfd61cd367cbce`. Its first resource record and timestamp exactly match the committed excerpt; counts are 15,293 records including 8,000 resources. The committed excerpt, screenshot and UI diagnostics match their recorded hashes. This corroborates the saved acquisition evidence; PM did not independently repeat game-side acquisition.
- The committed screenshot and fresh replay show one real-source record. The database is in-memory in this mode: this demonstrates insertion/query/display, not durable production persistence, fresh refresh, native bridge ownership or scan completion.
- Resource naming/occupancy remain unknown. The documented post-test restoration belongs to R7-001's evidence; this audit did not modify or freshly hash the installed Lua package.

## Required follow-ups — regular AI owns all three

| ID | Finding | Required result |
|---|---|---|
| PM10-01 | Fresh acquisition is not durably reproducible from R7-001's recipe. It names a probe version and temporary package/full-capture paths, but reproduction commands cover diagnostics/build/replay, not versioned capture source, exact capture invocation or restoration procedure. | Locate and preserve the minimal reusable capture source or a stable existing source reference, tool versions, source hashes, exact permitted invocation, extraction and restoration checks. Connect its supported contract to the app; do not add another unrelated snapshot. Preserve restrictions and do not assume a formerly executed operation is permitted now. |
| PM10-02 | Replay UI has no explicit saved-snapshot label; its normal-looking scan controls and stopped/idle status can confuse users. Resource auto-selection currently occurs only with `--capture`; the documented interactive command needs the Resource tab selected manually. | Clearly label capture time/source and replay mode, avoid implying refresh reacquires data, and make the documented demo open the intended tab or document the step. Keep this isolated from the recovered normal UI; never use a cosmetic label to claim integration. |
| PM10-03 | No dedicated importer/replay tests were added or found in the deterministic suite. Importer hashes arbitrary input but does not authenticate its live provenance, and silently skips records unless coordinates and IDs are positive Int32 values. These restrictions are not recovered general map semantics. | Add focused source-to-row, invalid/missing timestamp/record, optional-unknown, ID/coordinate boundary, isolation and normal-production-gate checks. Document demo-only limits; recover actual production ranges before generalizing. A computed input hash or fixture never establishes LIVE-PROVEN provenance. |

## Next checkpoint and specialist decision

Follow [the bounded task](first-live-result.md). First make the known acquisition reproducible, then resolve the earliest missing supported connection/acquisition link and wire fresh data into the app. Test a second fresh acquisition, failure/disconnection, and no saved-capture fallback. Stop expanding replay infrastructure once it provides the needed regression coverage. Optional resource-name recovery must not replace connection work.

No Daybreak assignment: ESC-001 CLOSED, ESC-003 NOT_ASSIGNED, ESC-002/004/005 NEEDS_INFORMATION. The R7-001 evidence reports Computer Use initialization rejected by automatic review; its exact tool invocation/error transcript is not included. The register tracks that documentation gap. This is an environment restriction, not withheld user permission or automatic specialist work. No denied operation was retried during this audit.

## Validation

Fresh Release build: zero warnings/errors. Deterministic suite: all five groups true, no failures. Recovered frontend generation/hash check, preference matrix and both transport checks passed. Fresh isolated replay exited zero and displayed the expected record. Its two reported errors remain expected open features: `MAP_INDEX_UNAVAILABLE` for options and `COMMAND_NOT_IMPLEMENTED` for plunder jobs. No fresh scan, game launch/control or original-binary analysis was performed. Source-review findings above remain open; passing existing checks does not cover them.

[Machine-readable audit evidence](../evidence/lwbridge-implementation/2026-09-10-pm-review-10.json) records checks and limits. All 47 acceptance rows are preserved. Delivery is a documentation/audit checkpoint, not a production feature implementation.

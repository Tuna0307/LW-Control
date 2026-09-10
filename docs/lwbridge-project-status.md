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
- `LWB-R7-002` preserves the exact historical acquisition source at ancestor commit `2a3cc66a88de86394b4404adf9e9cfd7af6275a1`, six source SHA-256s, the v14 staging/run/restoration contract and the historical `run_probe(...,180)` invocation. This makes the R7-001 acquisition recipe durable without claiming that the active environment currently permits replaying the same protected-file operation.
- `LWB-R7-002` also makes saved replay explicit and isolated: its own in-memory store, provenance banner, automatic Resource selection, disabled acquisition controls and `unavailable/saved_capture_replay` scan state. Focused tests preserve normal production launch/scan/summary gates.

## Required follow-ups — regular AI owns all three

| ID | Finding | Required result |
|---|---|---|
| PM10-01 | **PARTIAL — recipe preserved; control-pipe discovery recovered; fresh integration open.** `LWB-R7-002` pins the historical source commit/file hashes, v14 candidate identity, exact bounded run invocation, extraction and protected-file restoration/hash contract. `LWB-R5-006` proves the original per-user control-pipe derivation and live-correlates its name against the verified LWBridge host. | Recover the exact accepted session/message framing and request envelope, implement strict rebuild-owned production transport, initiate a new acquisition from the rebuilt app, display it, then repeat it with a second correlated capture time. Do not treat pipe presence, replay or historical executability as a fresh result. |
| PM10-02 | **DONE / IMPLEMENTED-OFFLINE-TESTED.** Replay is explicitly labeled with capture provenance, uses its own in-memory store, auto-selects Resource, disables acquisition controls and reports unavailable/replay scan state. | Keep the replay-only presentation isolated while production acquisition is implemented. |
| PM10-03 | **DONE / IMPLEMENTED-OFFLINE-TESTED.** Dedicated tests cover source mapping, invalid/missing timestamp/records, optional unknowns, recovered server range, demo-only point/coordinate boundaries, isolation and unchanged production gates. | Production ranges/provenance remain separate recovery work; file hashes alone are not live provenance. |

## Next checkpoint and specialist decision

Follow [the bounded task](first-live-result.md). The known acquisition is reproducible and `LWB-R5-006` resolves control-pipe naming. The earliest missing supported connection link is now exact accepted session/message framing plus host-to-proxy request grammar. Recover only that minimum, wire fresh data into the app, then test a second fresh acquisition, failure/disconnection, and no saved-capture fallback. Stop expanding replay infrastructure once it provides the needed regression coverage. Optional resource-name recovery must not replace connection work.

No Daybreak assignment: ESC-001 CLOSED, ESC-003 NOT_ASSIGNED, ESC-002/004/005 NEEDS_INFORMATION. The R7-001 evidence reports Computer Use initialization rejected by automatic review; its exact tool invocation/error transcript is not included. The register tracks that documentation gap. This is an environment restriction, not withheld user permission or automatic specialist work. No denied operation was retried during this audit.

## Validation

Current PM10 implementation validation: Release build has zero warnings/errors. Deterministic desktop suite reports `ok: true`, all five groups true and `failures: []`; the installed diagnostic is valid and game/launcher were stopped at test time. Review-10's earlier frontend/transport/replay validation remains the most recent UI proof. No second fresh scan was performed. An earlier rebuilt-app UI-capture attempt in this task was rejected by the environment's automatic review before launch and was not rerouted.

[Machine-readable audit evidence](../evidence/lwbridge-implementation/2026-09-10-pm-review-10.json) records checks and limits. All 47 acceptance rows are preserved. Delivery is a documentation/audit checkpoint, not a production feature implementation.

# R7-149 ? Scheduled Plunder owner retirement

**Date:** 2026-09-23
**Parent revision:** `a41037df4e105db62379c5bf799ba35f3a93a197`
**Scope:** remove Scheduled Plunder from the shipped rebuild while preserving ordinary read-only Truck/Dispatch scan, filter, status and navigation data.

## Result

Scheduled Plunder is retired end-to-end. The shipped product no longer exposes the `scheduledPlunder` result tab; Dispatch/Truck schedule, cancel, retry or Plunder Again controls; `map_plunder_jobs_list`; Dispatch/Truck plunder schedule/cancel commands; scheduler workers; Truck quick-rob / Dispatch plunder action executors; injected game-action request lanes; plunder change events; or durable Dispatch/Truck plunder job/history tables. Existing databases explicitly drop the three legacy scheduler tables during schema initialization.

Read-only semantics are intentionally retained: Truck and Dispatch target identity, goods/reward metadata, robbed/steal counts, protection/arrival/completion timing, plunderability/status filters, ordinary result browsing, cross-server navigation, Truck/Railway Follow, and Dispatch Alliance Share under its separate authorization boundary. Historical 0.3.1 recovery evidence remains provenance and is marked non-current.

Acceptance cases E03, E04 and E05 are now `retired_by_owner`, not pending live action. The current 47-case matrix is `2026-09-23-r7-acceptance-matrix-r7149.json`; all other case statuses remain unchanged from R7-145.

## Validation

- frontend generator `--check`: pass;
- generated API and Map panel `node --check`: pass;
- shipped-code grep for Scheduled Plunder commands/events/action lanes: zero matches;
- .NET Release build: 0 warnings / 0 errors;
- deterministic executable: `ok=true`, all six deterministic groups true, `failures=[]`;
- browser source parity: 36 checks pass after treating Scheduled Plunder retirement as an explicit owner override;
- City Excel removal browser check: pass;
- automatic scan strategy browser check: pass;
- R7-131 Map Data persistence/saved-server/Stop/Clear browser check: pass;
- R7-132 Auto navigation/Refresh/reconnect browser check: pass;
- R7-136 restart-ownership browser check: pass;
- normal production Release window smoke on the exported staged snapshot: `ok=true`, apphost SHA-256 `8A228D9AD619BB9406855A8CEBAECA3E10E397102B5153569CA43E50DE24A489`, Home/Map responsive, config/backup unchanged, final game/launcher/helper/desktop process counts zero, state-changing owner commands blocked.

No claim, plunder, share, attack, spend, collection, or other state-changing gameplay action was executed for this retirement checkpoint.

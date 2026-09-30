# LWB317-MAP-CATEGORIES-V22-001 — remaining v22 acquisition categories

**Date:** 2026-10-01  
**Branch:** `research/offline-controller`  
**Starting checkpoint:** `75515a5a853ba489d74661f0a84a378432639ff4`  
**State:** `AWAITING_REVIEW`

Resource was not rerun and its candidate policy was not changed.

## Combined live v22 acquisition

After a clean process/package preflight, one assistant-owned session ran a Fast
scan selecting `city, monster, truck, railway, dispatch, ghost, treasure` together.
It covered all 2,500 logical blocks with zero failed blocks and completed with no
native pending/dropped records. The server-2212 snapshot was:

| Kind | Snapshot rows | Result |
|---|---:|---|
| City | 6,850 | `LIVE_PROVEN` acquisition/summary/options/search; page 2 returned |
| Monster | 9,568 | `LIVE_PROVEN` acquisition/summary/options/search; page 2 returned |
| Truck | 61 | `LIVE_PROVEN` acquisition/summary/options/search; page 2 returned |
| Railway | 0 | `BLOCKED_BY_LIVE_STATE` for a positive-row proof |
| Dispatch | 18 | `LIVE_PROVEN` acquisition/summary/options/search |
| Ghost | 0 | `BLOCKED_BY_LIVE_STATE` for a positive-row proof |
| Treasure | 0 | `BLOCKED_BY_LIVE_STATE` for a positive-row proof |

Counts are a 2026-10-01 live snapshot, not immutable expected totals. Options
reconciled to the same counts and included real alliance, monster-name, Dispatch
level and Truck reward-item option data. Page 1 was queried for every category;
page 2 was queried when total rows exceeded 50. The run was then cleared and a
same-session current-client context read proved server 2212 remained healthy.

The official package was restored automatically; final SHA-256 is
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`,
the recovery journal is absent, and no task-owned game/LWBridge process remains.

Evidence:
`evidence/lwbridge-0.3.17/map/LWB317-MAP-CATEGORIES-V22-001/`.

## Canonical UI

The canonical Map table already routes all seven categories through the shared
`map_search`/sorting/pagination path. This stage also closes the restart/reopen
server-context gap found during independent review: MapDataPage now falls back to
the live current server supplied by App when no scan/browse server exists, so
durable saved rows can be queried after process reopen without inventing a
persisted browse-server preference.

Direct normal-WebView rendering of the four positive-row categories remains
`IMPLEMENTED_NOT_VALIDATED` in this checkpoint; the live acquisition/query proof
is native/current-client plus deterministic canonical-frontend contract coverage.
Railway/Ghost/Treasure remain live-state zero snapshots rather than fabricated
positive proofs.


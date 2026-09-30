# LWB317-MAP-SERVER-JUMP-001 — cross-server jump and history

**Date:** 2026-10-01  
**Branch:** `research/offline-controller`  
**Starting checkpoint:** `5092959b9c7a3bb6204ec7b23370933dcbb9e781`  
**State:** `AWAITING_REVIEW`

## Exact 0.3.17 contract

The recovered frontend implements the server switcher in the top bar, not inside
the Map table. It accepts server IDs `1..99999`, blocks while the game is offline
or a Map scan is active, exposes current/home/season/plunderable/recent servers,
and calls `server_jump`. A changed jump prepends the resulting server ID to a
unique five-entry recent list and persists it with `server_jump_history_set`.

The legacy frontend key `lastwar.serverJumpHistory` is migration input only.
On profile initialization the 0.3.17 frontend calls
`server_jump_history_import`; existing native history wins, then the legacy key
is removed. The frontend does not use `server_jump_history_get` for this flow.

The recovered native jump validates `1..99999`, returns
`{changed,previousServerId,serverId}`, treats the current server as a successful
no-op, serializes game operations, performs the protected jump request, and
polls authoritative server identity at 500 ms intervals for up to 10 seconds.

## Canonical frontend/backend

`src/LWBridge.UI-0.3.17` now wires the recovered top-bar server switcher to the
existing native `server_jump`, `server_jump_history_import`, and
`server_jump_history_set` commands. It renders current/home/season/plunderable/
recent server choices and enforces the recovered offline/active-scan guard.

The canonical Map317 `map_scan_status` response now refreshes current live server,
world presence, home server, season servers and truck-match servers from the
current-client status provider so the top bar is not forced to infer server
identity from a completed scan.

This checkpoint also corrects the Stage A production-path distinction discovered
during independent review: canonical restart ownership/reconciliation now lives
on the Map317 production path rather than relying on the retained legacy Manual
service.

## Live v22 proof

Preflight used the official validated v22 package. No LWBridge/Last War process
existed before the proof. The proof launched an assistant-owned game instance,
recorded home server `2212`, and read the live truck-match list
`[2198,2199,2205,2212]`. It selected `2198` from that live list rather than
guessing a server ID.

The same-server `2212 -> 2212` request returned `changed=false`. The cross-server
request then completed `2212 -> 2198`; authoritative current-client context
settled on `2198`; the return completed `2198 -> 2212`; and authoritative context
settled back on the original server. This native/current-client round trip is
`LIVE_PROVEN`.

The first automatic lifecycle stop timed out after the owned game had already
exited, leaving the package recovery journal at `closing_owned_game_for_restore`.
The established `run_overview_bridge.py stop` recovery path was then invoked with
the exact journal identity. It observed the game already exited, restored the
backup, cleared the recovery journal, and restored `LWScripts.data` to the
official SHA-256
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`.
Final process inventory is empty and the restored v22 compatibility postflight
passes.

Evidence is under
`evidence/lwbridge-0.3.17/map/LWB317-MAP-SERVER-JUMP-001/`.

## Classification

- exact server-jump public contract: `EXACT_CONTRACT`;
- canonical top-bar jump/history wiring: `IMPLEMENTED_NOT_VALIDATED` at direct
  normal-WebView click scope; deterministic frontend/native command coverage
  passes;
- current-client cross-server transition and return-home: `LIVE_PROVEN` on the
  assistant-owned v22 session (`2212 -> 2198 -> 2212`);
- same-server no-op: `LIVE_PROVEN`;
- history normalization/import/set and ordering/dedupe: `EXACT_CONTRACT` with
  deterministic storage/frontend coverage;
- arbitrary server reachability outside live advertised server lists: `UNKNOWN`.


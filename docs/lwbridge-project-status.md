# Current project status — Home and Map Data

**Date:** 2026-09-23
**Current checkpoint:** `LWB-R7-150`; parent revision `e594455ff43abe38d657f66a4eec3eb243e3585c`

## Executive status

The Home / Overview acceptance state is unchanged. Map Data received R7-147 owner-workflow corrections after direct testing exposed eight usability/correctness gaps. R7-148 additionally replaces Truck/Railway-only AOI scanning with the official direct Train-list source (0.45-0.57 s live acquisition). R7-149 owner-retires Scheduled Plunder end-to-end. R7-150 adds current-v20 live-proven covered cross-server Truck/Railway Auto acquisition without physical travel, while preserving **0 ordinary `partial` rows** and leaving the R7-149 acceptance-case statuses unchanged.

That does not mean every possible live action has been exercised. Remaining gaps are explicitly separated into population availability, the Alliance-share authorization boundary, one preserved protected-contract blocker, simultaneous multi-account availability, and final integrated release acceptance. Scheduled Plunder is no longer a remaining gate.

## Home / Overview

A01-A12 are closed at their recorded evidence levels. Production lifecycle, owned process handling, startup rollback, reconnect, status/bridge behavior, cross-server navigation, Release responsiveness, and normal user Home/Map navigation across restart all have current or composed evidence.

Current detail: `docs/tabs/home.md`.

## Map Data

Manual/Auto acquisition, Stop, restart safety, search/filter/sort/paging, marks, moving navigation, and native transition behavior remain accepted at their current evidence scopes. R7-147 adds session-scoped scan data, Auto **All**, one-shot multi-server Run Now while recurring Auto is disabled, cross-server row navigation, session-wide stopped Clear, Doom Walker Follow, the official current-v20 Train-list source, and removal of the Manual server filter. R7-149 removes Scheduled Plunder from UI/API/runtime/storage while preserving read-only Truck/Dispatch status/filter data. R7-150 uses the official Train-list `matchServers` set to bypass server jumps for covered Truck/Railway-only Auto targets; a live 2212 -> dataset 2182 proof completed in 0.809 s with the physical server unchanged.

Player City, Resource, Monster/Doom Walker, Zombie Boss, Truck, Dispatch, and ordinary Treasure retain positive live evidence. Railway retains historical positive live evidence, but the fresh R7-147 current-v20 official-list probes were empty on all sampled servers; a fresh positive Railway row is therefore population-dependent. Ghost and Supplies remain population-gated.

Current detail and performance audit: `docs/tabs/map-data.md`.

## Current acceptance counts

R7-149 current acceptance status counts:

| Status | Count |
|---|---:|
| `pass_current_offline` | 18 |
| `pass_current_plus_historical` | 10 |
| `pass_historical_live` | 5 |
| `pass_current_plus_live` | 1 |
| `pass_live` | 1 |
| `pass_current_normal_user` | 1 |
| `partial_population` | 4 |
| `partial_offline_authorization` | 1 |
| `blocked_implementation_authorization` | 1 |
| `blocked_implementation` | 1 |
| `retired_by_owner` | 4 |

## Remaining externally gated work

1. Ghost positive-row proof, owner-deferred until 2026-09-24.
2. Supplies positive-row proof when an authentic live population exists.
3. Treasure protected claim scheduler recovery remains blocked under SB-79; do not reroute the denied operation or invent the contract.
4. Simultaneous real multi-account UI population when multiple active accounts are available.
5. Fresh positive current-v20 Railway row/Follow when Train population is present.
6. Final integrated release acceptance.

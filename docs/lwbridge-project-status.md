# Current project status — Home and Map Data

**Date:** 2026-09-24
**Current checkpoint:** `LWB-R7-151`; parent revision `24ab001b9a444f15697c30e149f039a919cda5be`

## Executive status

The Home / Overview acceptance state is unchanged. R7-151 adds a v21 update-safe lifecycle (restore first, untouched official update, strict final validation, then injection) and accelerates Dispatch/Secret Task full scans to the proven 68-request wide path. A current-v21 server-2175 Dispatch proof completed in 7.028 s with persisted/reopened rows, and Auto Scan now surfaces one native read-only Quick Find result before the complete scan. The acceptance-case statuses remain unchanged with **0 ordinary `partial` rows**.

That does not mean every possible live action has been exercised. Remaining gaps are explicitly separated into population availability, the Alliance-share authorization boundary, one preserved protected-contract blocker, simultaneous multi-account availability, and final integrated release acceptance. Scheduled Plunder is no longer a remaining gate.

## Home / Overview

A01-A12 are closed at their recorded evidence levels. Production lifecycle, owned process handling, startup rollback, reconnect, status/bridge behavior, cross-server navigation, Release responsiveness, and normal user Home/Map navigation across restart all have current or composed evidence.

Current detail: `docs/tabs/home.md`.

## Map Data

Manual/Auto acquisition, Stop, restart safety, search/filter/sort/paging, marks, moving navigation, and native transition behavior remain accepted at their current evidence scopes. R7-151 keeps the R7-150 direct Train-list behavior and adds the current-v21 Dispatch fast path: 68 aligned wide AOI requests with exact 10,000-cell coverage. The live server-2175 proof completed in 7.028 s; Auto Dispatch additionally emits one native Quick Find coordinate before the complete scan without storing that one-target result as authoritative map data.

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

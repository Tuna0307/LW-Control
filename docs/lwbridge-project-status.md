# Current project status — Home and Map Data

**Date:** 2026-09-22
**Current checkpoint:** `LWB-R7-145`; parent revision `eb6f36babdd57a6236f0b96d42d647a07c769f4c`

## Executive status

The ordinary Home / Overview and Map Data implementation is complete at the scopes represented by the current 47-case acceptance matrix. The matrix has **0 ordinary `partial` rows**.

That does not mean every possible live action has been exercised. Remaining gaps are explicitly separated into population availability, state-changing authorization/target availability, one preserved protected-contract blocker, simultaneous multi-account availability, and final integrated release acceptance.

## Home / Overview

A01-A12 are closed at their recorded evidence levels. Production lifecycle, owned process handling, startup rollback, reconnect, status/bridge behavior, cross-server navigation, Release responsiveness, and normal user Home/Map navigation across restart all have current or composed evidence.

Current detail: `docs/tabs/home.md`.

## Map Data

Manual/Auto acquisition, persistence, multi-server saved data, Stop, restart safety, Clear race handling, search/filter/sort/paging, marks, coordinate Jump, moving Follow, and native transition behavior are accepted at their current evidence scopes.

Player City, Resource, Monster/Doom Walker, Zombie Boss, Truck, Railway, Dispatch, and ordinary Treasure have positive live evidence. Ghost and Supplies remain population-gated, not implementation-failure-gated.

Current detail and performance audit: `docs/tabs/map-data.md`.

## Current acceptance counts

R7-145 status counts (unchanged from R7-144):

| Status | Count |
|---|---:|
| `pass_current_offline` | 18 |
| `pass_current_plus_historical` | 10 |
| `pass_historical_live` | 5 |
| `pass_current_plus_live` | 1 |
| `pass_live` | 1 |
| `pass_current_normal_user` | 1 |
| `partial_population` | 4 |
| `partial_offline_action` | 2 |
| `partial_offline_authorization` | 1 |
| `not_run_authorization` | 1 |
| `blocked_implementation_authorization` | 1 |
| `blocked_implementation` | 1 |
| `retired_by_owner` | 1 |

## Remaining externally gated work

1. Ghost positive-row proof, owner-deferred until 2026-09-24.
2. Supplies positive-row proof when an authentic live population exists.
3. Treasure protected claim scheduler recovery remains blocked under SB-79; do not reroute the denied operation or invent the contract.
4. Suitable explicitly authorized live Truck/Dispatch plunder and Alliance-message actions.
5. Simultaneous real multi-account UI population when multiple active accounts are available.
6. Final integrated release acceptance.

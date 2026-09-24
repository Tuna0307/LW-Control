# Map Data — strict parity status

**Current through:** `LWB-R8-004`, 2026-09-24.

This page supersedes the former performance-oriented Map status. Map Data is now judged only against the original LWBridge 0.3.1 behavior.

## Direction correction

Do not continue designing a new scanner.

Do not use LW Atlas as product authority.

Do not promote a Last War-native API, manager, finder, wide-FOV trick, no-jump route, or other optimization merely because it is faster.

Recover the original LWBridge Map implementation first. Current-game APIs are allowed only as compatibility mechanisms for reproducing that recovered behavior.

## What is actually exact today

| Area | Parity classification | Note |
|---|---|---|
| Original Map frontend component family | EXACT_BYTES-derived | Original chunks/assets recovered, but rebuild transformations/customizations remain |
| `startMapScan` request fields | EXACT_CONTRACT | `scanRunId/serverId/worldId/scanMode/concurrency/selectedTypes/tileWidth/tileHeight` |
| Accepted gate / 5 s bridge-call behavior | EXACT_CONTRACT | Recovered from original host |
| Selected type allowlist | EXACT_CONTRACT | city/resource/monster/truck/railway/dispatch/ghost/treasure |
| Normal/Fast concurrency | EXACT_CONTRACT | normal=8, fast=20 |
| Native capture vocabulary/hooks | PARTIAL EXACT_CONTRACT | Recovered from original proxy |
| Query/storage/filter contracts | PARTIAL EXACT_CONTRACT | Significant original SQL/normalization recovered |
| Original acquisition algorithms | UNKNOWN | Current production scanner is our reconstruction |

## Known deviations

- R7-151 Secret Task Quick Find was a rebuild-only product feature.
- R7-151 wide-FOV/68-request scan strategy was our optimization, not a recovered original algorithm.
- Direct Train-list/no-jump routing is a current-game optimization until reference evidence proves original equivalence.
- City Excel export was removed despite being part of the original product.
- Scheduled Plunder surfaces/workers were removed despite being part of the original product.
- Manual/Auto UX and other owner workflow changes made during R7 must be audited against the reference rather than retained automatically.

## Completeness regression lesson

The wide-FOV work could report complete logical coverage while returning only tens of Player Cities where the older complete traversal returned thousands. This proves that our own coverage model is not a substitute for original behavior or semantic completeness.

The old complete traversal can remain temporarily as a reference oracle while original Map code is recovered, but it is not automatically the final parity implementation either.

## Per-kind parity status

| Kind | Current implementation status | Parity status |
|---|---|---|
| City | Working reconstruction; full population possible with older traversal | Original acquisition UNKNOWN |
| Resource | Working reconstruction | Original acquisition UNKNOWN |
| Monster / Doom Walker | Working reconstruction | Original acquisition UNKNOWN |
| Zombie Boss | Working dedicated reconstruction | Original acquisition UNKNOWN |
| Truck | Working direct-list reconstruction | Original source/semantics UNKNOWN |
| Railway | Working direct-list reconstruction | Original source/semantics UNKNOWN |
| Dispatch / Secret Task | Current R7 optimization regressed semantic completeness in broader use | Original acquisition UNKNOWN |
| Ghost Ops | Working/partially population-gated reconstruction | Original acquisition UNKNOWN |
| Treasure / Supplies | Strong partial contract recovery | Protected/orchestration pieces UNKNOWN |

## Immediate P0

1. Recover `bridge-scripts.dat` and original map handlers.
2. Recover exact host/proxy scan control and result grammar.
3. Identify original acquisition path for every selected type.
4. Restore original Map UI/features previously removed.
5. Remove every rebuild-only Map feature.
6. Compare original and rebuild results/identities/errors/defaults.
7. Only then consider invisible performance work that preserves exact parity.

Historical R7 performance and acceptance data remains evidence, not current product authority.

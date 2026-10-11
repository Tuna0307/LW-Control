# MAP-008 — current scanner method, accuracy and delivery ledger

2026-10-11. Working branch `codex/map-action-providers-007`, existing PR #9.
Historical lead negatives and accepted MAP-006 R1 release remain unchanged.
This document is the **single current MAP-008 continuation**. Genuine measurements
below refer only to explicitly identified official client v24 sessions; production
planner/engine checks use **controlled providers** unless marked otherwise.
The encrypted original's traversal method, global optimality and perfect recall
have **not** been established.

### Compact behavior differences from accepted branch checkpoint

| Surface | Earlier behavior | Current MAP-008 behavior |
| --- | --- | --- |
| Deferred Ghost/Treasure controls | A superseded preview/preparation could remain busy or surface on another tab | Exactly owned operation retirement, result fencing across tab/API/server, successor-safe busy clearing |
| Scan mode | Both modes used the same adaptive wide-step spatial placement despite requested 8/20 concurrency | Normal conservative gap placement, Fast only measured-wide placement, same full coverage gate; actual camera concurrency measured and logged |
| Scan validation | Nonzero captured world could pass when requested world was 0 | A mismatching nonzero world is rejected even for requested world 0 |
| Live diagnostic | Probe count and AOI union, insufficient mode provenance | Adds source run + selected kinds/mode, placement policy, requested limit, actual peak camera probes; packet count remains unknown |
| Released workflows | Accepted Home, six Map kind browse/store, Auto Stop-before-return | Same controls and records preserved, with new non-executing preparation/preview lifetime protection |

## Corrected reviewed defects

- **LEADMAP007-01:** preparation/preview operations hold explicit owner objects.
  Retiring the exact operation releases its own busy flag after tab, server or
  API/profile replacement; a completed obsolete owner cannot release a newer
  owner's flag.
- **LEADMAP007-02:** success/error acknowledgement requires the same operation
  reference, API, browse server and tab. Late responses cannot appear on a
  replacement tab. Mounted actual `MapDataPage` with controlled held provider
  promises: 8/8 tests, EN/light and JA/dark with narrow viewport; includes
  leave/return, reject/succeed, server/API replacement and successor overlap.
  `docs/proof/MAP-008-MOUNTED-LIFETIME-2026-10-11.json`.
- Immutable historical failures:
  `docs/proof/MAP-007-LEAD-LIFETIME-2026-10-11.json`,
  `tools/map_007_lead_review/check-ui-lifetime.mjs`.

## Acquisition and accuracy requirements

The standard world (1000×1000 tiles) uses one **serial fresh LOD0 native
WorldPointManager/AOI sweep** of precisely 10,000 native AOI cells, projected
into 2,500 logical checkpoints. One scan covers every selected spatial kind
from the **same fresh native footprints**; type-specific work runs after coverage.
Actual `MapScanEngine` is serial; configured Normal 8 and Fast 20 are admission
limits, not actual parallel view requests. For the standard world, the current
planner uses `current_fast_full_world_v2` except Truck/Railway-only selection,
which carries the historical `current_fast_train_list_v1` strategy **label**.
The production capture implementation still enters the full-world AOI sweep;
the standalone `CaptureFullTrainListMapAsync` shortcut is not called there.
Do not claim a zero-camera whole-Truck/Railway scanner from that label.

### Selection methods and limits

| Selected kind | Fresh baseline / type-specific work | Other candidate and established limit |
| --- | --- | --- |
| City/player | Validated native spatial point + city identity, server, coordinates, owner/level; staged once per full-world AOI union | No complete scoped official full-server city list proved; local city cache cannot establish fresh recall |
| Resource | Shared AOI spatial record + idle-resource detail enrichment and completeness counters | Direct region still requires native view lifecycle; no validated complete server-wide list |
| Monster | Shared native AOI point/march/monster snapshot + optional protection enrichment | The specialized LOD2/coarse Zombie Boss path omits generic Monster/Doom Walker families; not a generic Monster substitute |
| Truck | Shared AOI train/march locations and owned reward/max-loot enrichment | Refreshed official Train list contained only foreign-server Trucks in R1 paired test; cannot substitute for target-server Truck population |
| Railway | Shared spatial acquisition + one fresh official `LWTrainDataManager.TryGetTrainList(true)` response, merged by exact march identity | Train list is valid for its native scope but has not independently proved complete target-world enumeration without AOI |
| Secret Task / Dispatch | Shared spatial task identity, expiry/completion and plunder state fields | No complete server-wide task list verified; action scheduling is outside the passive scanner |
| Ghost | Shared Ghost recon native AOI point/task projection | Positive current-client Ghost population remains **owner-deferred**, so no live recall claim |
| Treasure | Shared AOI typed treasure point + read-only status inspection where supported | Full seasonal population/claim confirmation remains owner-deferred; previews never establish a claimed reward |

For **City + Resource + Monster**, one spatial AOI pass supplies all three,
followed by optional resource and monster detail work. For **Truck + Railway +
Dispatch**, one pass supplies Truck and Dispatch, plus the fresh Railway list
and Truck hydration. For **City + Truck** one pass supplies both, plus Truck
hydration. For **all six available** and **all eight selectable**, one pass
supplies every selected kind, followed by required list/details as applicable.
All 255 nonempty selections pass the production planner and engine with
controlled identity-distinct witnesses. This is selection/ingestion proof,
**not** 255 independent native-world acquisition measurements.

The full AOI population requires the **active target server**. Known
cross-server Train-list source fields depend on matchServers/event scope and
cannot justify full coverage of an arbitrary target server by ID alone.
Current-supported source inspection has not proved a complete without-switch,
without-camera path for any of the eight *full target-map populations*.
Earlier direct `SendAoiRequest` trial returned no fresh response (8.065 s
timeout), versus a confirmed view-assisted region response; a paired official
Train-list refresh completed in 305–355 ms but lacked the 178 same-server
Trucks in the fresh AOI baseline. These reject the **tested** substitutions,
not every possible future supported method; see
`docs/proof/MAP-006-R1-STOP-EFFICIENCY-2026-10-11.md`.

Acceptance gates: native build and owned profile/session/challenge/game PID;
source-specific response ID, fresh capture time, live target server and world;
record primary identity, required values, AOI union or proven complete list
scope/paging, no extra/omitted/duplicate known witnesses, correct selected
kind, no stale result; natural movement/expiry observed between bracketed
times is reported rather than silently normalized. Camera tile, LOD/FOV/aspect,
native manager indices and original installation must restore. A complete
block count, source count or timestamp alone **does not** establish world
recall. All known nonzero capture world IDs must now match the requested world,
including world 0. The zero-capture-world legacy sentinel remains accepted.

## Normal / Fast: engineering result, not a marketing label

Normal uses conservative AOI placements ahead of the first native coverage
gap; Fast advances farther only after an actual native footprint in the band
proves at least four columns. Both require exact 10,000-cell fresh union and
retain serial, owned camera mutation, bounded retries and restoration.
The runtime records **measured** peak in-flight camera probes (one for this
source), actual Lua probe attempts/retries and response waits. No artificial
sleep is introduced for Normal. Controlled simulation of 100-cell rows:
2/3-column footprints require 50 probes in both modes; 4-column footprints
33 Normal / 25 Fast; 5-column footprints 25 Normal / 20 Fast.
These counts are controlled geometry tests and do not predict genuine v24 speed.

The first matched genuine v24 **server 2212, all eight selected** pair is:

| Measure | Normal | Fast |
| --- | ---: | ---: |
| Owned run | `98c69b1f53904f12a7c023350bacdcf3` | `377f9662dde2418aa154e954bf119553` |
| Full run ms (SQLite lifecycle) | 227318 | 222542 |
| AOI acquisition ms (native producer) | 219925 | 218182 |
| Application→Lua native AOI attempts / retries | 340 / 0 | 340 / 0 |
| Validated native AOI responses | 340 | 340 |
| AOI union / logical blocks | 10000 / 2500 | 10000 / 2500 |
| Failed/staged at finish | 0 / 0 | 0 / 0 |
| City / Resource / Monster published | 7062 / 8000 / 8799 | 7062 / 8001 / 8777 |
| Truck / Railway / Dispatch | 260 / 0 / 1222 | 280 / 0 / 1203 |
| Ghost / Treasure | 0 / 50 | 0 / 49 |

Both finished with unique stored primary keys, same server/selection and no
partial publication. The 4.776-second Fast advantage on this single order is
**not a qualified repeatable speedup**; the **same 340 AOI requests** indicate
the measured wide-step policy was not used to reduce this run's work. Per-kind
population differences may reflect spawning, movement, task/Truck expiry and
other client activity over the minutes separating runs. A second, reversed
order and independent record witnesses are still needed for a meaningful
median/range or any accuracy-equivalent mode preference. Native probe counts
are *application-to-Lua commands*, **not raw server packets**. Raw packet
attribution is unavailable, not estimated.

The task-private detailed run receipts and game data live under
`artifacts/map-006/trial-scan008_20261011/`. Genuine cleanup and final
candidate hashes are recorded after the trial finishes.

**Fresh combined-type native comparisons from the same isolated session:**

| Workload | Mode, server | Run lifetime | Coverage / Lua AOI attempts | Published selected-kind counts |
| --- | --- | ---: | --- | --- |
| City + Resource + Monster | Fast, 2212 | 208.337 s | 10000 AOIs, 340 attempts, 0 retries | City 7061, Resource 7989, Monster 8784 |
| Truck + Railway + Dispatch, Auto target one | Fast, 2212 | 111.450 s | 2500 blocks, 0 failed | Truck 182, Railway 1, Dispatch 194 |
| Truck + Railway + Dispatch, Auto target two | Fast, 2213 | 108.833 s | 2500 blocks, 0 failed | Truck 99, Railway 0, Dispatch 220 |
| City + Truck | Normal, 2212 | 107.900 s | 10000 AOIs, 340 attempts, 0 retries | City 7062, Truck 207 |
| All six available | Fast, 2212 | 206.171 s | 10000 AOIs, 340 attempts, 0 retries | City 7062, Resource 7992, Monster 8841, Truck 271, Railway 0, Dispatch 1214 |
| All six available | Normal, 2212 | 206.276 s | 10000 AOIs, 340 attempts, 0 retries | City 7063, Resource 7999, Monster 8845, Truck 265, Railway 0, Dispatch 1213 |

Mixed Auto cycle `1791717824458..1791718049836` (225.378 s) finished
**both** target runs, zero scan failures/staging, with task-native source
`SceneUtils.ChangeToWorld+GoToUtil.CheckCrossWar+CrossServerUtil.OnBackSelfServer`
return proving 2213 → original **2212**; original and restored server match,
Auto disabled afterward, zero armed jobs. The single server-switch/return
overhead attributable to the cycle's total minus the two scan-run lifetimes
is **5.095 seconds combined**; separate switch and return spans were not
measured independently. Receipt:
`artifacts/map-006/trial-scan008_20261011/auto-cycle-truck_rail_dispatch_two_servers.json`.
Zero Railway in the second target means no positive Railway field witness
there; the first target published a positive official-list-backed Railway.

For City/Resource/Monster, the task copied a native **fresh response** from
`WorldPointManager` during the actual scan (requestId tied to the same owned
profile/session and game PID; server 2212, protected current v24). An
independent post-run read-only comparison against the published SQLite map
found **41/41 exact sampled City/Resource identity, tile and level** matches
(18 City and 23 Resource native rows, zero sample omissions/field mismatches).
The sample was neither generated from publisher records nor counted as a
whole-world oracle; monster native march identities require separate source
comparison. Source/receipt:
`tools/map_008_scan_checks/audit_native_region.py`, task-private
`city-resource-monster-native-region-sample.json` and
`city-resource-monster-sample-audit.json`. Time-based changes to moving
objects and partial witness scope remain explicit limitations.

The native AOI phase for the City/Truck Normal pass took **107.174 s**, about
half the City/Resource/Monster Fast AOI phase (**204.391 s**) even though both
used exactly 340 fresh response probes. The differing response payload and
optional per-kind native processing, rather than scanner concurrency, is a
significant measured workload cost. Different workloads and modes mean these
two durations are **not** a controlled head-to-head Normal/Fast speed ratio.

Six-kind ordering reversed the all-eight mode order: **Fast then Normal**,
both server 2212, native AOI phases **201.829 s / 201.935 s**, full run
**206.171 s / 206.276 s**. Each required the same 340 native AOI probes,
zero retries and serial camera operations; both independently published
2,500/2,500 logical blocks and left no staging. The **0.105 s** observed
difference is not a credible production speedup claim. All-eight Normal→Fast
and six-kind Fast→Normal pairs each have **n=1 per mode per workload**;
neither supplies repeated same-workload median/range, so the remaining
repeatability gate stays open. Scan timing is SQLite run lifecycle;
acquisition elapsed is source probe wall time, and staging/publication includes
the residual span without separately attributing CPU, hydration and I/O.

**Live cleanup receipt:** task-native Home Close retired the exact PID 36980;
the source-built Desktop host PID 22660 was closed afterward. At
2026-10-11T12:00:04Z `artifacts/map-006/trial-scan008_20261011/finish.json`
confirmed zero task actors, exact equality of three restored installed Lua
file hashes to preflight backups, no recovery journal, Auto disabled/idle,
zero queued action jobs. No live attack, claim, plunder, alliance share,
march, spending or updater action was performed.

## Controlled coverage/adversity and remaining gates

`tools/map_008_scan_checks` uses the **actual** planner and engine, testing
255 nonempty kind selections, correct staged selection populations and 15
distinguishing rejections: wrong server/world, unselected kind, malformed
or duplicate/missing block, missing payload, partial/native error, omitted
witness identity, dropped kind, extra/duplicate/wrong-valued/stale/wrong-server
records. Independent witness comparisons detect omitted and wrong-field
records that no mere block counter could detect. Held cancellation prevents
publication. Repeated two-server dense eight-kind controlled scans exercise
serial backpressure and retained managed-memory bounds. This is
`docs/proof/MAP-008-SCAN-CONTROLLED-2026-10-11.json`; no game commands.
Affected MAP-006 Auto exact Stop-before-return, uncommitted failure and
successor guards remain covered by the default native Release suite and the
separate Windows named-pipe recovery regression.

**Executed current-source checks after live cleanup:** canonical frontend
`npm.cmd run check`, `npm.cmd run build`, `npm.cmd run check:production-build`,
`npm.cmd run check:map008-lifetime` all pass; `dotnet build
src/LWBridge.Desktop/LWBridge.Desktop.csproj -c Release` passes with zero
warnings/errors; `dotnet run --project tests/LWBridge.Desktop.Checks -c
Release` passes Home, Map, Auto and Action provider regressions; the actual
Windows `tools/home_004_pipe_recovery_probe` passes with zero game launches;
`tools/map_008_scan_checks` passes 255 selections, 15 bad-response guards
and repeated dense stress. `npm ci` on the pinned lockfile now installs
`jsdom` for independently reproducible mounted ownership checks.

**Open for MAP-008 acceptance:** repeat same-workload genuine Normal/Fast
measurements with median/range on representative *individual* kinds and
combined workloads; independent native sampled identity/field witnesses for
each supported kind (including Monster march, Truck goods, Railway event,
Dispatch timers), actual client pressure/camera restoration and older-result
age observations; real bounded multi-server *individual* modes and separately
timed return overhead; final extracted candidate verification. Some truth-oracle
values are not available at client scope and remain explicit proof limits.
Ghost positive and full seasonal Treasure live acceptance remain deferred.
Even if all controlled checks pass, do **not** return READY while these
available functional comparison gates remain open.

## Owner review sequence

After independent lead review, use the source-identified candidate only.
Keep one supported game session and verify **Connected**, current server,
Home and Map current-state ownership. Choose City/Resource/Monster, then
Truck/Railway/Dispatch, then all available kinds, testing Normal then Fast
without clearing completed data. Watch the count and progress of **each**
selected kind, stop and restart, browse/filter/page/export, then enable a
bounded reachable two-server Auto plan with Return on, confirm every target
run completes, stop Auto and verify the original server. Ghost and Treasure
previews are non-executing. Do not interpret zero Ghosts, unloaded seasonal
Treasures or an action preparation result as a completed live action.

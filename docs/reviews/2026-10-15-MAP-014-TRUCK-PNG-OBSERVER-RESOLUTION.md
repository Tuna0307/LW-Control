# MAP-014 — Truck PNG observer failure resolved by native arrival provenance

**Review scope:** lead correction `ba777a803f936a34dd5c643167d252f6b184e1bf`
on existing `codex/map-owner-fixes-014` / draft PR #10. This document
separates the available product behavior, the actual WebView witness, and
the test observer. It does not approve whole Map or account actions.

## Reproduced failure and definitive diagnosis

The new `ba777a80` source-pinned Windows package reported no loaded native
Truck PNG in **two** genuine saved-dataset WebView executions:
`artifacts/map014-takeover/native-ui-20261015-004810/` and
`native-ui-20261015-005301/`. Both had accepted `map_search` replies,
positive Other/warzone/Resource/Dispatch gates, and an exit-code-1 image
timeout after approximately 41 seconds. Both sessions restored exact
original v26 scripts, exited all processes and left no recovery journal.

An independently run **negative control using the previously successful,
unmodified `19b818c5` extracted EXE** also failed the same image gate on
2026-10-15 at `native-ui-20261015-010440/`. This rules out the
Railway/moving-Monster timer patch as a direct cause. The sole product
source delta between those revisions was expansion of a table clock to
Railway/Monster; all reward markup, game-image reader/cache, native
asset transport, Lua and WebView proof gate were unchanged.

**Source-backed cause:** `MapStore.Query.cs:409-413` intentionally excludes
Truck/Railway rows when known `arriveTs <= nowUnixMs`. The cloned database
still has **193** genuine retained Truck records with actual `currentGoods`
and native `iconPath` strings, but every native Truck arrival was at/before
2026-10-14 17:58:27.833 UTC. A read-only reproduction of the exact SQL
predicate gives **38** eligible Truck records during the previously
successful extracted-app PNG proof (2026-10-14 16:17:54 UTC), **zero** at
both new failures (2026-10-15 00:50:12 and 00:54:28 UTC) and **zero** in
the old-executable negative control. See ignored raw receipt
`artifacts/map014-final01/expiry-source-truth.json`.

The old test observer at `LWBridgeWindow.Map014Proof.cs:358-360`
accepted a settled `map_search` but **never required any time-eligible
Truck row** before waiting for
`img.map-reward-icon.complete && naturalWidth > 0`. An empty table has no
reward `GameAssetImage` component and cannot issue or render the requested
PNG. Both negative replays have **no** task-local
`live-resource/asset-image-result.json` or
`profiles/map014-native-ui/asset-cache/` directory. This is consistent
with zero visible reward rows; it is not evidence of an image request
rejected by the native asset pipeline.

The genuine earlier extracted proof
`native-ui-20261014-161655/` retains **29 actual decoded PNG images**,
16 session-produced native cache PNGs and a correlated image-probe result,
plus all eight Map UI gates. It is historical positive evidence only, not
a current fresh Truck population.

## Corrected test observer and safe regression

New `Map014TruckImageWitnessClassifier` requires actual positive
`map_search.total`, visible page rows and native nonblank reward icon
sources before the isolated WebView test claims that a Truck image should
render. With no unexpired source rows it records
`native_truck_image_positive_unavailable`, captures the real empty table
and fails clearly with `TRUCK_POSITIVE_WITNESS_UNAVAILABLE` **without
claiming a broken PNG**. This is not a test bypass or fake success: the
full eight-gate positive result remains unavailable from expired rows.

When a genuine positive is present, the prior strict requirement for an
actual fully decoded PNG (`naturalWidth>0`) remains mandatory. A positive
row with a real icon path that fails to render now captures bounded DOM,
source row, cache/probe existence and screenshot diagnostics before
failing. No input source, expiration policy, image renderer, native asset
code, game language or game scripts have been changed.

`Map014TruckImageWitnessChecks` covers the expired/empty source, positive
native icon identity (including duplicates), positive total with no page
rows, absent icon metadata and malformed total. Full Release .NET/Home/Map
suite and full frontend suite including lead Railway/Monster deadline tests
are expected to be rerun for the final candidate.

## Final acceptance classification

The observer's earlier generic PNG failure is **explained and corrected**.
Independent available-scope acceptance can rely on previous genuine
positive native-image and same-UUID Truck witnesses only at their dated
scope, current stored-data/filter evidence, and the independently mounted
Railway/Monster deadline correction. An **all-eight-gates positive replay
of a newer candidate requires fresh, naturally unexpired Truck data**:
without that data the correctly instrumented test reports
`TRUCK_POSITIVE_WITNESS_UNAVAILABLE`, not PASS.

The scanner remains City-only FOV20 under qualified standard-world
geometry and guarded serial LOD0 for the remaining kinds and mixes.
No new full-world scan or changed live account action is warranted just
to satisfy a stale witness. Positive Ghost, Celebration Party,
moving Doom/Train expiry, positive-staged native Stop, authenticated
Ghost terminal, real Treasure claim/quota/reward and owner-only
Dispatch/Truck share/plunder actions retain their independent
**PARTIAL/PENDING** status. Preserve persisted Auto preferences and
the original Chinese-language game, v26 scripts and exact sessions.

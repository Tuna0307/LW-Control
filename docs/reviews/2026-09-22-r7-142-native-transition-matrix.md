# LWB-R7-142 — Native point/march transition matrix

**Date:** 2026-09-22
**Base revision:** `e4059b3aea215815fc75c1b50cf1aa978dd545f3`
**Acceptance case:** B11
**Status:** PASS_CURRENT_PLUS_LIVE

## Goal

Close B11:

> Native point/march add/update/remove/movement -> correct stable identity, index updates/removals, and query changes.

R7-142 uses two complementary paths:

1. a current deterministic end-to-end transition matrix through the production fast native parser and transactional index; and
2. a current read-only same-session live Truck comparison on server 2212.

No production behavior change was required.

## Production identity rules exercised

The current scanner normalizes source identities as follows:

- Dispatch point records use the native positive `pointId` as `recordKey`;
- Truck/Railway moving march records use the exact native march UUID text as `recordKey`;
- the 64-bit UUID text is not converted to a lossy JavaScript number.

The transition regression uses those exact production rules.

## Deterministic point + march transition matrix

Path:

`CurrentClientMapBlockSource -> MapScanEngine -> MapDataStoreScanSink -> MapDataStore.SearchIndexed`

Both runs are full-world:

- server 2212
- world 0
- 1000 x 1000
- 2,500 logical blocks
- selected types: Dispatch + Truck

### Scan A

Dispatch:

- stable point `90011`: level 5, quality 3
- temporary point `90012`

Truck:

- stable march `truck-stable` at `(9,9)`, point index `9010`
- temporary march `truck-remove`

The published query sets contain exactly those two point identities and two march identities.

### Scan B

Dispatch:

- `90011` remains the same point identity but updates to level 7, quality 5, special=true
- `90012` disappears
- `90013` appears

Truck:

- `truck-stable` remains the same exact UUID/record key but moves across the map to `(985,985)`, point index `985986`, quality 5, power 2000
- `truck-remove` disappears
- `truck-added` appears

After the completed transactional publication:

- old point/march identities are absent;
- new identities are present;
- the stable point is updated in place;
- the stable Truck has one row only, under the same UUID key, at its new position;
- public indexed query results expose only the second snapshot.

This covers add, update, remove and movement without bypassing native-shaped parsing or transactional replacement.

## Current live transition observation

The maintained `--live-native-transition-proof` harness ran two read-only Fast Truck scans in the same owned current-client session on server 2212.

### Snapshot A

- run ID: `51020280a40d41fc8d318585097bc856`
- strategy: `current_fast_full_world_v2`
- concurrency: 20
- 2,500/2,500
- zero failed/unread
- 244 published Truck rows
- 85.7687769 s scan wall

### Snapshot B

- run ID: `cba878348eb148e5bd16c668a34b8d0e`
- same strategy/concurrency
- 2,500/2,500
- zero failed/unread
- 255 published Truck rows
- 76.929701 s scan wall

### A -> B native identity delta

- common exact march UUIDs: **58**
- added UUIDs: **197**
- removed UUIDs: **186**
- moved common UUIDs: **58**
- common UUIDs with movement: **58 / 58**
- metadata-only changes in the compared normalized columns: 0

Every live indexed Truck row retained:

- `recordKey == native march UUID`
- exact 64-bit UUID text

Example movements:

- `1417409933821387962`: `(545,756) -> (556,772)`
- `1417409934031103048`: `(934,169) -> (953,160)`
- `1417409934039491603`: `(389,471) -> (400,487)`

This is direct live evidence that a real moving march keeps stable identity while its indexed position changes, and that native add/remove churn is reflected by the next completed scan.

## Admission-gap behavior observed

Two first-attempt transient Overview admission gaps occurred during the live proof:

- target `(845,175)`, rowStart 10
- target `(5,875)`, rowStart 80

Both were `overview_session_unavailable` and recovered through the existing bounded same-session retry path. Neither scan published failed or unread blocks.

## Safety

The live R7-142 proof performed only read-only map scanning.

It did not invoke:

- Follow
- attack or plunder
- claim or collection
- messaging

## Cleanup / installed package

After the proof:

- game running: false
- launcher running: false
- installed package version: 20

Original hashes were restored exactly:

- `LWScripts.data`: `FEDD635A7F972843B72D274128E2D443D81463272D86497E5A8A32223C6BB7A9`
- `LWScripts.txt`: `FDC4DCD824C5EBC9E36DBEBD10A733588A01DEBF07E7E2014EBBE8ACAAB21F7F`
- `version.txt`: `F5CA38F748A1D6EAF726B8A42FB575C3C71F1864A8143301782DE13DA2D9202B`

## Permanent regressions / harness

- `tests/LWBridge.Desktop.Checks/CurrentClientMapBlockSourceChecks.cs`
- `tests/LWBridge.Desktop.Checks/LiveNativeTransitionProof.cs`
- `tests/LWBridge.Desktop.Checks/Program.cs` exposes `--live-native-transition-proof`

## Validation

Current R7-142 validation before packaging:

- Release build: **0 warnings / 0 errors**
- all six deterministic groups: **true**
- deterministic failures: **[]**
- live harness exit code: **0**
- live harness total runtime: **211.86 s**
- game/launcher cleanup: **zero**

## Acceptance effect

**B11 -> PASS_CURRENT_PLUS_LIVE**

The live evidence directly proves native march add/remove/movement. The point add/update/remove and full query/index replacement matrix is current deterministic evidence through the same production native parser and transactional store.

After R7-142, **F04 is the only acceptance row left in ordinary `partial` state**. Population-, authorization- and implementation-blocked rows remain separate categories.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-native-transition-matrix.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7142.json`

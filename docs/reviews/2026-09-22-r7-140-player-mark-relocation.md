# LWB-R7-140 — Player mark survives rescan/restart and relocates

**Date:** 2026-09-22
**Base revision:** `b07d9ffa04b007445098787fe6a6553b672f186b`
**Acceptance case:** C04
**Status:** PASS_CURRENT_PLUS_HISTORICAL

## Goal

Close the full owner-facing sequence:

mark -> rescan -> application restart -> find marked Player City -> relocate -> unmark -> restart

without pretending a mark is tied to stale coordinates.

## Current deterministic sequence

The permanent regression `tests/LWBridge.Desktop.Checks/PlayerMarkRelocationChecks.cs` uses a real file-backed SQLite map store.

Committed-checkout regression SHA-256: `1E35DAB51FA99017530C7B29B55C15D0B9E4716AF3ED1E9444BA4C3B550BF500`.

Fixture identity:

- server: 2212
- stable owner UID: `12345678901234567890`
- initial City position: `(111,222)`
- moved City position: `(333,444)`

### 1. Public mark

The test invokes `map_player_mark_set` through `LWBridgeBackend` and marks the City using recovered stable identity:

`(serverId, ownerUid)`

The mark is persisted independently from `map_records`.

### 2. Transactional rescan/move

A real `MapDataStoreScanSink` run publishes a replacement City dataset:

- old record key `city-old-position` disappears;
- new record key `city-new-position` is published at `(333,444)`;
- the mark remains because it is keyed by stable server/owner identity, not positional record key.

### 3. File-backed restart

The store is disposed and reopened.

A public `map_search` with `markedOnly=true` returns exactly one row:

- owner UID matches the marked owner;
- server remains 2212;
- coordinates are `(333,444)`;
- marked overlay is true.

The old mark-time coordinates are not used.

### 4. Public relocate routing

The reopened row supplies the relocation payload.

The command is invoked through the real `LWBridgeBackend.InvokeAsync("map_coordinate_jump", ...)` boundary using an `INativeAsyncCommandService` recorder.

Observed:

- calls: 1
- server: 2212
- x: 333
- y: 444
- returned coordinates: 333,444

Thus the owner-facing relocate action after restart is derived from the newly indexed City row, not stale mark metadata.

### 5. Public unmark and second restart

The test invokes `map_player_mark_set(marked=false)`.

After another file-backed reopen:

- `markedOnly=true` returns zero rows;
- the moved City remains published;
- unmark does not delete or revert the City record.

## Live navigation authority

R7-140 does not claim a new live mark/rescan transaction.

The final in-game navigation behavior is composed from existing **LWB-R7-061** live evidence:

- command: `map_coordinate_jump`
- entry: `ManualMapScanCommandService.InvokeAsync`
- server: 2212
- live target: `(346,735)`
- target callback matched;
- return to player `(309,682)` proven;
- camera navigation only;
- no scan, attack, plunder, claim, collection or messaging action.

Evidence:

`evidence/lwbridge-implementation/2026-09-19-r7-coordinate-jump-live.json`

## Validation

Fresh R7-140 clean-worktree validation:

- Release build: **0 warnings / 0 errors**
- all six deterministic groups: **true**
- deterministic failures: **[]**
- game running after checks: false
- launcher running after checks: false

The new regression runs automatically through its module initializer as part of the default deterministic suite.

## Acceptance effect

**C04 -> PASS_CURRENT_PLUS_HISTORICAL**

This means:

- current bytes prove mark/rescan/restart/moved-coordinate routing and durable unmark;
- historical live evidence proves the public coordinate-jump command performs owned-session camera navigation.

It does **not** mean R7-140 itself performed a live Player City rescan while marking a real player.

## Remaining ordinary partial cases

After this closure, the current acceptance matrix has three ordinary `partial` rows:

- B11 — native add/update/remove/movement transition matrix;
- C07 — vanished/replaced-target failure branches;
- F04 — final human normal-user built-executable walkthrough.

Population- and authorization-dependent rows remain separate categories.

Machine-readable evidence:

- `evidence/lwbridge-implementation/2026-09-22-r7-player-mark-relocation.json`
- `evidence/lwbridge-implementation/2026-09-22-r7-acceptance-matrix-r7140.json`

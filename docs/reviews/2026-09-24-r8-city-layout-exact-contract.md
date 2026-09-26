# LWBridge 0.3.1 — exact original City Layout recovery

Date: 2026-09-24
Role: secondary researcher, read-only strict-parity lane
Repository inspected: C:\Users\chimw\OneDrive\Desktop\Github\LW-Control
Reference: C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe
Reference SHA-256: 2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff

## Scope and owner boundary

This report recovers the original LWBridge 0.3.1 City Layout control plane and frontend workflow only.

Per the task instruction, account/login/authentication/license research was intentionally excluded. No authentication path, credential material, private-key material, or forbidden secure-proxy RVA was inspected.

LW-Control was treated as read-only. This report is written only under LW Helper Finding.

Evidence labels:
- EXACT_BYTES — immutable original frontend bytes.
- EXACT_CONTRACT — exact host/public behavior recovered from permitted original instructions/data.
- RECOVERED — strongly supported composition across exact callers/consumers.
- PARTIAL — public boundary is known but nested/extra fields or rare branches are incomplete.
- PROTECTED_UNKNOWN — behavior is inside game-side/protected execution and is not guessed.

---

# 1. Executive result

The original City Layout frontend is already present in the current tree and is byte-identical to the recovered original chunk.

Original/current CityLayoutPanel SHA-256:

97dc2c5e0bf4fc5e3b3a058e704c02e21385b9c9511aeb9b3297ec3272c78022

All eight original API wrapper names are also still present in the current shipped API asset.

However, the production C# backend currently has zero native city_layout_* handlers. The only current non-original substitutes found are preview-only fixtures:

- city_layout_draft_get -> null
- city_layout_apply_status -> {state:'idle'}

Therefore the current parity gap is primarily the complete native/backend/persistence/game-bridge implementation, not frontend reconstruction.

The eight original commands are:

1. city_layout_snapshot_get
2. city_layout_validate
3. city_layout_apply_start
4. city_layout_apply_status
5. city_layout_apply_cancel
6. city_layout_draft_get
7. city_layout_draft_save
8. city_layout_draft_clear

No City Layout-specific frontend bridge event is used. The original feature is command + polling based.

---

# 2. Original API wrappers

EXACT_BYTES, recovered from api-ClPPi2JT.js.

| Command | Original wrapper request |
|---|---|
| city_layout_snapshot_get | no feature payload |
| city_layout_validate | {baseRevision, placements} |
| city_layout_apply_start | {baseRevision, placements} |
| city_layout_apply_status | no feature payload |
| city_layout_apply_cancel | {jobId} |
| city_layout_draft_get | {profileId} |
| city_layout_draft_save | {profileId, revision, value} |
| city_layout_draft_clear | {profileId, revision} |

The generic original host wrapper can inject the selected profileId when it is not already present. Draft wrappers supply profileId explicitly.

Approximate original API character offsets:

- city_layout_snapshot_get: 19817
- city_layout_validate: 19871
- city_layout_apply_start: 19951
- city_layout_apply_status: 20031
- city_layout_apply_cancel: 20083
- city_layout_draft_get: 20145
- city_layout_draft_save: 20212
- city_layout_draft_clear: 20297

---

# 3. Snapshot contract

## 3.1 Public success schema

EXACT_BYTES consumer schema.

The frontend consumes at least:

- layoutRevision
- isInCity
- cells[]
- buildings[]
- regions[]

### Cell fields consumed

Each cell is consumed with:

- pointId
- x
- y
- regionId
- unlocked
- road
- flagOnly

### Building fields consumed

Each building is consumed with:

- uuid
- pointId
- x
- y
- occupiedPoints[]
- movable
- isFlag
- tileX
- tileY
- level
- nameKey
- name
- iconPath

The original native City Layout metadata additionally embeds building field names:

- uuid
- itemId
- level
- pointId
- tileX
- tileY
- zoneType
- occupiedPoints
- movable
- isFlag

and cell field names:

- x
- y
- unlocked
- road
- flagOnly
- regionId

The native snapshot validator explicitly requires top-level cells and buildings arrays and checks/constructs layoutRevision. The complete required-vs-optional status of every display-only field is PARTIAL.

### Region fields consumed

The frontend uses the first region:

- id
- pointIds[]
- bounds.minX
- bounds.maxX
- bounds.minY
- bounds.maxY

The recovered component only renders/manipulates regions[0] as the main-city work area even though translation strings include extension-region wording.

## 3.2 Native host path

EXACT_CONTRACT.

Protected/game bridge method: getCityLayoutSnapshot

Native command handler source marker: src\commands\city_layout.rs

Request timeout: 10000 ms

Original native handler RVA: approximately 0xDC30C–0xDC58F

Snapshot validator: approximately 0x230D3D–0x231361

If the game bridge is unavailable:

GAME_DISCONNECTED / game disconnected

If the returned snapshot fails the host structural validator:

CITY_LAYOUT_INVALID

No separate human-readable CITY_LAYOUT_INVALID message was recovered; do not invent one.

## 3.3 Snapshot integrity behavior

The host does not blindly return any JSON payload. It validates at least:

- object shape;
- cells array;
- buildings array;
- cell records through the native cell validator;
- building records through the native building validator;
- layoutRevision field handling.

regions and display metadata are consumed by the frontend, but their exact native validation branches are not fully recovered.

---

# 4. Placement / move schema

## 4.1 Exact placement object

EXACT_BYTES.

A frontend placement is exactly:

{uuid, targetPointId}

The placement array contains only changed buildings.

Returning a building to its original pointId removes that building from placements.

The array is sorted lexicographically by uuid after each edit.

The frontend canonical placement fingerprint used for race checks is:

uuid:targetPointId|uuid:targetPointId|...

in current placement-array order.

## 4.2 Footprint relocation

For a changed building, the frontend remaps each original occupied point relative to the building's original x/y onto the target point's x/y.

The resulting occupied pointIds are sorted ascending.

A target whose translated footprint cannot be fully mapped produces CITY_LAYOUT_OUTSIDE.

## 4.3 Group move

For group dragging, selected movable buildings retain their relative x/y offsets from the anchor building.

A candidate anchor is accepted only when each selected building's translated footprint is complete and remains in the first region.

The drag preview tries the direct candidate first; when invalid it also tries horizontal-only and vertical-only aligned candidates relative to the previous anchor position.

---

# 5. Exact frontend local validation

EXACT_BYTES.

Local validation runs before every host city_layout_validate call and before apply.

Local issues have this observable shape:

{uuid, code, pointId?}

The local validator emits these exact codes:

- CITY_LAYOUT_IMMOVABLE
- CITY_LAYOUT_OUTSIDE
- CITY_LAYOUT_ROAD
- CITY_LAYOUT_FLAG_ONLY
- CITY_LAYOUT_LOCKED
- CITY_LAYOUT_OVERLAP

Behavior:

### CITY_LAYOUT_IMMOVABLE

If a placement exists for a building with movable=false:

{uuid, code:'CITY_LAYOUT_IMMOVABLE', pointId:targetPointId}

### CITY_LAYOUT_OUTSIDE

If translated footprint size differs from the original occupiedPoints length:

{uuid, code:'CITY_LAYOUT_OUTSIDE', pointId:targetPointId}

### CITY_LAYOUT_ROAD

For a changed building, each occupied target cell with unlocked=true and road=true can emit:

{uuid, code:'CITY_LAYOUT_ROAD', pointId:<cell>}

### CITY_LAYOUT_FLAG_ONLY

For a changed non-flag building on a flagOnly cell:

{uuid, code:'CITY_LAYOUT_FLAG_ONLY', pointId:<cell>}

Flags are allowed on ordinary non-road cells and flag-only cells.

### CITY_LAYOUT_LOCKED

For a changed building on unlocked=false land:

{uuid, code:'CITY_LAYOUT_LOCKED', pointId:<cell>}

### CITY_LAYOUT_OVERLAP

Occupancy is accumulated across changed and unchanged buildings. A point already owned by another building emits:

{uuid, code:'CITY_LAYOUT_OVERLAP', pointId:<cell>}

The validator can emit multiple issues; it does not deduplicate to one issue per building.

## 5.1 Server-prepared issue vocabulary

EXACT_BYTES translation vocabulary; server origin PARTIAL.

The original English bundle also contains messages for:

- CITY_LAYOUT_ZONE
- CITY_LAYOUT_BUILDING_MISSING
- CITY_LAYOUT_NO_BUFFER

These three are not emitted by the local validator. They are prepared for host/game validation/application responses.

Exact English messages:

- CITY_LAYOUT_ZONE — This building is not allowed in this area.
- CITY_LAYOUT_BUILDING_MISSING — The building is no longer present in the game.
- CITY_LAYOUT_NO_BUFFER — No temporary cell is available for the swap.

The exact protected planner branches that emit these codes remain PROTECTED_UNKNOWN.

---

# 6. city_layout_validate

## Request

EXACT_BYTES.

{baseRevision, placements}

placements is the exact changed-building list described above.

The host explicitly extracts/normalizes the placements field before the game request. Missing placement data follows an empty-array preparation path in the native JSON layer. baseRevision is forwarded, but its detailed game-side comparison logic is not visible at the host boundary.

## Native game-bridge method

validateCityLayout

Timeout: 10000 ms

Native handler approximately: 0x1869E7–0x187506

If the game bridge is unavailable:

GAME_DISCONNECTED / game disconnected

## Success result consumed by frontend

EXACT_BYTES minimum result shape:

{
  valid: boolean,
  issues: Issue[],
  totalMoves: number,
  temporaryMoves: number
}

When local validation fails, the frontend returns the same shape without invoking the host:

{
  valid: false,
  issues: <local issues>,
  totalMoves: 0,
  temporaryMoves: 0
}

## Race suppression

The frontend increments a validation generation counter for every validation request and records the canonical placement fingerprint.

A returned host validation result updates visible validation state only if:

- it is still the newest validation generation; and
- the placement fingerprint still matches current placements.

Stale validation responses are ignored.

## Protected boundary

How validateCityLayout computes execution order, temporary moves, swap/buffer plans, zone restrictions, and missing-building checks is PROTECTED_UNKNOWN and must not be reconstructed from current-client gameplay behavior.

---

# 7. Draft persistence contract

## 7.1 Storage location

EXACT_CONTRACT.

Drafts use the original per-profile SQLite profile database, table profile_state.

Schema:

CREATE TABLE profile_state (
  key TEXT PRIMARY KEY,
  value_json TEXT NOT NULL,
  revision INTEGER NOT NULL
)

Exact City Layout key:

city_layout_draft_v1

This is not browser localStorage.

A surviving legacy profile.db on this machine confirms the table schema, but contains no current city_layout_draft_v1 row.

## 7.2 Draft value v1

EXACT_BYTES.

{
  version: 1,
  baseRevision: <snapshot.layoutRevision>,
  placements: Placement[],
  updatedAt: Date.now()
}

Only value.version===1 is accepted by the frontend as a City Layout draft. Other versions are ignored as draft content.

## 7.3 Draft get

Request:

{profileId}

Native command uses exact key city_layout_draft_v1.

The native response record is constructed with keys:

- profileId
- key
- revision
- value

The frontend consumes revision and value.

A missing/empty draft is represented through the generic profile-state record path; the exact native no-row representation is PARTIAL, but the frontend expects the command result object to expose revision and value.

Malformed stored JSON can map to:

PROFILE_DATA_INVALID

## 7.4 Draft save optimistic revision

Request:

{profileId, revision, value}

Exact generic state SQL:

INSERT OR IGNORE INTO profile_state(key, value_json, revision)
VALUES (?, ?, 1)

and for an existing row:

UPDATE profile_state
SET value_json = ?, revision = revision + 1
WHERE key = ? AND revision = ?

Therefore:

- first creation revision = 1;
- every matching update increments revision by exactly 1;
- stale revision cannot overwrite current data.

The generic save path re-reads/returns state after success. The frontend consumes returned .revision and updates its local draft revision.

Exact conflict code:

PROFILE_REVISION_CONFLICT

Other generic persistence codes reachable from the profile-state layer include:

- INVALID_PROFILE_STATE
- PROFILE_STATE_UNAVAILABLE
- PROFILE_DATA_INVALID

Exact branch-to-message text beyond the codes above is PARTIAL.

## 7.5 Draft clear optimistic revision

Request:

{profileId, revision}

Exact SQL:

DELETE FROM profile_state
WHERE key = ? AND revision = ?

A stale/mismatched revision maps to:

PROFILE_REVISION_CONFLICT

On explicit Discard draft, frontend behavior after successful clear is:

- local revision -> 0
- base revision -> current snapshot.layoutRevision
- stale flag -> false
- placements/history -> empty
- saved-placement fingerprint -> empty

The direct clear command result is not used by the frontend; its full success body is PARTIAL.

## 7.6 Autosave serialization

The frontend keeps a promise chain for draft writes.

Every save waits for the prior draft operation, even if the prior operation rejected.

This prevents overlapping draft revisions from racing.

Autosave occurs only when:

- component initialization is complete;
- stale flag is false;
- current placement fingerprint differs from the last successfully saved fingerprint.

Autosave delay: 500 ms

A new edit clears the pending timer and starts a new 500 ms timer.

Manual Save draft bypasses the delay but uses the same serialized save chain.

---

# 8. Initial load / Refresh workflow

EXACT_BYTES.

When online with a profile, Refresh does:

1. set loading=true;
2. clear visible generic error;
3. wait for the current draft-save promise chain;
4. run in parallel:
   - city_layout_snapshot_get();
   - city_layout_draft_get(profileId);
   - city_layout_apply_status();
5. accept draft value only if value.version===1;
6. if apply status is succeeded AND a v1 draft exists AND draft.baseRevision != snapshot.layoutRevision:
   - clear the draft using the fetched draft revision;
   - discard draft value;
   - local draft revision becomes 0;
7. load draft placements or [];
8. set snapshot;
9. reset undo/redo history;
10. set draft revision;
11. set base revision to snapshot.layoutRevision;
12. stale=false;
13. set apply status to null when returned state is idle, otherwise retain returned status;
14. select the first valid building in the first region when prior selection is no longer valid;
15. set last-saved placement fingerprint;
16. clear server-validation display;
17. mark component initialized;
18. loading=false.

## Important stale-draft quirk

The recovered component declares and renders a stale-draft warning state, but there is no setter call that ever changes stale from false to true.

The warning UI and stale-based button disables therefore have no reachable set-true path in this recovered chunk.

Do not silently invent a stale=true transition if strict byte-level behavior is required.

After a succeeded apply, stale persisted drafts are instead automatically cleared during Refresh using the baseRevision mismatch rule above.

---

# 9. Apply workflow

## 9.1 Frontend admission

Apply immediately returns without action when:

- no snapshot;
- stale flag true;
- placements length == 0.

The Apply button is disabled under the same stale/no-changes/busy rules.

Notably, snapshot.isInCity=false does not directly disable the Apply button. The UI only shows a warning; host apply-start performs the hard in-city gate.

## 9.2 Apply sequence

EXACT_BYTES.

1. Capture canonical placement fingerprint.
2. Set apply-start busy flag.
3. Run local + host validation.
4. Abort if placements changed while validation was in flight.
5. Abort unless result.valid is true.
6. Confirm with browser confirm dialog using totalMoves.
7. Save the current draft and wait for it to finish.
8. Abort if placements changed during the save.
9. Call city_layout_apply_start(baseRevision, placements).
10. If response is missing accepted or status:
    - set validation display to invalid;
    - issues = response.issues || [];
    - totalMoves=0;
    - temporaryMoves=0;
    - do not enter apply-status tracking.
11. Otherwise install response.status.
12. Clear apply-start busy flag in finally.

Exact confirmation text:

Run {count} building moves? A failure stops immediately and is not rolled back automatically.

This text is exact UI behavior. The underlying executor rollback semantics remain protected; do not infer more than the warning states.

---

# 10. city_layout_apply_start

## Request

{baseRevision, placements}

## Host precondition

EXACT_CONTRACT.

The host obtains a fresh City Layout snapshot before starting.

It reads snapshot.isInCity.

If false:

CITY_LAYOUT_NOT_IN_CITY

The distinct human-readable message for this code was not independently recovered.

If the game bridge is unavailable:

GAME_DISCONNECTED / game disconnected

## Protected method

startCityLayoutApply

Timeout: 10000 ms

Native handler approximately: 0x183AFC–0x1847DD

## Success result consumed

EXACT_BYTES minimum shape:

{
  accepted: boolean,
  status: ApplyStatus | null,
  issues?: Issue[]
}

Frontend requires both accepted truthy and status truthy to enter active apply tracking.

If rejected/no status, issues are rendered through the same validation issue UI.

## Protected boundary

The actual move planner/executor, including move ordering, temporary moves, swap buffer selection, game request sequencing, failure stop behavior, and any rollback or lack thereof is PROTECTED_UNKNOWN except for the observable UI warning and returned status/results.

---

# 11. Apply status lifecycle

## city_layout_apply_status

No feature payload.

Protected method:

getCityLayoutApplyStatus

Timeout: 5000 ms

Native handler approximately: 0x122DA2–0x123765

Game unavailable:

GAME_DISCONNECTED / game disconnected

## ApplyStatus minimum frontend schema

EXACT_BYTES.

The frontend consumes:

- state
- jobId
- completedMoves
- totalMoves

Known exact state vocabulary from the frontend:

- idle
- running
- cancelling
- succeeded
- failed
- cancelled

Additional job fields/result/error fields may exist in the game response, but are PARTIAL because this component does not consume them and the host forwards protected result JSON.

## Busy states

The whole editor enters busy mode when state is:

- running
- cancelling

Busy mode disables editing/history/refresh/save/apply actions.

## Progress

When running/cancelling, UI shows:

Applying {completedMoves}/{totalMoves}

and an HTML progress element with:

max = totalMoves || 1
value = completedMoves

A cancel button remains visible.

---

# 12. Apply polling

EXACT_BYTES.

Polling is not setInterval. It is a chained one-shot setTimeout.

While current status.state is running or cancelling:

- wait 500 ms;
- call city_layout_apply_status();
- replace current status;
- if returned state is succeeded, failed, or cancelled:
  - call the full Refresh workflow.

Polling cadence: 500 ms

Only one timer is active per rendered status state, so requests do not intentionally overlap.

No City Layout-specific bridge event participates in job progress.

---

# 13. city_layout_apply_cancel

## Request

{jobId}

The frontend only invokes cancel when current status has a truthy jobId.

Protected method:

cancelCityLayoutApply

Timeout: 5000 ms

Native handler approximately: 0x113007–0x113C01

Game unavailable:

GAME_DISCONNECTED / game disconnected

The command result is installed directly as the new ApplyStatus.

The exact protected cancel transition semantics are PARTIAL. The frontend is prepared for cancelling and cancelled states.

Important frontend nuance:

- polling automatically Refreshes when a polled status becomes terminal;
- the direct cancel function itself only installs its returned status;
- if cancel were to return terminal cancelled immediately, that direct path does not itself call Refresh.

Do not assume a protected cancellation transition beyond the observed result handling.

---

# 14. Exact City Layout UI text

Recovered English strings include:

- City Layout
- {cells} unlocked cells · {buildings} buildings · {movable} movable
- The game is disconnected, so the city layout cannot be loaded.
- No city layout data is available.
- Undo
- Redo
- Restore initial layout
- Save draft
- Apply to game
- The game layout changed. This draft cannot be applied directly.
- Discard draft
- Return to the inner city before applying the layout.
- Main city
- Extension {index}
- Level {level}
- Available
- Road: blocked
- Flag only
- Locked
- Building properties
- Footprint
- Movable
- Placement rule
- Roads and flag-only cells are blocked
- Allowed on ordinary non-road and flag-only cells
- Local layout checks passed
- Execution plan: {moves} moves, including {temporary} temporary moves
- Pending changes ({count})
- No changes
- Run {count} building moves? A failure stops immediately and is not rolled back automatically.
- Applying {current}/{total}
- Stop remaining moves
- Draft revision {revision}
- {count} conflicts

Control-help text:

- Drag a blank area: box select
- Shift + drag: add box selection (can start on a building)
- Ctrl + click: add or remove
- Drag a selected building: move the group
- Ctrl+Z undo · Ctrl+Y redo · Esc clear selection

Exact issue messages:

- CITY_LAYOUT_ROAD — Buildings cannot be placed on roads.
- CITY_LAYOUT_FLAG_ONLY — Only the military flag can use this cell.
- CITY_LAYOUT_LOCKED — The target includes locked land.
- CITY_LAYOUT_OUTSIDE — The target is outside available land.
- CITY_LAYOUT_OVERLAP — The target overlaps another building.
- CITY_LAYOUT_IMMOVABLE — This building cannot be moved.
- CITY_LAYOUT_ZONE — This building is not allowed in this area.
- CITY_LAYOUT_BUILDING_MISSING — The building is no longer present in the game.
- CITY_LAYOUT_NO_BUFFER — No temporary cell is available for the swap.

---

# 15. Exact UI states and button rules

## Offline

If online=false, the entire panel is replaced by:

The game is disconnected, so the city layout cannot be loaded.

No snapshot command is useful until online.

## Online but no snapshot

Shows:

- common processing text while loading;
- otherwise last generic action-failed text;
- otherwise No city layout data is available.

## Header actions

Refresh:
- disabled while loading;
- disabled while apply-start busy;
- disabled while job state running/cancelling.

Undo:
- disabled when undo history empty;
- disabled while busy.

Redo:
- disabled when redo history empty;
- disabled while busy.

Restore initial layout:
- disabled when placements empty;
- disabled while busy.

Save draft:
- disabled when stale;
- disabled while busy;
- notably not disabled merely because placements are empty.

Apply to game:
- disabled when stale;
- disabled when placements empty;
- disabled while busy.

## isInCity warning

When snapshot.isInCity is false, show:

Return to the inner city before applying the layout.

This warning does not itself disable Apply.

## Stale warning

UI exists for:

The game layout changed. This draft cannot be applied directly. + Discard draft

But recovered component never sets stale=true.

## Footer

Always shows:

- Draft revision {revision}
- {count} conflicts

Conflict count is local-validation issue count.

---

# 16. Undo/redo and keyboard behavior

History state:

{past, present, future}

Every accepted placement edit:

- appends old present to past;
- makes new placements present;
- clears future;
- clears latest host-validation display.

Buttons:

- Undo uses last past entry.
- Redo uses first future entry.
- Restore initial sets placements to [] through normal edit history.

Keyboard:

- Escape clears drag state, lasso state, multi-selection, and primary selected building.
- Ctrl+Z performs undo.
- Ctrl+Shift+Z performs redo.
- Ctrl+Y performs redo.

While busy, keyboard history shortcuts are effectively blocked by the component's busy guard.

---

# 17. Selection / lasso / drag behavior

## Normal click

Select exactly one building.

## Ctrl+click

Toggle building in multi-selection.

If removing the currently primary-selected building, primary selection becomes another selected building or empty.

## Shift+drag on a building

Starts additive lasso selection.

## Drag blank grid

Starts box selection.

Ctrl held at blank-grid pointer-down makes the lasso additive.

## Box selection result

Only movable buildings are selected.

A building is included if any of its current/draft occupied cells intersects the lasso rectangle.

## Group drag

Dragging a selected movable building moves the selected movable group.

If the pointer-down building is not already selected, selection becomes that building.

Nonmovable buildings are filtered out of the dragged group.

---

# 18. Zoom/canvas behavior

Default cell size: 24 px

Minimum: 4 px

Maximum: 72 px

Zoom step: 4 px

Display percentage:

round(cellSize / 24 * 100)

Zoom in/out and Fit Canvas are disabled while a building drag or lasso is active.

Fit Canvas computes a cell size from the first region dimensions and viewport:

floor(min((width-28)/regionWidth, (height-28)/regionHeight))

then clamps to 4..72 and scrolls viewport to 0,0.

---

# 19. Localization of building names

The component builds a sorted unique nameKey list from snapshot buildings.

It calls the existing localization API for those keys.

Display name priority:

1. localized value when present and not equal to nameKey;
2. building.name;
3. building.nameKey.

This is presentation-only and does not alter placement identity.

---

# 20. Native command/storage evidence anchors

Original EXE native string cluster:

- src\commands\city_layout.rs around raw 0x821A28+
- getCityLayoutSnapshot
- placements
- validateCityLayout
- city_layout_draft_v1
- revision
- value
- PROFILE_DATA_INVALID
- isInCity
- startCityLayoutApply
- CITY_LAYOUT_NOT_IN_CITY
- jobId
- cancelCityLayoutApply
- getCityLayoutApplyStatus

Snapshot metadata cluster around raw 0x837D46+:

- CITY_LAYOUT_INVALID
- INVALID_REQUEST
- cells
- buildings
- uuid
- itemId
- level
- pointId
- tileX
- tileY
- zoneType
- occupiedPoints
- movable
- isFlag
- x
- y
- unlocked
- road
- flagOnly
- regionId

Native command/function anchors:

- snapshot command: ~0xDC30C–0xDC58F
- apply cancel: ~0x113007–0x113C01
- apply status: ~0x122DA2–0x123765
- draft save: ~0x12BFEA–0x12CAFF
- draft clear: ~0x137FA7–0x138AB2
- draft get: ~0x13DB48–0x13E893
- apply start: ~0x183AFC–0x1847DD
- validate: ~0x1869E7–0x187506
- snapshot structural validator: ~0x230D3D–0x231361

Generic profile-state storage:

- read profile state: ~0x3D43C8–0x3D4A7F
- save profile state: ~0x3D7264–0x3D78B3
- clear profile state: ~0x3D78B3–0x3D7E64

Exact profile-state SQL strings:

SELECT revision, value_json FROM profile_state WHERE key = ?

INSERT OR IGNORE INTO profile_state(key, value_json, revision)
VALUES (?, ?, 1)

UPDATE profile_state
SET value_json = ?, revision = revision + 1
WHERE key = ? AND revision = ?

DELETE FROM profile_state
WHERE key = ? AND revision = ?

---

# 21. Bridge events

EXACT_BYTES.

The CityLayoutPanel registers no City Layout-specific bridge event listener.

There is no recovered equivalent of bridge://city-layout-... for snapshot/apply progress.

The original feature relies on:

- command results;
- explicit Refresh;
- 500 ms apply-status polling;
- 500 ms draft autosave timer.

Do not add a City Layout progress event as product behavior unless separate original evidence is later recovered.

---

# 22. Error-code inventory

## Exact host/native codes recovered

- GAME_DISCONNECTED
  - message: game disconnected
- CITY_LAYOUT_INVALID
  - snapshot structural-invalid code
  - distinct message not recovered
- CITY_LAYOUT_NOT_IN_CITY
  - apply-start host precondition
  - distinct message not recovered
- PROFILE_DATA_INVALID
- INVALID_PROFILE_STATE
- PROFILE_STATE_UNAVAILABLE
- PROFILE_REVISION_CONFLICT
- INVALID_REQUEST

## Exact frontend validation issue codes

- CITY_LAYOUT_IMMOVABLE
- CITY_LAYOUT_OUTSIDE
- CITY_LAYOUT_ROAD
- CITY_LAYOUT_FLAG_ONLY
- CITY_LAYOUT_LOCKED
- CITY_LAYOUT_OVERLAP

## Exact translation-prepared server issue codes

- CITY_LAYOUT_ZONE
- CITY_LAYOUT_BUILDING_MISSING
- CITY_LAYOUT_NO_BUFFER

For codes whose native human-readable message was not separately recovered, implementation should preserve the code and avoid inventing message wording as exact original.

---

# 23. Current rebuild status

## Frontend

MATCH / byte-identical.

Current CityLayoutPanel-B4B03XEi.js SHA-256 exactly matches recovered original:

97dc2c5e0bf4fc5e3b3a058e704c02e21385b9c9511aeb9b3297ec3272c78022

All eight city_layout_* API strings remain present in the current API asset.

## Production C# backend

MISSING for all eight commands.

A direct search of src/LWBridge.Desktop/*.cs produced no city_layout_* production command handler.

Therefore live production invocation falls through the generic unimplemented-command path.

## Preview fixture

preview-host.js contains only:

- city_layout_draft_get: () => null
- city_layout_apply_status: () => ({state:'idle'})

These are preview/test fixtures and are not original backend parity.

The draft_get fixture is not a faithful original success shape because the original component expects an object exposing revision and value.

---

# 24. Implementation-ready command/event table

| Surface | Request | Minimum success shape | Exact host bridge/storage | Timeout | Key exact errors | Status |
|---|---|---|---|---:|---|---|
| city_layout_snapshot_get | none | {layoutRevision,isInCity,cells,buildings,regions} | getCityLayoutSnapshot + host snapshot validator | 10000 ms | GAME_DISCONNECTED, CITY_LAYOUT_INVALID | IMPLEMENTABLE host boundary; gameplay snapshot producer protected |
| city_layout_validate | {baseRevision,placements} | {valid,issues,totalMoves,temporaryMoves} | validateCityLayout | 10000 ms | GAME_DISCONNECTED; protected validation issues | IMPLEMENTABLE host boundary; planner protected |
| city_layout_apply_start | {baseRevision,placements} | {accepted,status,issues?} | fresh snapshot -> isInCity gate -> startCityLayoutApply | 10000 ms | GAME_DISCONNECTED, CITY_LAYOUT_NOT_IN_CITY | IMPLEMENTABLE host boundary; executor protected |
| city_layout_apply_status | none | ApplyStatus with state/jobId/completedMoves/totalMoves minimum | getCityLayoutApplyStatus | 5000 ms | GAME_DISCONNECTED | IMPLEMENTABLE forwarding/polling boundary |
| city_layout_apply_cancel | {jobId} | ApplyStatus | cancelCityLayoutApply | 5000 ms | GAME_DISCONNECTED; protected job errors partial | IMPLEMENTABLE forwarding boundary |
| city_layout_draft_get | {profileId} | state record containing profileId,key,revision,value | profile_state key city_layout_draft_v1 | local DB | PROFILE_DATA_INVALID etc. | IMPLEMENTABLE exact persistence |
| city_layout_draft_save | {profileId,revision,value} | updated state/revision; frontend requires .revision | revisioned profile_state insert/update | local DB | PROFILE_REVISION_CONFLICT, INVALID_PROFILE_STATE, PROFILE_STATE_UNAVAILABLE, PROFILE_DATA_INVALID | IMPLEMENTABLE exact persistence |
| city_layout_draft_clear | {profileId,revision} | direct result unused | guarded profile_state delete | local DB | PROFILE_REVISION_CONFLICT + generic state errors | IMPLEMENTABLE exact persistence |
| City Layout event | none | none | no feature event | n/a | n/a | DO NOT ADD |

---

# 25. Exact UI restoration checklist

The current chunk already satisfies this checklist byte-for-byte, but any future rewrite should preserve all items:

- [ ] Offline whole-panel replacement.
- [ ] Online loading / no-data / generic-error empty state.
- [ ] Header title and unlocked/building/movable stats.
- [ ] Refresh.
- [ ] Undo.
- [ ] Redo.
- [ ] Restore initial layout.
- [ ] Save draft.
- [ ] Apply to game.
- [ ] Stale warning + Discard draft code path, even though stale=true is unreachable in recovered component.
- [ ] Not-in-city warning without directly disabling Apply.
- [ ] Main-city first-region rendering.
- [ ] Zoom out / percentage / zoom in / Fit Canvas.
- [ ] Default 24 px, range 4..72, step 4.
- [ ] Localized hover/name preview.
- [ ] Controls help text.
- [ ] Blank-area lasso.
- [ ] Shift additive lasso.
- [ ] Ctrl toggle selection.
- [ ] Group drag with relative offsets.
- [ ] Escape clears selection/drag/lasso.
- [ ] Ctrl+Z, Ctrl+Shift+Z, Ctrl+Y history.
- [ ] Available / road / flag-only / locked legend.
- [ ] Building icons, names and levels on grid.
- [ ] Selected and changed visual classes.
- [ ] Inspector: building icon/name/level.
- [ ] Inspector: footprint.
- [ ] Inspector: movable yes/no.
- [ ] Inspector: normal/flag placement rule.
- [ ] Local validation indicator.
- [ ] Server execution-plan indicator with total/temporary move counts.
- [ ] Pending changes list.
- [ ] Apply confirmation with not-rolled-back-automatically warning.
- [ ] Applying current/total progress.
- [ ] Stop remaining moves button.
- [ ] Draft revision footer.
- [ ] Conflict-count footer.
- [ ] 500 ms draft debounce.
- [ ] Serialized draft-save promise chain.
- [ ] 500 ms apply-status polling with no feature bridge event.
- [ ] Terminal status Refresh.
- [ ] Succeeded-apply stale-draft auto-clear by baseRevision mismatch.

---

# 26. Current-rebuild deviation list

1. All eight production backend commands are missing.
2. The current UI is preserved, so it attempts original calls that production C# cannot service.
3. Preview city_layout_draft_get returns null rather than the original revision/value state object.
4. Preview apply-status only returns idle and therefore does not model job lifecycle.
5. No original profile_state City Layout persistence is implemented in production C#.
6. No original 5000/10000 ms game-bridge request paths exist in production C# for this cluster.
7. No original snapshot structural validation is implemented.
8. No original CITY_LAYOUT_NOT_IN_CITY apply-start precondition is implemented.
9. No original optimistic draft revision conflict handling is implemented.
10. No protected validate/apply/status/cancel bridge integration exists.

The UI itself should not be redesigned merely because the backend is missing; it is already byte-identical to the reference component.

---

# 27. Unresolved / protected stop list

Do not invent these:

1. Internal getCityLayoutSnapshot game-side data acquisition.
2. Exact meaning/production of layoutRevision inside the game script.
3. Complete native required/optional type rules for every snapshot display field beyond the recovered validator metadata.
4. Protected validateCityLayout planning algorithm.
5. Exact zone-restriction logic behind CITY_LAYOUT_ZONE.
6. Building-disappearance detection behind CITY_LAYOUT_BUILDING_MISSING.
7. Temporary swap/buffer search behind CITY_LAYOUT_NO_BUFFER.
8. Exact move ordering.
9. Exact temporary-move construction.
10. Game RPC/action sequence used by startCityLayoutApply.
11. Detailed failure/partial-apply behavior beyond the exact frontend warning that failure stops immediately and is not rolled back automatically.
12. Full ApplyStatus extra fields/result/error schema beyond fields consumed by the frontend.
13. Exact protected cancellation timing/transition rules.
14. Any gameplay behavior inferred only from a current Last War client.

The correct implementation boundary is to reproduce the original host/public envelopes, timing, validation gates, persistence and UI, while leaving protected planner/executor behavior gated until independently recovered.

---

# 28. Prioritized implementation sequence for main researcher

## Phase 1 — local persistence first

1. Implement a per-profile profile_state equivalent with key/value_json/revision and exact city_layout_draft_v1 key.
2. Implement city_layout_draft_get.
3. Implement city_layout_draft_save:
   - first revision 1;
   - guarded update;
   - revision +1;
   - PROFILE_REVISION_CONFLICT on stale revision.
4. Implement city_layout_draft_clear:
   - guarded delete;
   - same revision conflict behavior.
5. Add tests for empty state, first save, sequential save, stale save, stale clear, valid clear, and malformed stored JSON.

This makes autosave/manual-draft behavior functional independently of gameplay execution.

## Phase 2 — snapshot bridge

6. Implement city_layout_snapshot_get:
   - selected-profile routing;
   - game connection guard;
   - getCityLayoutSnapshot;
   - 10 s timeout;
   - snapshot structural validator;
   - GAME_DISCONNECTED;
   - CITY_LAYOUT_INVALID.
7. Add contract tests using recovered snapshot schema and malformed variants.

## Phase 3 — validation

8. Implement city_layout_validate forwarding:
   - {baseRevision,placements};
   - placements default/normalization matching original host;
   - 10 s timeout;
   - no invented planning logic.
9. Preserve exact result JSON from protected provider when available.
10. Test frontend race suppression separately; do not implement it again in host.

## Phase 4 — apply lifecycle

11. Implement city_layout_apply_start:
   - fresh snapshot;
   - isInCity gate;
   - CITY_LAYOUT_NOT_IN_CITY;
   - startCityLayoutApply;
   - 10 s timeout;
   - return {accepted,status,issues?}.
12. Implement city_layout_apply_status:
   - getCityLayoutApplyStatus;
   - 5 s timeout.
13. Implement city_layout_apply_cancel:
   - jobId request;
   - cancelCityLayoutApply;
   - 5 s timeout.
14. Preserve original state vocabulary and do not add a new event system.

## Phase 5 — integration verification

15. Verify the existing byte-identical CityLayoutPanel works without changes.
16. Verify 500 ms autosave.
17. Verify 500 ms status polling.
18. Verify Refresh waits for pending draft saves.
19. Verify Apply performs validate -> confirm -> draft save -> start.
20. Verify terminal poll refreshes snapshot/draft/status.
21. Verify succeeded apply clears mismatched old draft.
22. Verify not-in-city warning remains visible and host blocks start.
23. Verify local issue codes/messages.
24. Verify no City Layout feature event is required.

---

# 29. Read-only / Git note

No LW-Control file was modified by this helper.

The repository was clean on branch research/offline-controller when this City Layout lane began.

All findings were written only under:

C:\Users\chimw\OneDrive\Desktop\LW Helper Finding

## Final concurrent-worktree verification

A final read-only git status showed active main-researcher work on branch research/offline-controller, including the R8-011 Map Data Options strict-parity lane and related source/test/docs changes.

No listed repository change was created or modified by this helper.

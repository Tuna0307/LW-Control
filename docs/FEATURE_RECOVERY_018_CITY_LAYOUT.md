# FEATURE-RECOVERY-018 City Layout checkpoint

Date: 2026-10-17. Status: PARTIAL; live City Layout actions NOT_POSSIBLE on current verified source.

The original LWBridge 0.3.17 City Layout interface has genuine local grid geometry and editing controls. This checkpoint connects those controls to the existing profile-owned native backend transport. It requests the genuine city_layout_snapshot_get command; rejects missing city cells/buildings/regions/revision; loads revisioned per-profile drafts, detects stale drafts and discards them through native commands; serializes revisioned autosaves and manual saves; validates actual changed placements; requires confirmation before native apply; polls real apply status every 500 ms during a job; and sends cancellation to the native backend. Errors are displayed and never represented as successful moves. Existing UI preview fixtures remain isolated.

## Exact production limitation

Only three City Layout draft commands (city_layout_draft_get/save/clear) have production handlers in src/LWBridge.Desktop/CityLayoutDraftCommandService.cs:28-63, registered from LWBridgeWindow.cs:477. The five game-facing commands city_layout_snapshot_get, city_layout_validate, city_layout_apply_start, city_layout_apply_status and city_layout_apply_cancel have no live production handlers. The currently proven CurrentClientMapBlockSource handles world Resource and Player City map acquisition, rather than interior building placements. Current overview Lua pumps Map and Home operations, with no verified interior-city read/move implementation.

Read-only inspection of the installed current Assembly-CSharp.rdl confirms native CityBuilding fields (tilePos, tileX, tileY, Uuid) and CityScene methods (including GetPointInfo, GetAllMainBaseList). These names are insufficient to establish a complete authoritative building and road/flag/locked-cell snapshot or a validated and confirmed game move. Historical 0.3.1 native handlers have a dependency on original authorization-state-derived input; do not synthesize that input or bypass authorization admission. Evidence: research/offline-controller branch docs/reviews/2026-09-26-r8-050-city-layout-snapshot-fence.md, r8-052-city-layout-apply-status-fence.md, r8-070-city-layout-live-fence.md.

## Feature outcome ledger

| Feature | Status | Exact reason or proof |
|---|---|---|
| Local Zoom, selection/drag, group drag, undo/redo, inspection, local overlap/road/locked validation | WORKING in isolated UI fixture | Frontend deterministic UI checks passed; no live current-city layout is claimed. |
| Per-profile native revisioned draft persistence | WORKING in existing backend | Revisioned profile-state save, stale-write/clear conflicts; existing CityLayoutDraftChecks. |
| Live city snapshot and real building/footprint/region display | NOT_POSSIBLE | Native city_layout_snapshot_get is not implemented; no verified current-client city interior snapshot provider. |
| Server-side validation and move plan | NOT_POSSIBLE | Native city_layout_validate is not implemented; game zone and temporary-buffer planner unverified. |
| Real placement Apply/progress/Stop | NOT_POSSIBLE | Native apply start/status/cancel are not implemented; authoritative current-client executor absent. |
| Packaged game verification | NOT_POSSIBLE in this checkpoint | No successful authenticated current-game City session with native snapshot/move receipts. |

Validation: npm.cmd --prefix src/LWBridge.UI-0.3.17 run build PASS and run check PASS, including existing UI/Map owner regressions. These are code-level tests, not gameplay proof.

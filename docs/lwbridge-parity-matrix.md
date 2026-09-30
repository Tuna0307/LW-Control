# LWBridge 0.3.17 parity matrix

This is the current completion authority for the new 0.3.17 program.

The matrix intentionally begins almost empty. Do not pre-fill it with 0.3.1 assumptions.

## Scope exception

The original LWBridge login/account/licensing/entitlement system is **OUT_OF_SCOPE** for reconstruction.

If it is encountered, record only the access boundary. Do not rebuild it or bypass it. A later in-scope feature may justify tracing only the auth-produced state it directly consumes.

## UI matrix

| ID | Surface/state | Reference evidence | Reproduction status | Visual validation | Open gaps |
|---|---|---|---|---|---|
| UI-000 | Login/account/licensing boundary | RUNTIME OBSERVED 2026-09-29; redacted boundary screenshot | OUT_OF_SCOPE | OUT_OF_SCOPE | Dependency-only investigation if later required |
| UI-001 | App launch / initial in-scope shell | EXACT_BYTES static shell/CSS contract; runtime blocked by auth | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Reference post-auth geometry/theme/profile-sidebar state unavailable |
| UI-002 | Top-level navigation | EXACT_BYTES definition + English locale + inline SVGs + CSS | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Reference selected/hover pixels unavailable; exact build statically disables Advanced |
| UI-003 | Home | EXACT_BYTES component + English locale + CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Exact unresolved state cloned; reference post-auth geometry/theme unavailable |
| UI-004 | Automation | EXACT_BYTES tab/card/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Static disconnected/category/card surface cloned; persisted runtime state unavailable |
| UI-005 | Map Data | EXACT_BYTES scan/tab/filter/table/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Static unloaded Manual Scan / City surface cloned; runtime data unavailable |
| UI-006 | Squads / AFK | EXACT_BYTES AFK/equipment/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Static AFK/equipment empty surfaces cloned; runtime data unavailable |
| UI-007 | City Layout | EXACT_BYTES grid/inspector/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Exact disconnected render cloned; city data/geometry unavailable |
| UI-008 | Hotkeys | EXACT_BYTES shortcut/card/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Seven-card static surface cloned; persisted config/reference pixels unavailable |
| UI-009 | Mini Games | EXACT_BYTES helper/card/status/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Four-card/helper static surface cloned; task/runtime state unavailable |
| UI-010 | Settings | EXACT_BYTES settings/feedback/update/English locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Initial performance/feedback/update static surface cloned; runtime state unavailable |
| UI-011+ | Additional in-scope 0.3.17 surfaces | ADD ONLY AFTER EVIDENCE | NOT STARTED | NOT STARTED | Unknown until inventory |

## Function matrix

Add one row per in-scope 0.3.17 function only after it is observed/recovered.

| ID | Function | UI trigger | Frontend/API | Host/runtime | Storage/state | Current-client map | Live proof |
|---|---|---|---|---|---|---|---|
| MAP-001A | Map frontend + host command surface | EXACT_BYTES UI inventory | EXACT_CONTRACT | EXACT_CONTRACT | N/A | N/A | NOT STARTED |
| MAP-001B | Scan lifecycle + acquisition | Map Start/Stop/Clear/status | EXACT_CONTRACT | EXACT_CONTRACT; Map317 production plane | EXACT_CONTRACT; per-profile Map DB | IMPLEMENTED_NOT_VALIDATED; v21 source-backed block provider | NOT STARTED |
| MAP-001C | Query/options/summary/export/marks/history | Map data/search/export UI | EXACT_CONTRACT | EXACT_CONTRACT; Map317 production plane | EXACT_CONTRACT; schema v4 | IMPLEMENTED_NOT_VALIDATED | NOT STARTED |
| MAP-001D | Navigation + Treasure inspection | row actions / Treasure refresh | EXACT_CONTRACT | EXACT_CONTRACT | EXACT_CONTRACT where local state is owned | IMPLEMENTED_NOT_VALIDATED; Treasure claim/status remain fail-closed | NOT STARTED |
| MAP-001E | Dispatch share + scheduled Dispatch/Truck plunder | Map row actions / Scheduled Plunder | EXACT_CONTRACT | EXACT_CONTRACT; arm->pending->result workers | EXACT_CONTRACT; durable jobs/history/server-day | IMPLEMENTED_NOT_VALIDATED; v21 share/server-day/Dispatch/Truck paths wired | NOT STARTED |
| MAP-001F | Ghost plunder preparation | Scheduled Plunder Ghost rows | EXACT_CONTRACT scheduling boundary | EXACT_CONTRACT host boundary | EXACT_CONTRACT durable row semantics | BLOCKED — current-v21 `prepareGhostPlunderTasks` equivalent not source-proven; production fails closed | NOT STARTED |

## Completion rule

An in-scope UI row closes only with reference evidence plus reproduction/visual validation.

An in-scope function row closes only with an evidence-backed 0.3.17 contract and appropriate implementation/live validation.

Historical 0.3.1 row names/statuses must not be imported automatically.

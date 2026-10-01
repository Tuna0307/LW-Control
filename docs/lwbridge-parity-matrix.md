# LWBridge 0.3.17 parity matrix

This is the current completion authority for the new 0.3.17 program.

The matrix intentionally begins almost empty. Do not pre-fill it with 0.3.1 assumptions.

## Scope exception

The original LWBridge login/account/licensing/entitlement system is **OUT_OF_SCOPE** for reconstruction.

If it is encountered, record only the access boundary. Do not rebuild it or bypass it. A later in-scope feature may justify tracing only the auth-produced state it directly consumes.

## UI matrix

PM-006 lead review keeps `LWB317-UI-COMPLETE-001` historically
**CHANGES_REQUIRED**. PM-007 has reviewed follow-on `LWB317-UI-CORRECT-001`
as **CHANGES_REQUIRED**, accepting Home request scoping and retaining useful
nested UI work. `LWB317-UI-CORRECT-002` is now **AWAITING_REVIEW** after recovery
of the interrupted worker: focused Automation controls, AFK draft isolation and
Map fixture queries have contract, state tests and real browser evidence.
Its `coverage-matrix.md` explicitly records remaining source-recoverable branches.
Complete all-page UI parity is not accepted. Original visual validation and
existing Map live-proof scopes remain separate.

| ID | Surface/state | Reference evidence | Reproduction status | Visual validation | Open gaps |
|---|---|---|---|---|---|
| UI-000 | Login/account/licensing boundary | RUNTIME OBSERVED 2026-09-29; redacted boundary screenshot | OUT_OF_SCOPE | OUT_OF_SCOPE | Dependency-only investigation if later required |
| UI-001 | App launch / initial in-scope shell | EXACT_BYTES shell/CSS + all nine recovered locale chunks; runtime blocked by auth | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Locale/theme/responsive clone QA complete under `LWB317-UI-COMPLETE-001`; reference post-auth geometry/theme/profile-sidebar state unavailable |
| UI-002 | Top-level navigation | EXACT_BYTES definition + locale chunks + inline SVGs + CSS | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Exact eight-item order, translated rendering and selected-state clone QA implemented; reference hover/focus pixels unavailable; Advanced stays off |
| UI-003 | Home | EXACT_BYTES component/status predicates + locale/CSS inventory; existing Desktop read/config contracts | IMPLEMENTED_NOT_VALIDATED | BLOCKED | PM-007 accepts R1 source/transport correction; actual callback deferred ack/error/missing-profile checks pass. PM-015 audit found error translation/channels, busy presentation and localized switch-description gaps. PM-017 accepts HOME-ERROR-001 translation/recovery composition after independent review baf5473. PM-018 integrates db3aae3: HOME-ERROR-002 is CHANGES_REQUIRED for root-status acknowledgement/polling; R1 is ASSIGNED. HOME-BUSY-001 display remains AWAITING_REVIEW. PM-016 accepts UI-SWITCH-LOCALE-001 47c243a for focused source/local scope after 360 nine-locale original/production helper comparisons and focused browser QA. Production lifecycle busy producers remain absent. Native preference/live lifecycle proof and original pixels remain separate |
| UI-004 | Automation | EXACT_BYTES tab/card/nested-surface/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | PM-010/012/013 accept local weekly, Trade selection and Purchased-items presentation. PM-014 accepts local cross-server setting 003D at bd808994. 003E is AWAITING_REVIEW after project-lead takeover: status defaults, supplied fetch-error text and retained goods/purchases pass source/local/browser checks; independent review pending. Native images/providers, Assist/other runtime/purchasing and full Automation/native/pixel parity remain open |
| UI-005 | Map Data | EXACT_BYTES scan/tab/filter/table/action/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED overall; clean Resource runtime flow LIVE_PROVEN | BLOCKED for direct original post-auth visual comparison | CORRECT-002 read-only synthetic dataset honors filters/sort/pages/totals/zero matches; native modes cannot select it and mutations reject. Canonical native Map path preserved. Existing per-category/live-state status remains as documented by Map Goal |
| UI-006 | Squads / AFK | EXACT_BYTES AFK/profile/equipment/nested locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | CORRECT-002 AWAITING_REVIEW: complete per-ID target/squad/execution/filter/join/member drafts, new-profile independence, navigation retention and local retry/discard proved in targeted QA. Actual target inventory/range warning and member loading/left/self variants need further UI coverage; physical drag/native persistence/original pixels unproved. Equipment corrections preserved |
| UI-007 | City Layout | EXACT_BYTES disconnected + workbench/grid/inspector/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Fenced local draft/history/selection/movement/validation model audited; browser DOM drag, invalid-overlap, Undo and Ctrl+Y QA recorded. Exact runtime city data/geometry unavailable |
| UI-008 | Hotkeys | EXACT_BYTES shortcut/card/conditional config/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Seven cards plus attack speedup option states and load/save errors audited; real browser Attack/item/diamond interaction recorded. Persisted config/reference pixels unavailable |
| UI-009 | Mini Games | EXACT_BYTES helper/card/status/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Recovered Land/Food House result/status branches and local chest toggle audited; gameplay buttons stay disabled and no native success is fabricated |
| UI-010 | Settings | EXACT_BYTES metrics/profile-focus/feedback/update/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | BLOCKED | Metrics/account-focus/feedback/updater branches audited; real browser local toggle QA recorded. Native updater/diagnostic providers remain unavailable |
| UI-011+ | Additional in-scope 0.3.17 surfaces | ADD ONLY AFTER EVIDENCE | NOT STARTED | NOT STARTED | Unknown until inventory |

## Function matrix

Add one row per in-scope 0.3.17 function only after it is observed/recovered.

| ID | Function | UI trigger | Frontend/API | Host/runtime | Storage/state | Current-client map | Live proof |
|---|---|---|---|---|---|---|---|
| MAP-001A | Map frontend + host command surface | EXACT_BYTES UI inventory | EXACT_CONTRACT | EXACT_CONTRACT | N/A | N/A | LIVE_PROVEN through the canonical `src/LWBridge.UI-0.3.17` normal Desktop path: zero-argument launch, packaged build identity, native WebView bridge and all eight primary pages smoke-proven; legacy WebUI preserved only for deliberate recovery/reference |
| MAP-001B | Scan lifecycle + acquisition | Map Start/Stop/Clear/status + Auto Scan | EXACT_CONTRACT | EXACT_CONTRACT; Map317 production plane | EXACT_CONTRACT; per-profile Map DB | Resource current path LIVE_PROVEN; City/Monster/Truck/Dispatch LIVE_PROVEN acquisition; Railway/Ghost/Treasure BLOCKED_BY_LIVE_STATE positive rows | Resource CURRENT_PATH_COMPLETE/LIVE_PROVEN; Manual lifecycle/active stop/Clear LIVE_PROVEN; bounded current-server Auto Scan LIVE_PROVEN; restart reconciliation is deterministic rebuild policy, not live-proven exact 0.3.17 behavior |
| MAP-001C | Query/options/summary/export/marks/history | Map data/search/export UI | EXACT_CONTRACT | EXACT_CONTRACT; Map317 production plane | EXACT_CONTRACT; schema v4 | Resource and positive City/Monster/Truck/Dispatch query paths LIVE_PROVEN | Resource canonical UI query/page/filter LIVE_PROVEN; City/Monster/Truck/Dispatch acquisition/summary/options/search LIVE_PROVEN at native/current-client boundary, direct canonical-WebView positive-row rendering IMPLEMENTED_NOT_VALIDATED; export/marks deterministic EXACT_CONTRACT; server history EXACT_CONTRACT |
| MAP-001D | Navigation + Treasure inspection | row actions / Treasure refresh | EXACT_CONTRACT | EXACT_CONTRACT | EXACT_CONTRACT where local state is owned | Coordinate navigation LIVE_PROVEN; Treasure claim/status fail-closed BLOCKED | Coordinate provider LIVE_PROVEN; canonical coordinate button wiring IMPLEMENTED_NOT_VALIDATED at direct click scope; Treasure positive-row inspection BLOCKED_BY_LIVE_STATE and claim/status BLOCKED |
| MAP-001E | Dispatch share + scheduled Dispatch/Truck plunder | Map row actions / Scheduled Plunder | EXACT_CONTRACT | EXACT_CONTRACT; arm->pending->result workers | EXACT_CONTRACT; durable jobs/history/server-day | IMPLEMENTED_NOT_VALIDATED; v22 share/server-day/Dispatch/Truck paths revalidated | IMPLEMENTED_NOT_VALIDATED — intentionally not live-executed because these actions can affect gameplay/alliance state |
| MAP-001F | Ghost plunder preparation | Scheduled Plunder Ghost rows | EXACT_CONTRACT scheduling boundary | EXACT_CONTRACT host boundary | EXACT_CONTRACT durable row semantics | BLOCKED — current-v22 `prepareGhostPlunderTasks` equivalent not source-proven; production fails closed | BLOCKED — provider preparation unavailable; positive Ghost rows were also BLOCKED_BY_LIVE_STATE in the category snapshot |

## Completion rule

An in-scope UI row closes only with reference evidence plus reproduction/visual validation.

An in-scope function row closes only with an evidence-backed 0.3.17 contract and appropriate implementation/live validation.

Historical 0.3.1 row names/statuses must not be imported automatically.

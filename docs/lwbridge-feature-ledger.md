# LWBridge 0.3.17 feature ledger

This ledger starts clean for 0.3.17.

Do not copy feature names or statuses from the 0.3.1 ledger until the 0.3.17 reference itself establishes them.

PM-006 leaves `LWB317-UI-COMPLETE-001` historically **CHANGES_REQUIRED**.
PM-007 reviewed follow-on `LWB317-UI-CORRECT-001` as **CHANGES_REQUIRED**.
Home request-scope correction is accepted; useful nested UI progress remains.
UI-CORRECT-002 is now AWAITING_REVIEW after the owner requested completion of the
interrupted worker. Focused Automation controls, independent AFK drafts and
meaningful read-only Map fixture queries pass targeted local QA. Its affected
coverage matrix records remaining implementation gaps. Full all-page UI parity
is not accepted. Separately recorded Map live proofs are unchanged.

| Surface | 0.3.17 inventory | UI parity | Function recovery | Current-client mapping | Live proof | Notes |
|---|---|---|---|---|---|---|
| Login/account/licensing boundary | OUT_OF_SCOPE | OUT_OF_SCOPE | OUT_OF_SCOPE except dependency trace | OUT_OF_SCOPE except dependency map | OUT_OF_SCOPE | Document boundary only; no recreation/bypass |
| App shell / initial in-scope window | EXACT_BYTES shell/CSS/nine-locale contract; runtime BLOCKED | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Source-backed theme/language/responsive clone QA complete; reference post-auth visual validation blocked |
| Top-level navigation | EXACT_BYTES definition/labels/icons/CSS/locales | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact eight-item order, inline SVGs and translated rendering reproduced; Advanced remains dormant; reference visual validation blocked |
| Home | EXACT_BYTES status/predicate/locale/CSS inventory; existing read/config host contracts | IMPLEMENTED_NOT_VALIDATED | NOT STARTED for native lifecycle family; existing read/config presentation wired | Partial existing-state mapping for root/proxy/recovery/config only | BLOCKED for original visual comparison; native launch not validated | PM-007 accepts R1 source/transport correction with actual callback deferred ack/error/missing-profile checks. Native preference/live lifecycle and original-pixel proof remain separate. |
| Automation | EXACT_BYTES tab/card/nested-surface/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | PM-010 accepts focused local weekly 003A/R1 at d065d08: defaults, saving editability, actual immediate writes/concurrent recovery and anchors pass. Active 003B is Trade currency/goods/exclusive selection only; history/Assist/runtime and original/native acceptance remain open. |
| Map Data | EXACT_BYTES static scan/tab/filter/table/CSS inventory | IMPLEMENTED_NOT_VALIDATED overall; canonical Resource runtime flow LIVE_PROVEN; recovered server switcher/Auto/safe action UI implemented | EXACT_CONTRACT — `LWB317-RE-MAP-001` | Resource CURRENT_PATH_COMPLETE/LIVE_PROVEN; City/Monster/Truck/Dispatch acquisition/query LIVE_PROVEN; Railway/Ghost/Treasure positive rows BLOCKED_BY_LIVE_STATE | Resource canonical UI flow, server jump/return, coordinate provider and bounded current-server Auto Scan LIVE_PROVEN; other stated UI scopes remain IMPLEMENTED_NOT_VALIDATED | `src/LWBridge.UI-0.3.17` is canonical. Server jump round trip `2212 -> 2198 -> 2212` is LIVE_PROVEN. Combined v22 scan produced live City/Monster/Truck/Dispatch rows; zero Railway/Ghost/Treasure is a live-state blocker. Direct canonical-WebView positive-row rendering for those four categories and live UI marks/export remain IMPLEMENTED_NOT_VALIDATED. Treasure claim/status and Ghost preparation remain BLOCKED. Resource `GAME_UNIVERSE_COMPLETE` and original private traversal equivalence remain UNKNOWN. |
| Squads / AFK | EXACT_BYTES AFK/profile/equipment/nested locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | CORRECT-002 AWAITING_REVIEW: per-profile target/squad/execution/filter/join/member edits and new defaults survive switching and navigation; local save-state tests pass. Target/range/member variants remain UI gaps; native persistence/physical drag unproved. Equipment corrections retained. |
| City Layout | EXACT_BYTES disconnected + workbench/grid/inspector/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact disconnected path retained; fenced populated draft/history/selection/movement/validation model audited with browser interaction. No production city data fabricated. |
| Hotkeys | EXACT_BYTES shortcut/card/conditional locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Seven shortcuts, conditional attack speedup settings and load/save error states audited; persisted native config not claimed. |
| Mini Games | EXACT_BYTES helper/card/status/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Recovered Land/Food House result/error/status states and local chest toggle audited; gameplay actions remain disabled. |
| Settings | EXACT_BYTES metrics/profile-focus/feedback/update/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Recovered metrics/account-focus/feedback/updater states audited; updater/export provider actions remain disabled. |
| Advanced (dormant navigation helper) | EXACT_BYTES helper; forced off in exact build | N/A | N/A | N/A | N/A | Exact call site passes `false` into the premium/admin gate, so the visible 0.3.17 nav has eight entries |
| Additional visible in-scope surfaces | UNKNOWN | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Add rows only after 0.3.17 evidence |
| Hidden / conditional in-scope surfaces | UNKNOWN | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Add rows only after evidence |
| Non-Map backend/runtime functions | UNKNOWN | N/A | NOT STARTED | NOT STARTED | NOT STARTED | Map runtime is tracked by the dedicated Map Data row; add other function families only after 0.3.17 evidence and a separately authorized Goal. |

Allowed status values:

- `NOT STARTED`
- `INVENTORIED`
- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

## Rule

A historical 0.3.1 feature name is a hypothesis, not a 0.3.17 row. Add it here only after the 0.3.17 reference confirms that the surface/function exists.

Login/account/licensing remains out of scope unless a specific in-scope feature later requires a narrowly defined dependency trace.

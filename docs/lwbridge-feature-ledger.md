# LWBridge 0.3.17 feature ledger

This ledger starts clean for 0.3.17.

Do not copy feature names or statuses from the 0.3.1 ledger until the 0.3.17 reference itself establishes them.

| Surface | 0.3.17 inventory | UI parity | Function recovery | Current-client mapping | Live proof | Notes |
|---|---|---|---|---|---|---|
| Login/account/licensing boundary | OUT_OF_SCOPE | OUT_OF_SCOPE | OUT_OF_SCOPE except dependency trace | OUT_OF_SCOPE except dependency map | OUT_OF_SCOPE | Document boundary only; no recreation/bypass |
| App shell / initial in-scope window | EXACT_BYTES static shell/CSS contract; runtime BLOCKED | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Single-profile shell reproduced from exact static evidence; reference post-auth visual validation blocked |
| Top-level navigation | EXACT_BYTES definition/labels/icons/CSS | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact eight-item order and inline SVG icons reproduced; reference post-auth visual validation blocked |
| Home | EXACT_BYTES static component/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact unresolved Home state reproduced; reference post-auth visual validation blocked |
| Automation | EXACT_BYTES static tab/card/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Seven exact categories and inventoried cards reproduced; runtime persisted state not claimed |
| Map Data | EXACT_BYTES static scan/tab/filter/table/CSS inventory | IMPLEMENTED_NOT_VALIDATED overall; canonical Resource runtime flow LIVE_PROVEN; recovered server switcher/Auto/safe action UI implemented | EXACT_CONTRACT — `LWB317-RE-MAP-001` | Resource CURRENT_PATH_COMPLETE/LIVE_PROVEN; City/Monster/Truck/Dispatch acquisition/query LIVE_PROVEN; Railway/Ghost/Treasure positive rows BLOCKED_BY_LIVE_STATE | Resource canonical UI flow, server jump/return, coordinate provider and bounded current-server Auto Scan LIVE_PROVEN; other stated UI scopes remain IMPLEMENTED_NOT_VALIDATED | `src/LWBridge.UI-0.3.17` is canonical. Server jump round trip `2212 -> 2198 -> 2212` is LIVE_PROVEN. Combined v22 scan produced live City/Monster/Truck/Dispatch rows; zero Railway/Ghost/Treasure is a live-state blocker. Direct canonical-WebView positive-row rendering for those four categories and live UI marks/export remain IMPLEMENTED_NOT_VALIDATED. Treasure claim/status and Ghost preparation remain BLOCKED. Resource `GAME_UNIVERSE_COMPLETE` and original private traversal equivalence remain UNKNOWN. |
| Squads / AFK | EXACT_BYTES static AFK/equipment/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | AFK default and equipment empty state reproduced; UI-007 static-contract refinements complete, reference runtime validation blocked |
| City Layout | EXACT_BYTES static grid/inspector/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact disconnected render path reproduced; no city/building data fabricated |
| Hotkeys | EXACT_BYTES static shortcut/card/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Seven exact shortcut cards reproduced with deterministic clone-only config fixture |
| Mini Games | EXACT_BYTES static helper/card/status/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Four exact helper/card areas reproduced without gameplay actions |
| Settings | EXACT_BYTES static settings/feedback/update/locale/CSS inventory | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact initial performance, feedback and updater static sections reproduced |
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

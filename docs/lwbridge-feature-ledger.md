# LWBridge 0.3.17 feature ledger

This ledger starts clean for 0.3.17.

Do not copy feature names or statuses from the 0.3.1 ledger until the 0.3.17 reference itself establishes them.

| Surface | 0.3.17 inventory | UI parity | Function recovery | Current-client mapping | Live proof | Notes |
|---|---|---|---|---|---|---|
| Login/account/licensing boundary | OUT_OF_SCOPE | OUT_OF_SCOPE | OUT_OF_SCOPE except dependency trace | OUT_OF_SCOPE except dependency map | OUT_OF_SCOPE | Document boundary only; no recreation/bypass |
| App shell / initial in-scope window | EXACT_BYTES static shell/CSS contract; runtime BLOCKED | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Single-profile shell reproduced from exact static evidence; reference post-auth visual validation blocked |
| Top-level navigation | EXACT_BYTES definition/labels/icons/CSS | IMPLEMENTED_NOT_VALIDATED | N/A | N/A | BLOCKED | Exact eight-item order and inline SVG icons reproduced; reference post-auth visual validation blocked |
| Home | EXACT_BYTES static component/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Initial `overview` route; runtime visual observation blocked by auth boundary |
| Automation | EXACT_BYTES static tab/card/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Seven exact top-level categories; runtime visual observation blocked by auth boundary |
| Map Data | EXACT_BYTES static scan/tab/filter/table/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | City default, Manual Scan default; runtime visual observation blocked by auth boundary |
| Squads / AFK | EXACT_BYTES static AFK/equipment/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | `AFK Tasks` exact default; runtime visual observation blocked by auth boundary |
| City Layout | EXACT_BYTES static grid/inspector/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Static layout editor controls/states inventoried; runtime visual observation blocked by auth boundary |
| Hotkeys | EXACT_BYTES static shortcut/card/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Seven exact shortcut cards; runtime visual observation blocked by auth boundary |
| Mini Games | EXACT_BYTES static helper/card/status/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Four exact helper/card areas; runtime visual observation blocked by auth boundary |
| Settings | EXACT_BYTES static settings/feedback/update/locale/CSS inventory | NOT STARTED | NOT STARTED | NOT STARTED | BLOCKED | Performance, conditional account interaction, feedback and updater UI inventoried; runtime visual observation blocked by auth boundary |
| Advanced (dormant navigation helper) | EXACT_BYTES helper; forced off in exact build | N/A | N/A | N/A | N/A | Exact call site passes `false` into the premium/admin gate, so the visible 0.3.17 nav has eight entries |
| Additional visible in-scope surfaces | UNKNOWN | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Add rows only after 0.3.17 evidence |
| Hidden / conditional in-scope surfaces | UNKNOWN | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Add rows only after evidence |
| Backend/runtime functions | UNKNOWN | N/A | NOT STARTED | NOT STARTED | NOT STARTED | Add one row/function after observation/recovery |

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

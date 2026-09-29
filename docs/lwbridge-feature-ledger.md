# LWBridge 0.3.17 feature ledger

This ledger starts clean for 0.3.17.

Do not copy feature names or statuses from the 0.3.1 ledger until the 0.3.17 reference itself establishes them.

| Surface | 0.3.17 inventory | UI parity | Function recovery | Current-client mapping | Live proof | Notes |
|---|---|---|---|---|---|---|
| Login/account/licensing boundary | OUT_OF_SCOPE | OUT_OF_SCOPE | OUT_OF_SCOPE except dependency trace | OUT_OF_SCOPE except dependency map | OUT_OF_SCOPE | Document boundary only; no recreation/bypass |
| App shell / initial in-scope window | BLOCKED | NOT STARTED | N/A | N/A | BLOCKED | Exact reference launch reaches out-of-scope auth boundary; post-auth shell not visually validated |
| Top-level navigation | EXACT_BYTES | NOT STARTED | N/A | N/A | NOT STARTED | Static order/labels recovered; visual state pending |
| Home | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | `overview -> nav.overview -> Home` |
| Automation | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Map Data | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Squads / AFK | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| City Layout | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Hotkeys | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Mini Games | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Settings | EXACT_BYTES label | NOT STARTED | NOT STARTED | NOT STARTED | NOT STARTED | Static navigation evidence only |
| Advanced (conditional) | EXACT_BYTES conditional entry | NOT STARTED | NOT STARTED | NOT STARTED | UNKNOWN | Runtime visibility and exact English label still unknown |
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

# LWBridge 0.3.1 one-to-one parity matrix

**Current through:** `LWB-R8-012`, 2026-09-24
**Reference:** `C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.1.exe`
**SHA-256:** `2a2de09b35bb6a03f26b5e05f949f3aea6215f294127e605d7d78481f855cdff`

This is now the product-completion matrix for the retained product scope. The older 47-case Home/Map acceptance matrix remains historical implementation evidence, not one-to-one completion authority. Account/Login/Authentication and all related account-purpose activation, renewal, unbind, logout, entitlement, credential-persistence and account UI/backend surfaces are explicitly excluded by current owner direction.

| Surface / subsystem | Current parity class | Current fact | Required parity work |
|---|---|---|---|
| Reference EXE identity | EXACT_BYTES authority | Hash reverified 2026-09-24 | Keep immutable |
| Extracted React/Vite feature chunks | EXACT_BYTES source, modified presentation boundary | Original chunks/styles/icons/locales were recovered; generator/API boundary and some product changes alter the shipped rebuild | Remove non-reference transformations except compatibility plumbing that is observationally invisible |
| Original stylesheet/icons/9 locale bundles | EXACT_BYTES | Recovered byte-for-byte | Preserve hashes |
| Normal eight post-login navigation entries | EXACT_CONTRACT / near-exact presentation | Overview, Automation, Map Data, Squads/AFK, City Layout, Hotkeys, Mini-games, Settings recovered | Re-audit every nested state/action against reference |
| Original auth/login/account flows | EXCLUDED — explicit owner directive | Login, registration, authentication, account management, activation/renewal, unbind, logout, entitlement and account-purpose UI/backend are intentionally outside retained scope | Do not research or restore; keep historical evidence only |
| Header geometry / non-account controls | PARTIAL / requires retained-scope audit | Earlier rebuild changed header geometry while removing account controls | Restore only retained non-account header behavior; do not reintroduce account/auth controls |
| Advanced page visibility | EXACT_CONTRACT | Reference hard-hides Advanced in normal navigation | Preserve exact behavior |
| Home/Overview frontend | EXACT_BYTES-derived | Original component is used | Audit transformed API boundary and all connected states |
| Home lifecycle backend | EQUIVALENT_REIMPLEMENTATION, parity not closed | Current C# lifecycle works and has live evidence | Recover original Rust/launcher/proxy behavior far enough to prove one-to-one semantics |
| Map Data frontend | EXACT_BYTES-derived with deviations | Original component family is used, but prior rebuild removed/added product behavior | Restore reference controls and remove non-reference additions |
| City Excel export | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-007 restores original API/UI/locales, native save dialog, 200-row paging/200k limit, exact 12-column six-part XLSX writer, filename and result envelope | Preserve with strict-parity regression checks |
| `map_scan_clear` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-008 restores the positive current-live-server gate, exact errors, server-scoped deletion, player-mark preservation and Manual-only frontend control | Preserve; do not reintroduce `serverId=0`/saved-server Clear |
| `server_jump` public contract | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-009 restores `{changed, previousServerId, serverId}` and retains recovered validation/error behavior | Recover only the still-protected travel internals and exact numeric timeout before claiming full implementation parity |
| `map_summary` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-010 restores exact `{serverId, counts, scanState}`, exactly eight original count keys, shared-state server ownership and active-run vs published count selection | Audit the shared `map_scan_status` serializer separately; do not reintroduce saved-server fallback or `savedServerIds` into this command |
| `map_data_options` | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-011 restores exact recovered top-level order, eight count kinds, Resource/Monster name families, server-scoped published fallback and matching active-run staging; removes public `zombie_boss`, `monsterLevels` and all-server fallback | Complete nested `scanProgress` serializer only when stronger evidence exists; preserve current strict-parity regression |
| Manual `map_scan_start` public contract | EXACT_CONTRACT + EQUIVALENT_REIMPLEMENTATION | R8-012 restores exactly eight selectable kinds, Manual Normal/Fast UI/persistence, Normal=8/Fast=20, original null/non-string default-to-Normal behavior and exact invalid-string error; current-client strategy choice no longer rewrites public mode | Preserve; recover status/stop and protected pacing/traversal separately |
| Scheduled Plunder surfaces | DEVIATION | Previously owner-retired and removed | Re-audit reference and restore every retained original surface/worker/action that belongs to 0.3.1 |
| Secret Task Quick Find product feature | DEVIATION REMOVED | Added by R7-151 from a current-game native finder; R8-002 removes the product surface because it is not established as LWBridge 0.3.1 behavior | Restore only if reference evidence proves it exists |
| Map scan request envelope | EXACT_CONTRACT | `startMapScan` fields, accepted gate, selected types, normal/fast concurrency recovered | Preserve; recover remaining handler internals |
| Original Map scan algorithm | UNKNOWN | Current scanner is our reconstruction and includes R7-specific strategies | Recover original bridge script/host scan implementation before further redesign |
| Player City acquisition | EQUIVALENT_REIMPLEMENTATION only | Current/old traversal can find full population, but algorithm is ours | Recover original acquisition path and compare identities/results |
| Resource acquisition | EQUIVALENT_REIMPLEMENTATION only | Current path works but is not original code | Recover original path |
| Monster / Doom Walker acquisition | EQUIVALENT_REIMPLEMENTATION only | Current path works | Recover original path |
| Zombie Boss acquisition | EQUIVALENT_REIMPLEMENTATION only | Dedicated current-client source is ours | Recover original path |
| Truck acquisition | EQUIVALENT_REIMPLEMENTATION / possible deviation | Direct `LWTrainDataManager` route is a current-game optimization | Prove whether original LWBridge used the same source/semantics |
| Railway acquisition | EQUIVALENT_REIMPLEMENTATION / possible deviation | Direct Train-list route is ours | Recover original source/flow |
| Dispatch / Secret Task acquisition | UNKNOWN; known deviation rolled back | R8-002 removes the wide-FOV 68-request production shortcut after same-client completeness failures; temporary movement/AOI behavior is still our reconstruction | Recover the original LWBridge implementation before claiming parity |
| Ghost Ops acquisition | EQUIVALENT_REIMPLEMENTATION only | Current full-world path is ours | Recover original implementation |
| Treasure/Supplies read paths | PARTIAL EXACT_CONTRACT + reimplementation | Many original fields/query/state contracts recovered; protected orchestration incomplete | Recover original protected handlers/scripts |
| Treasure claim orchestration | UNKNOWN | Exact original protected implementation not recovered | Recover from original package/host/proxy evidence before enabling |
| Map SQLite/index/query semantics | PARTIAL EXACT_CONTRACT + reimplementation | Large portions of SQL/query/normalization recovered | Finish unresolved branches and compare exact outputs/errors |
| Marks / Jump / Follow | PARTIAL EXACT_CONTRACT + reimplementation | Rebuild behavior tested | Tie every branch/default/error to reference |
| Auto Scan | PARTIAL EXACT_CONTRACT + reimplementation | Current scheduler includes owner workflow changes and later routing optimizations | Recover exact original scheduler, travel, timing and failure behavior |
| Original bridge pipe framing | PARTIAL EXACT_CONTRACT | Frame length and hello fields recovered | Recover exact hello.ack, readiness, request/result grammar and failure mapping |
| `bridge-scripts.dat` plaintext | PARTIAL EXACT_CONTRACT / evidence-limited | Earlier R8 work recovered substantial package/crypto structure, but the remaining LWKE1 field map/AAD is blocked on genuinely new permitted evidence | Keep this lane parked while evidence-limited; resume only for retained non-account runtime needs when new permitted evidence appears |
| Secure/plain xLua proxy behavior | PARTIAL EXACT_CONTRACT | Many hashes, ABI, crypto and loader facts recovered | Recover remaining package/handler/control semantics |
| Profile launcher / multi-hook | PARTIAL EXACT_CONTRACT + reimplementation | Architecture recovered, rebuild uses its own lifecycle code | Audit one-for-one behavior |
| Automation page functions | UI exact-derived, backend parity UNKNOWN | Original component assets exist | Build feature-by-feature parity inventory and recover handlers |
| Squads / AFK | UI exact-derived, backend parity UNKNOWN | Original component assets exist | Recover all connected functions |
| City Layout | UI EXACT_BYTES / backend MISSING | `CityLayoutPanel-B4B03XEi.js` hash matches original; eight wrappers present; helper recovered draft persistence, polling, host bridge boundaries and UI contract | Implement eight backend commands from `docs/reviews/2026-09-24-r8-city-layout-exact-contract.md`; do not invent protected planner/executor |
| Hotkeys | UI exact-derived, backend parity UNKNOWN | Original component assets exist | Recover all connected functions and persistence semantics |
| Mini-games | UI exact-derived, backend parity UNKNOWN | Original component assets exist | Recover all connected functions |
| Settings | UI exact-derived, backend parity UNKNOWN | Original component assets exist | Recover all settings/defaults/update/feedback behavior |
| Whole-program one-to-one release | NOT READY | Prior acceptance measured reconstructed Home/Map functionality, not whole-program parity | Close every required DEVIATION/UNKNOWN and live-prove the final product |

## Immediate retained-scope sequence

1. Preserve and fingerprint the reference and every recovered embedded asset.
2. Restore `map_scan_status` and `map_scan_stop` public/state parity.
3. Restore `map_search` filter ownership and remove rebuild-added Monster/Resource level semantics.
4. Roll Auto Scan back to the original scheduler/state machine, then restore Scheduled Plunder.
5. Continue retained whole-program parity across Automation, Squads/AFK, City Layout, Hotkeys, Mini-games and Settings.
6. Keep the evidence-limited package-key lane parked until genuinely new permitted evidence exists, and do not expand it into Account/Login/Authentication recovery.
7. Perform reference-vs-rebuild UI/behavior comparisons and current-client live validation.

No feature is considered complete merely because our current implementation works.

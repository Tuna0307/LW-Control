# LWBridge 0.3.17 project status

**Date:** 2026-10-01
**Branch:** `research/offline-controller`
**Phase:** 2 — function recovery / Map Goal production-frontend checkpoint

## Current state

Phase 1 static UI recovery has been accepted. The active target remains exact
LWBridge 0.3.17, and the only active Phase 2 function family is
`LWB317-RE-MAP-001` (Map Data).

The Map frontend/host surface, scan lifecycle, schema-v4 per-profile storage,
query/export/marks/history, navigation/action contracts, and scheduled
Dispatch/Ghost/Truck worker semantics have been recovered from exact 0.3.17
evidence and implemented in the versioned `LWBridge.Map317` plane. Normal
Desktop production uses that plane as the sole persistent Map authority at
`profiles/<profileId>/map-data/map-data.db`; legacy Map persistence is retained
only for explicitly isolated historical proof/replay modes.

Current Last War content-version 22 has passed the Map-specific static/source
compatibility revalidation in `LWB317-COMPAT-MAP-V22-001`.
`LWB317-LIVE-MAP-V22-001` then freshly live-proved the owned-session Map
runtime/world path, coordinate navigation, one complete fast Resource scan
(2500/2500, zero failed/unread), query/filter/sort/pagination and clear/reset;
later completeness diagnostics show its 678-row population was not a trustworthy
full Resource census. `LWB317-LIVE-MAP-V22-002` additionally live-proves a
genuinely active Resource scan stop, retained Map readiness after stop, clear to
the default zero/idle state without losing readiness, a second Resource scan in
the same profile/instance/PID/server, a second controlled stop/clear, and final
same-session readiness. Its first attempt exposed a false
`GAME_CONNECTION_UNAVAILABLE` while the game/session/heartbeat/world were still
healthy; the cause was concurrent use of the single-slot current-client
world-state protocol, fixed minimally by serializing that transaction and covered
by deterministic regression testing.

`LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` then instrumented the full
Resource population and live-proved a second acquisition defect: the old remote
path restored the camera before the correlated AOI response had been serialized,
causing genuine remote `ResPointInfo` candidates to be rejected as
`outside_selected_aoi`. A Resource-scoped deferred-restoration correction waits
for the correlated remote response and serialization, restores the camera, then
waits for the restored-home response. Two corrected full-world scans completed
2500/2500 with zero failed/unread blocks and zero Resource-candidate rejections,
publishing 8,008 and 8,007 unique Resources with full `(0,0)..(999,999)` bounds.
The corrected observable route is `CURRENT_PATH_COMPLETE`; complete game-universe
coverage and original protected/private LWBridge traversal equivalence remain
`UNKNOWN`.

Source-backed adapters remain wired for
acquisition, navigation, Treasure inspection, server-day, Dispatch alliance
share, Dispatch scheduled execution and Truck scheduled execution. Treasure
claim/status and Ghost preparation remain explicit fail-closed current-client
provider gaps rather than approximations.

`LWB317-MAP-UI-INTEGRATION-001` then connected the clean reconstructed
`src/LWBridge.UI-0.3.17` Map Data page to the existing production native bridge.
The final owned v22 Resource-only acceptance completed `2500/2500`, zero
failed/unread, and exposed 7,960 real Resource rows through the clean UI. Page 2
returned 50 rows with zero page-1 key overlap; Resource-name filter `100281`
returned 2,726 rows; Clear returned/rendered zero data while the same owned Map
session/server remained healthy and the UI connection state returned to
`Connected`.

`LWB317-MAP-UI-PRODUCTIONIZE-001` now makes `src/LWBridge.UI-0.3.17` the
canonical production frontend. Desktop builds create a fresh, fingerprinted
`ProductionUi` package from that source and ordinary zero-argument launch selects
it through the existing privileged WebView origin/native bridge. Missing or
invalid canonical assets fail clearly; there is no silent legacy fallback.
`src/LWBridge.Desktop/WebUi` remains packaged only as a deliberate `--legacy-ui`
recovery/reference route, while arbitrary `--ui-root` remains proof-gated.

The productionization acceptance normal-launched the Release executable with no
application arguments, smoke-rendered all eight primary reconstructed pages in
native mode, then completed a Resource-only v22 scan at 2500/2500 with zero
failed/unread and 7,994 live Resource rows. Page 2 rendered 50 rows with zero
page-1 rendered-row overlap, filter `100281` returned 2,740 items, Clear rendered
zero, and the same assistant-owned game instance remained connected. Runtime
diagnostics were clean.

Server jump, restart/resume/saved browse context, other Map categories/actions
and final Map closure remain open.

The prior 0.3.1 reverse-engineering history remains intact as legacy evidence
and is reused only where exact 0.3.17/current-client revalidation exists.

## Target identity

- Version: 0.3.17
- Size: 15,866,880 bytes
- SHA-256: `4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Explicit auth scope exception

The new reconstruction does **not** recreate LWBridge's original login/account/licensing/entitlement system.

If login/locked state is encountered:

- document the access boundary only;
- do not bypass it;
- do not reverse engineer credential/token/license/purchase protocols;
- do not implement the original login/account/licensing UI.

If a later in-scope feature proves it consumes auth-produced state, recover only the minimum downstream dependency contract that feature requires.

## Accepted 0.3.17 work

### LWB317-PM-001

Project-management/documentation reset for the 0.3.17 program.

### LWB317-UI-001A

Accepted static frontend-package baseline.

Established from the exact reference:

- Rust/Tauri 2 + Wry/WebView2 desktop host;
- Vite/React frontend;
- exact 24-record Brotli-compressed frontend asset table;
- all 24 frontend assets recovered and hash-locked;
- exact static top-level navigation keys/order and English labels;
- an Advanced navigation helper exists statically, but this exact 0.3.17 build
  passes `false` into its premium/admin visibility gate, so the effective
  visible navigation has eight entries and Advanced is statically forced off.

Project-lead acceptance review:

`docs/reviews/2026-09-29-LWB317-PM-002-review-ui-001a.md`

## UI campaign result

The project-lead-authorized UI campaign is `ACCEPTED`:

`docs/LOOP_CAMPAIGN_8H.md`

It completed or legitimately blocked each authorized **UI-only** stage:

- runtime/static shell and page inventory;
- common visual-system consolidation;
- clean separate 0.3.17 UI scaffold;
- shell/navigation reproduction;
- accessible page reproduction;
- visual comparison/fix pass (static contract fixes complete; direct post-auth
  reference/pixel comparison blocked by the auth boundary).

The reconstructed UI at `src/LWBridge.UI-0.3.17/` is now the canonical product
frontend and the ordinary Desktop default. Its clean Map page uses the recovered
Map317 commands through the real Desktop native bridge; the Resource manual flow
is `LIVE_PROVEN` by the integration and productionization checkpoints. The
bundled `src/LWBridge.Desktop/WebUi` remains only for explicit recovery/reference.
Future frontend feature work targets the canonical clean project. Phase 2 Map
function recovery remains active under the separate Map Goal; all other function
families remain blocked.

Queue authority:

`docs/LOOP_QUEUE.md`

Loop rules:

`docs/LOOP_WORKER_PROTOCOL.md`

## Desktop / live-validation state

Desktop-control tooling is authorized for the active Map Goal. The Goal also
authorizes bounded Map-only interaction with an **assistant-owned** Last War
session after a fresh process/session ownership check. It does not authorize
non-Map gameplay or auth/licensing/credential work.

The v22 live tasks used assistant-owned game sessions and cleaned them up. The
latest productionization cleanup records no Last War/launcher/LWBridge/helper
processes, no Overview recovery journal or Map WAL/SHM files, and the pristine
v22 package SHA-256
`248f3aeac712b3f14f86bff37a0c365e467897a2248403837c44c1b774f05b22`
restored. Any future live work must still perform a fresh ownership check rather
than reusing that state.

## Historical research value

The 0.3.1 branch history contains deep work on:

- frontend recovery;
- host command inventory;
- Map acquisition;
- Overview lifecycle;
- auth/session/entitlement boundaries;
- bridge/proxy/native transport;
- Last War loader/runtime identity;
- current-client live testing.

Those findings may substantially accelerate future 0.3.17 work, but each reused claim must be revalidated against 0.3.17.

Historical auth research remains archived evidence, not a current reconstruction target.

## Next project-lead milestone

Review `LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001`,
`LWB317-MAP-UI-INTEGRATION-001` and `LWB317-MAP-UI-PRODUCTIONIZE-001` with their
evidence-state boundaries. Resource
acquisition through the corrected current-v22 route is
`LIVE_PROVEN`/`CURRENT_PATH_COMPLETE`, and the clean reconstructed Resource
manual UI flow plus its normal production deployment are also `LIVE_PROVEN`;
`GAME_UNIVERSE_COMPLETE` and original
private traversal equivalence remain `UNKNOWN`. Server jump,
restart/resume/saved browse context and other Map categories/actions remain
open. Do not open another subsystem until the project lead decides the next
bounded Map step or closes the Map Goal.

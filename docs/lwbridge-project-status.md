# LWBridge 0.3.17 project status

**Date:** 2026-10-02
**Branch:** `research/offline-controller`
**Phase:** 2 — function recovery / Map Goal awaiting review

## Current state

Owner clarification: the clone has no login UI; required auth-related local
dependency research is permitted under `AGENTS.md` section 6. No product fallback
is wanted. Historical source is retained; the existing selectable `--legacy-ui`
option has a separate pending retirement task. See
`reviews/2026-10-01-LWB317-PM-004-owner-scope-clarification.md`.

The 2026-10-01 project-lead takeover inspected actual clean/pushed `389df37`,
rather than the stale pasted `13b25f6` handoff. `LWB317-UI-COMPLETE-001`
remains historically `CHANGES_REQUIRED` after PM-006. PM-007 independently
reviewed correction HEAD `543b6eb` and returned UI-CORRECT-001 as
`CHANGES_REQUIRED`: Home profile-scope correction is accepted, and useful
Equipment/dialog/locale work is retained, but Automation controls remain omitted
and AFK drafts leak across profiles. Map fixture QA could not prove applied query
semantics at that reviewed revision. Implementation milestones
`239254a` and `591409d` and their evidence are preserved under
`evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/`.
Native Home launch/close/repair stays separate. No new live scan/backend campaign
was opened. Original post-auth pixel comparison remains separately `BLOCKED`.
See `reviews/2026-10-01-LWB317-PM-007-ui-correction-lead-review.md` for the
historical decision and `reviews/2026-10-01-LWB317-UI-CORRECT-001.md` for the
preserved worker return. Missing UI implementation is separate from unavailable
original pixel validation or future native providers.

The owner subsequently requested recovery of the interrupted CORRECT-002 worker.
Its three modified files and two screenshots were preserved at baseline `a19905d`.
`LWB317-UI-CORRECT-002` is now `AWAITING_REVIEW`: focused Automation controls,
independent AFK drafts/save states and applied read-only Map fixture queries are
implemented with 17 source-contract records, 50 browser observations/assertions
and targeted state/provider checks. Canonical check/build/package checks pass.
See `reviews/2026-10-01-LWB317-UI-CORRECT-002.md` and its affected coverage matrix.
Additional Automation runtime/Trade/Assist, weekly quality adapter wiring and
AFK target/member variants remain implementation gaps; all-page parity is open.

Subsequent partial CORRECT-003 checkpoint `5204f67` adds code for many of those
branches, but Stage B browser/coverage/delivery evidence is incomplete. PM-008
re-ran current canonical/state checks successfully; it does not accept the new
branches as parity-proven. The owner requested smaller continuations, starting
with CORRECT-003A weekly quality settings. Preserve all other code and WIP.

PM-010 subsequently accepted the focused weekly CORRECT-003A/R1 delivery at
`d065d08` after the actual handlers, deferred writes, recovery, anchors and package
checks passed. This is local/source acceptance, not full Automation/native/pixel
parity. PM-012 accepts Trade selection CORRECT-003B/R1 at `bfc652f`: PM-011's
composition defects are fixed; actual-expression/selection/recovery checks,
canonical build/package and evidence hashes pass. PM-013 accepts CORRECT-003C
`1a25a9c` for source/local history presentation after independent helper/regression/
package/evidence checks. Native images remain declared placeholders. PM-014 accepts
CORRECT-003D `bd808994` for local/source cross-server setting UI after actual
callback/regression/package/evidence checks. 003E is AWAITING_REVIEW after
owner-requested project-lead implementation takeover: Trade status defaults,
supplied fetch-error text and retained goods/purchases pass source/local render,
browser, regression and canonical checks. Independent review is pending; see
`reviews/2026-10-02-LWB317-UI-CORRECT-003E.md`. Native providers,
purchasing and cross-server gameplay remain separate.
Full Automation/native/pixel parity remains unaccepted.
PM-015 additionally completed a read-only Home source/state audit: error
translation/channels, busy presentation and shared switch-state localization remain
demonstrated gaps. 960 synthetic comparisons are reproducible; no production UI or
native changes were made. Finding: `reviews/2026-10-02-LWB317-PM-015-home-audit.md`.
Subsequent HOME-ERROR-001 translation-only implementation is AWAITING_REVIEW:
recovered helper priority/namespaces/generic message and recovery detail pass
source/local/nine-locale/browser/canonical checks. See its dated review.
HOME-ERROR-002 channels correction is also AWAITING_REVIEW: actual callback,
original picker/render and local browser checks pass for independent errors and
cancel/invalid/acknowledged valid selections. Existing host contract inspected;
native picker/persistence unproved. HOME-BUSY-001 display correction is also
AWAITING_REVIEW: nine-locale actual render/predicate and local browser checks pass
for independent busy inputs and header/button precedence. Production lifecycle
busy producers remain absent. Switch descriptions remain open; no native
lifecycle or full Home acceptance is implied.

Phase 1 static UI recovery has been accepted. The active target remains exact
LWBridge 0.3.17. The Map-only `LWB317-RE-MAP-001` campaign has completed its
ordered continuation and is `AWAITING_REVIEW`; no other Phase 2 function family
is authorized to begin.

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

The 2026-10-01 continuation additionally recovered restart/reopen ownership and
the exact saved-state boundary, live-proved same-server and `2212 -> 2198 -> 2212`
server jump/return, and ran one combined non-Resource v22 scan. That scan
LIVE_PROVED City (6,850), Monster (9,568), Truck (61) and Dispatch (18)
acquisition/query snapshots; Railway/Ghost/Treasure each had zero live examples
and are `BLOCKED_BY_LIVE_STATE` for positive-row proof. Counts are snapshots, not
fixed expectations. Safe coordinate navigation is LIVE_PROVEN; canonical marks
and City export are deterministically covered. A bounded current-server Auto Scan
cycle completed 2500/2500 with zero failed blocks, was disabled afterward, cleared
its data and retained connected health.

The closeout keeps direct canonical-WebView positive-row rendering for the four
newly positive categories and live UI-level marks/export at
`IMPLEMENTED_NOT_VALIDATED`. Restart orphan reconciliation is rebuild safety
policy with deterministic coverage, not exact 0.3.17 bytes or live termination
proof. Treasure claim/status and Ghost preparation remain fail-closed `BLOCKED`.
Resource `GAME_UNIVERSE_COMPLETE` and exact original private/protected traversal
equivalence remain `UNKNOWN`.

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

PM-008 inspected pushed CORRECT-003 implementation `5204f67`; code/state checks
pass but Stage B evidence and delivery are incomplete. At the owner's request,
the broad task is PARTIAL and split.
PM-010 accepted the focused weekly unit at `d065d08` after R1 closed the actual
callback defect. PM-012 accepts focused local Trade selection CORRECT-003B/R1
at `bfc652f`, closing PM-011's two composition defects with all three unchanged
actual-expression cases and selection/recovery/package/evidence checks passing.
PM-013 accepts CORRECT-003C at `1a25a9c` for local/source purchase-history
presentation with declared native-image placeholders. PM-014 accepts local/source
cross-server setting CORRECT-003D at `bd808994`. 003E Trade stats/last-result/
loading/fetch-error presentation is implemented and locally checked, AWAITING_REVIEW
after project-lead takeover. Parent CORRECT-003 remains PARTIAL. Purchase execution, native
providers, other runtime panels, Assist and AFK remain separate. Other WIP stays
preserved. No full UI acceptance is recorded.
PM-007's historical CHANGES_REQUIRED
decision and accepted Home scope fix remain preserved. Separately close or reassign the awaiting
`LWB317-RE-MAP-001` Goal.
Native Home lifecycle and new backend subsystems require their own project-lead
assignment.

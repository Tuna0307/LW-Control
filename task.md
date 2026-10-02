# Current project directive

Current lead decision, 2026-10-02: PM-028 ACCEPTS FILTER-LIFECYCLE-001 and R1 for focused source/local UI scope at ff4ed369. Lead replays pass, including PM-027 6/6, R1 5/5, parent assertions and maintained regression/package checks. MAP-TREASURE-PICKER-001 implementation is COMPLETE under project-lead takeover: source details/menu, resolved names/counts, strict keys and close behavior; 28 original/current comparisons, parent/regression checks and fresh en/ja browser QA pass. Independent peer review remains a follow-up; no native/gameplay integration. Overall UI/pixel/native status remains unchanged. See the PM-028 acceptance review and picker work item.

## Target

Reproduce the in-scope post-auth LWBridge 0.3.17 experience one-for-one, then recover and implement its functions against the current Last War client.

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Explicit auth scope exception

The new product will **not** recreate LWBridge's original login/account/licensing system.

Owner clarification, 2026-10-01: no login page or commercial account system is
required in the clone. Research into auth-related local code/state/contracts is
allowed where needed to implement a named in-scope feature, using supplied
artifacts and authorized access. Do not stop solely at an auth-related dependency.
Do not circumvent original authentication/entitlement controls or use another
person's credentials. `AGENTS.md` section 6 is the current dependency rule.

One canonical production path is required. Do not add legacy fallback behavior;
retain historical files only as evidence. The existing selectable `--legacy-ui`
path is pending retirement under a separate bounded host assignment.

The parity target therefore begins at the in-scope post-auth application experience.

## Phase order

### Phase 0 — preparation

- clean project management/docs;
- establish 0.3.17 reference identity;
- separate current authority from 0.3.1 historical evidence;
- define worker-AI protocol and evidence naming.

### Phase 1 — UI parity

Copy the in-scope 0.3.17 post-auth UI one-for-one before implementing game behavior.

Deliverables will include:

- complete accessible screen/tab inventory;
- exact text/labels/defaults;
- layout/theme/spacing/assets;
- empty/loading/error/connected states where observable;
- visual comparison evidence;
- navigation/state-transition matrix.

If the reference opens at a login/locked boundary, document that boundary but do not recreate or bypass it.

No gameplay function should be claimed implemented merely because a control exists.

### Phase 2 — function recovery

Recover one in-scope function at a time from the 0.3.17 reference.

For each function trace, where evidence permits:

`UI trigger -> frontend/API call -> host command -> runtime/provider request -> state mutation -> visible result`

Auth-related local internals may be traced where required by the assigned
in-scope feature. Recover the relevant dependency contract and document its
limits; do not reconstruct a commercial account system or circumvent access controls.

### Phase 3 — current-client mapping

Map recovered 0.3.17 behavior to the currently installed Last War client. Compatibility shims may differ internally, but user-visible in-scope behavior should preserve the recovered 0.3.17 contract.

### Phase 4 — live parity validation

Live-prove each implemented in-scope function against an assistant-owned/current client session.

## Current instruction

Current worker update, 2026-10-02: **LWB317-UI-MAP-FILTER-LIFECYCLE-001-R1 is
AWAITING_REVIEW**. The PM-027 options-only redirect mismatch is corrected using
the source `C.serverId` / local `R` lifecycle: original and current now settle on
scan server 321 after options requests [321,322,321]. Delayed Clear remains
[322,321]. The lead six-case checker, five new exact original/current R1 cases,
maintained regressions, canonical check/build/package, evidence validation and
offline browser smoke pass. Parent acceptance remains with the project lead;
overall UI/native/original-pixel status is unchanged. No native/gameplay campaign.

Historical worker delivery, superseded by PM-027: **LWB317-UI-MAP-FILTER-LIFECYCLE-001
AWAITING_REVIEW**. The worker completed all four UI milestones: refreshed-option
validation, encoded alliance identity, acknowledged Clear frontend resets,
Treasure defaults/persistence/query projection and integrated evidence. The
controlled callback/effect suite, accepted INTERACTIONS/NAVIGATION regressions,
real offline browser controls and a 375 px Japanese/dark preview pass; native
Clear/Treasure operations remain unavailable. Independent review found one
duplicate post-Clear option reload, corrected in `3e20fa1`, and the superseding
review at `6b654c5` reports PASS for the focused source/local scope. Project lead
owns acceptance; overall UI/native/original-pixel status is unchanged.

Previous decision, 2026-10-02: **PM-026 accepts INTERACTIONS-001 and corrected
NAVIGATION-001 for focused source/local UI scope**. Lead continued the usage-limit
handoff, reran all A-D checks including the full scheduled render/mutation suite,
closed final English/Japanese browser evidence and fixed stale docs. Product
checkpoint 5b76ae8; this review checkpoint adds evidence/docs only. No active
worker implementation remains; do not restart the completed campaign. The next
bounded UI gap needs a separate assignment; native/gameplay work remains outside
this closeout. Read the PM-026 review for exact open gaps and proof limits.

Historical worker delivery, superseded by PM-026 acceptance: **LWB317-UI-MAP-INTERACTIONS-001 AWAITING_REVIEW** (all four milestones). A (3e8617c) restored request disposal fencing; B/C (c57be79) recovered keyword/search/name/selection/random-delay behavior and the Scheduled Plunder Dispatch/Ghost/Truck tables with the original component executed as oracle; D verified the combination (differential, mutation, replay, integration, offline-browser en/ja light/dark/375 px). Native job producer, scheduling/sharing/cancel/clear, Treasure wiring, scan/options polling parity and original pixels remain open; nothing native or gameplay ran. Lead makes the acceptance decision. See `docs/reviews/2026-10-02-LWB317-UI-MAP-INTERACTIONS-001.md`.

Historical assignment text, superseded by the delivery above: **LWB317-UI-MAP-INTERACTIONS-001 ASSIGNED**.
Owner requests a large task. Read its work item; complete disposal correction,
exact local search/name and per-kind selection behavior, Scheduled Plunder
presentation, and integrated UI checks. Two subagents may work with exclusive
file ownership. Main worker integrates; lead makes final acceptance. No native
or gameplay work. Assignment prepared for manual relay; execution not confirmed.

PM-025 leaves NAVIGATION-001 **CHANGES_REQUIRED** at 305240e: lead reproduced
pending success/rejection changing UI after backend unavailability; previous
cancellation ignored both. Preserve useful caching and fix disposal first.
See `docs/reviews/2026-10-02-LWB317-PM-025-navigation-review.md`.

Previous worker delivery, superseded by PM-025: **LWB317-UI-MAP-NAVIGATION-001 AWAITING_REVIEW**.
The returning worker implemented source-derived normal-tab page/row/total
restoration, cached/uncached loading, search-generation fencing and data-server
cache invalidation. Scheduled Plunder remains a navigation-only boundary.
Persistent callback/effect tests, populated server-321 browser navigation,
focused Map regressions and canonical check/build/package pass. No
native/gameplay/function work was added. Project lead makes the final acceptance
decision; keyword/debounce, selection/plunder lifecycle, native Treasure wiring,
scheduling/claims and original pixels remain separate.

Previous decision, 2026-10-02: PM-024 accepts UI-MAP-FILTERS-001 and its independent
review af73d18 for the six focused source/local behaviors. The lead verified
actual-code/source/evidence/package checks and recorded the reviewer's empty
Checking browser-session limit. Do not repeat that review. Other UI/native/pixel
gaps remain open; the bounded navigation assignment above is the next UI unit.

Historical PM-023 assignment, superseded by PM-024 above: LWB317-UI-MAP-FILTERS-001 was
AWAITING_REVIEW. Lead corrected per-kind Map filters, exact Secret Task level
and an isolated Treasure Checking preview; source/query/render/browser/package
checks pass. Returning-worker independent review is ASSIGNED under
`docs/work-items/LWB317-REVIEW-MAP-FILTERS-001.md`; prompt prepared for owner
relay, execution not confirmed. Review product checkpoint 3d2f6ba only. Existing host
Treasure handlers still need canonical frontend context/refresh wiring in a
separate assignment. Other UI gaps and original pixel validation remain open;
no native/gameplay phase is opened by this checkpoint.

Latest delivery, 2026-10-02: owner requested project-lead takeover while worker
rests. LWB317-UI-PARALLEL-001 is COMPLETE for its assigned source/local scope;
PM-022 accepts independently reviewed Map row/basic-table and Trade status/
inactive-fixture corrections. Relevant checks/build/package and new validators
pass. Do not restart the campaign. Broader Map interactions, Treasure Checking
producer, Automation/AFK conditional states, assets and original pixels remain
open; a next named UI task must be assigned separately. No native/gameplay work.

Historical assignment direction, superseded by delivery above: speed up UI progress using the returning
worker and two subagents. Current assignment:
`docs/work-items/LWB317-UI-PARALLEL-001.md` (now delivered by lead takeover).
Coordinator corrects Map row defects, A independently reviews/corrects Trade 003E,
B reviews City/Resource/Monster tables read-only. This explicitly supersedes
historical single-worker/no-subagent limits only for those three lanes. It does
not reopen gameplay/native integration or the interrupted broad campaign.
Transport review 266ce2a returned CHANGES_REQUIRED; lead independently reproduced
14 mismatches grouped into two defects and validated its evidence. No product
code changed in this assignment checkpoint. Historical instructions below remain
context; follow the latest named scope and ownership rules.

2026-10-02 owner-requested larger lead takeover is delivered as
`LWB317-UI-LEAD-TABLES-001` **AWAITING_REVIEW**: eight normal Map tables corrected
against exact source with nine-locale differential/render, browser and package
evidence. See `docs/reviews/2026-10-02-LWB317-UI-LEAD-TABLES-001.md` and its
coverage matrix. Returning worker's assigned independent Home busy review remains
pending; Trade 003E and this Map unit also need independent review. Lead author
rechecks do not constitute that acceptance. No native/gameplay scope is opened;
protected AFK/scratch/parent screenshot WIP remains unchanged/unstaged.

PM-007 reviewed the UI-CORRECT-001 worker return at `543b6eb` and marked it
`CHANGES_REQUIRED`. Home reconnect profile scoping is accepted; preserve useful
Equipment/dialog/locale and other UI corrections. Source-recoverable Automation
controls were missing, AFK drafts leaked between profiles, and Map fixture QA
did not apply query semantics at that reviewed revision. Historical lead review:
`docs/reviews/2026-10-01-LWB317-PM-007-ui-correction-lead-review.md`.
Evidence: `evidence/lwbridge-0.3.17/ui/LWB317-UI-CORRECT-001/lead-review/`.
Earlier UI-COMPLETE-001/PM-006 history remains preserved.

The owner requested recovery of the interrupted worker. The focused
`docs/work-items/LWB317-UI-CORRECT-002.md` checkpoint is now `AWAITING_REVIEW`:
Automation controls, per-ID AFK drafts/save states and applied read-only Map
fixture queries pass targeted contract/state/browser QA. Current delivery:
`docs/reviews/2026-10-01-LWB317-UI-CORRECT-002.md`. Its affected coverage matrix
records remaining source-recoverable UI gaps. CORRECT-003 subsequently pushed
substantial implementation at 5204f67, but Stage B evidence/delivery is unfinished.
The owner requested smaller tasks, replacing its active broad scope.
PM-010 reviewed R1 delivery d065d08 and accepted the focused local weekly unit
(CORRECT-003A/R1): immediate actual callbacks, concurrent drafts, Retry/Discard
and source anchors pass. Native persistence/original pixels/full UI are unaccepted.
PM-012 accepts focused local Trade selection CORRECT-003B/R1 at bfc652f after
composition/selection/recovery/package/evidence checks pass. Both assignments
are closed; PM-011's failing baseline is preserved. PM-013 accepts source/local
Purchased-items presentation CORRECT-003C at 1a25a9c after helper/regression/
package/evidence checks. Native images remain declared placeholders. PM-014 accepts
local/source cross-server setting CORRECT-003D at bd808994 after actual callback/
regression/package/evidence checks. Owner requested project-lead takeover of
`docs/work-items/LWB317-UI-CORRECT-003E.md`; it is now AWAITING_REVIEW after
Trade counters/last-result defaults, supplied fetch-error text and independent
goods/purchases were implemented and source/local/browser checked. Independent
review is the exact continuation point; parent CORRECT-003 remains PARTIAL.
Do not run a second worker on this unit concurrently. No native providers, cross-server gameplay,
purchase execution or other panels are opened.
Preserve accepted history/selection/weekly work and uncommitted AFK/scratch files.
No new native/gameplay family is opened; all-page parity is not accepted.
Owner subsequently requested tasks for the lead while the worker rests. PM-015
Home source/state audit is COMPLETE, with 960 synthetic comparisons and exact
source locators. See `docs/reviews/2026-10-02-LWB317-PM-015-home-audit.md`.
Queued lead continuations: Home error translation/channels, Home busy presentation,
then shared switch-state localization as separate small units. No worker dispatch,
production edit or native lifecycle implementation is implied by this audit.
Owner requested continuation; lead completed HOME-ERROR-001 translation-only
implementation, now ACCEPTED for focused source/local scope after independent
review baf5473 and PM-017 integration. Delivery:
`docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-001.md`. Exact helper/render,
nine-locale, browser and canonical checks pass; no state-channel/native changes.
HOME-ERROR-002 is COMPLETE / ACCEPTED by PM-019 after R1; historical independent root/action errors,
picker cancel/invalid/acknowledged valid handling and actual callback/original
render/browser checks. See docs/reviews/2026-10-02-LWB317-UI-HOME-ERROR-002.md.
HOME-BUSY-001 is also AWAITING_REVIEW: independent proxy/launch display inputs
and header/button precedence pass nine-locale render/predicate and browser checks.
App unchanged; lifecycle busy producers absent. See its dated review.
PM-016 accepts UI-SWITCH-LOCALE-001 47c243a for focused source/local scope:
recovered/production ToggleRow comparison passes 360 nine-locale cases, all 11
callers are inventoried, focused browser QA passes and canonical/Home regressions
remain green. See docs/reviews/2026-10-02-LWB317-PM-016-switch-locale-acceptance.md.
PM-017 acceptance: docs/reviews/2026-10-02-LWB317-PM-017-home-translation-acceptance.md.
PM-019 accepts HOME-ERROR-002 and R1 at 8415316 for focused source/local
error-channel and root acknowledgement/polling scope. Fourteen actual-callback/
effect scenarios and four independent acknowledgement comparisons pass; accepted
translation/switch regressions and package verification remain green. Native
contracts/persistence/lifecycle and original pixels remain unproved. Busy remains
AWAITING_REVIEW; LWB317-REVIEW-HOME-BUSY-001 is ASSIGNED for presentation only.
See `docs/reviews/2026-10-02-LWB317-PM-019-home-root-correction-acceptance.md` and
`docs/work-items/LWB317-REVIEW-HOME-BUSY-001.md`.
Native picker/persistence/lifecycle and original pixels remain unproved.

Direct original post-auth screenshot/pixel comparison remains `BLOCKED` by the
documented reference access boundary, so affected UI rows remain
`IMPLEMENTED_NOT_VALIDATED`. This result does not open native Home lifecycle
integration or authorize game launch/control. The Map Goal remains
`AWAITING_REVIEW`; no worker may automatically resume its completed campaign or
begin another backend family until project-lead review.

The project is in **Phase 2 — function recovery**. Phase 1 static UI recovery is
accepted. The Map-only `LWB317-RE-MAP-001` Goal in
`docs/GOAL_CAMPAIGN_PHASE2_MAP.md` is `AWAITING_REVIEW`; no other function family
may begin until project-lead closure. `LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` established
the corrected observable Resource path as `LIVE_PROVEN` /
`CURRENT_PATH_COMPLETE`, while complete game-universe/original-private-traversal
coverage remains `UNKNOWN`. `LWB317-MAP-UI-PRODUCTIONIZE-001` was project-lead
accepted at `086757e36562b76d7e45b857a282c51267e16171`: `src/LWBridge.UI-0.3.17` is the canonical production
frontend and a zero-argument normal Desktop launch uses its verified packaged
`ProductionUi` assets through the existing native bridge. The preserved
`src/LWBridge.Desktop/WebUi` is an explicit recovery/reference path, not the
default. A fresh normal-launch Resource acceptance completed 2500/2500 with zero
failed/unread, rendered 7,994 Resources, proved page-2/filter/Clear and retained
the same connected game instance. The ordered Map continuation also live-proved
server jump/return, acquired City/Monster/Truck/Dispatch in a combined v22 scan,
live-proved safe coordinate navigation, and live-proved a bounded current-server
Auto Scan cycle. The Goal is `AWAITING_REVIEW`; direct canonical-WebView positive
row rendering for the newly acquired non-Resource categories and live UI-level
marks/export remain `IMPLEMENTED_NOT_VALIDATED`. Railway/Ghost/Treasure positive
rows were `BLOCKED_BY_LIVE_STATE`; Treasure claim/status and Ghost preparation
remain `BLOCKED`; Resource `GAME_UNIVERSE_COMPLETE` and original private traversal
equivalence remain `UNKNOWN`.
Auth/login/licensing reconstruction remains out of scope.

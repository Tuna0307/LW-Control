# Current project directive

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
HOME-ERROR-002 is CHANGES_REQUIRED after PM-018; historical independent root/action errors,
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
PM-018 integrates independent review db3aae3: HOME-ERROR-002 is CHANGES_REQUIRED for
root-status acknowledgement/polling. Root/action placement remains useful; correction
LWB317-UI-HOME-ERROR-002-R1 is ASSIGNED. HOME-BUSY-001 remains AWAITING_REVIEW. See
docs/reviews/2026-10-02-LWB317-PM-018-home-channel-review-integration.md and
docs/work-items/LWB317-UI-HOME-ERROR-002-R1.md.
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

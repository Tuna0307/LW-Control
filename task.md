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

Project-lead takeover on 2026-10-01 reaffirms the user's Home/Map UI/UX priority.
The next authorized bounded worker is **UI-only**
`docs/work-items/LWB317-UI-HOME-STATES-001.md`: recover and reproduce missing Home
render states using exact bytes and isolated clone QA. It does not open Home
native lifecycle integration or authorize game launch/control. The Map Goal
remains `AWAITING_REVIEW`; no worker may automatically resume its completed
campaign or begin another backend family. See the takeover audit
`docs/reviews/2026-10-01-LWB317-PM-003-project-lead-takeover.md`.

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

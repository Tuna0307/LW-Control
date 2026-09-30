# Current project directive

## Target

Reproduce the in-scope post-auth LWBridge 0.3.17 experience one-for-one, then recover and implement its functions against the current Last War client.

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Explicit auth scope exception

The new product will **not** recreate LWBridge's original login/account/licensing system.

Login/auth/entitlement is treated as a boundary:

- capture only enough visible evidence to understand that boundary;
- do not implement the original login/account/licensing UI;
- do not reverse engineer credential/token/license/purchase protocols;
- do not bypass authentication or entitlement;
- if a later target feature depends on auth-produced state, recover only the minimum downstream state contract that feature requires.

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

Auth/login internals are excluded unless a specific in-scope function proves it needs a minimal auth-produced dependency. In that case trace only the consumed state contract.

### Phase 3 — current-client mapping

Map recovered 0.3.17 behavior to the currently installed Last War client. Compatibility shims may differ internally, but user-visible in-scope behavior should preserve the recovered 0.3.17 contract.

### Phase 4 — live parity validation

Live-prove each implemented in-scope function against an assistant-owned/current client session.

## Current instruction

The project is in **Phase 2 — function recovery**. Phase 1 static UI recovery is
accepted. The active work item is the Map-only `LWB317-RE-MAP-001` Goal in
`docs/GOAL_CAMPAIGN_PHASE2_MAP.md`; no other function family may begin until
that Goal is closed. `LWB317-LIVE-MAP-V22-RESOURCE-COMPLETENESS-001` established
the corrected observable Resource path as `LIVE_PROVEN` /
`CURRENT_PATH_COMPLETE`, while complete game-universe/original-private-traversal
coverage remains `UNKNOWN`. `LWB317-MAP-UI-PRODUCTIONIZE-001` was project-lead
accepted at `086757e36562b76d7e45b857a282c51267e16171`: `src/LWBridge.UI-0.3.17` is the canonical production
frontend and a zero-argument normal Desktop launch uses its verified packaged
`ProductionUi` assets through the existing native bridge. The preserved
`src/LWBridge.Desktop/WebUi` is an explicit recovery/reference path, not the
default. A fresh normal-launch Resource acceptance completed 2500/2500 with zero
failed/unread, rendered 7,994 Resources, proved page-2/filter/Clear and retained
the same connected game instance. Server jump, restart/resume/saved browse
context, other Map categories/actions and final Map closure remain open.
Auth/login/licensing reconstruction remains out of scope.

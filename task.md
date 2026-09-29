# Current project directive

## Target

Reproduce LWBridge 0.3.17 one-for-one, then recover and implement its functions against the current Last War client.

Reference:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

## Phase order

### Phase 0 — preparation

- clean project management/docs;
- establish 0.3.17 reference identity;
- separate current authority from 0.3.1 historical evidence;
- define worker-AI protocol and evidence naming.

### Phase 1 — UI parity

Copy the 0.3.17 UI one-for-one before implementing game behavior.

Deliverables will include:

- complete screen/tab inventory;
- exact text/labels/defaults;
- layout/theme/spacing/assets;
- empty/loading/error/connected states where observable;
- visual comparison evidence;
- navigation/state-transition matrix.

No gameplay function should be claimed implemented merely because a control exists.

### Phase 2 — function recovery

Recover one function at a time from the 0.3.17 reference.

For each function trace, where evidence permits:

`UI trigger -> frontend/API call -> host command -> runtime/provider request -> state mutation -> visible result`

### Phase 3 — current-client mapping

Map recovered 0.3.17 behavior to the currently installed Last War client. Compatibility shims may differ internally, but user-visible behavior should preserve the recovered 0.3.17 contract.

### Phase 4 — live parity validation

Live-prove each implemented function against an assistant-owned/current client session.

## Current instruction

Do **not** begin UI reproduction or reverse engineering yet. The repository is currently being prepared so other AIs can take bounded work items safely.

The project lead owns the master plan and will assign the first UI work item later.

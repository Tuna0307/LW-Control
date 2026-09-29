# Loop worker protocol

This protocol exists for Chat On Steroids **Loop** mode.

Loop mode is allowed to keep the worker active, but it does **not** make the
worker the project lead.

## Authority

The project lead owns:

- phase order;
- work-item creation;
- acceptance/rejection of completed work;
- master parity status;
- when function reverse engineering begins;
- when Last War/live testing is allowed.

The loop worker owns only the currently `READY` work item in
`docs/LOOP_QUEUE.md`.

## Required start sequence

At the start of every continuation:

1. read `AGENTS.md`;
2. read `task.md`;
3. read `docs/PROJECT_LEAD.md`;
4. read `docs/AI_WORK_PROTOCOL.md`;
5. read `docs/LOOP_QUEUE.md`;
6. inspect `git status --short`;
7. identify exactly one `READY` work item;
8. read that work-item file completely before acting.

Do not select a different task because it looks more interesting.

## Continuation behavior

If the current work item is incomplete:

- continue only that work item;
- prefer concrete evidence over commentary;
- keep findings durable in the repository;
- use approved desktop tooling only when the work item permits it;
- keep the work bounded to the stated non-goals.

If the work item is complete:

1. run its required validation;
2. run `git diff --check`;
3. create one coherent commit;
4. push to `origin/research/offline-controller`;
5. update only that row in `docs/LOOP_QUEUE.md` from `READY` to
   `AWAITING_REVIEW`;
6. write/finish the required worker report;
7. do **not** start another work item unless another row is explicitly already
   marked `READY` by the project lead.

If no work item is `READY`:

- do not change project files;
- do not invent a new task;
- do not start reverse engineering;
- do not start implementation;
- report exactly:

`WAITING_FOR_PROJECT_LEAD`

The user may then stop Loop mode or wait for the project lead to queue another
item.

## Desktop rules

Desktop control is currently allowed for assigned UI-parity observation.

That does not imply permission to:

- launch/control Last War;
- perform gameplay tests;
- bypass login/auth/entitlement;
- press unrelated function/action controls;
- expand a UI-capture task into backend reverse engineering.

The work-item file decides what desktop actions are permitted.

## Evidence rule

Never promote an inference into fact.

Use the project evidence states:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`

Every important fact must name the exact 0.3.17 source/observation.

## Time/checkpoint rule

Even in Loop mode, preserve a coherent checkpoint roughly every 20 minutes.

If a work item cannot be finished in one block, commit only when the state is
coherent and the work item allows an intermediate checkpoint. Otherwise leave
the tree understandable and record the exact continuation point.

## Prohibited loop behavior

Loop mode must never become:

- “keep reverse engineering anything useful”;
- “keep improving the UI”;
- “continue until the whole project is done”;
- “fix whatever you notice”.

Those prompts destroy evidence boundaries and project-lead control.


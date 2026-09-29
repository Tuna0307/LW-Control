# Loop worker protocol

This protocol exists for Chat On Steroids **Loop** mode.

Loop mode may keep a worker active for many continuations, but it does **not** make the worker the project lead.

## Authority

The project lead owns:

- phase order;
- campaign/work-item creation;
- acceptance/rejection of completed work;
- master parity status;
- when function reverse engineering begins;
- when Last War/live testing is allowed;
- whether any auth-produced dependency deserves investigation.

A Loop worker may execute only:

1. a work item marked `READY` in `docs/LOOP_QUEUE.md`; or
2. the ordered stages in a project-lead-authored campaign file explicitly marked `ACTIVE` in that queue.

The worker must never invent the next project task.

## Required start sequence

At the start of every continuation:

1. read `AGENTS.md`;
2. read `task.md`;
3. read `docs/PROJECT_LEAD.md`;
4. read `docs/AI_WORK_PROTOCOL.md`;
5. read `docs/LOOP_WORKER_PROTOCOL.md`;
6. read `docs/LOOP_QUEUE.md`;
7. inspect `git status --short`;
8. if the local branch is merely behind the remote and otherwise clean, fast-forward with:
   `git pull --ff-only origin research/offline-controller`;
9. resume exactly the first incomplete authorized task/stage.

Do not select a different task because it looks more interesting.

## Single-work-item mode

If a normal `READY` work item is active:

- continue only that work item;
- follow its explicit scope/non-goals;
- preserve concrete evidence;
- validate/commit/push when complete;
- change its queue state to `AWAITING_REVIEW`;
- do not start a BLOCKED item.

## Campaign mode

If one campaign is marked `ACTIVE`:

- read the campaign file completely;
- execute its pre-authorized stages in order;
- do not wait for project-lead review between stages that the campaign explicitly authorizes;
- checkpoint/commit/push after each coherent completed stage;
- skip to a later stage only when the campaign itself permits that behavior;
- never continue past the campaign's explicit STOP boundary.

A campaign is permission to execute a prewritten plan, **not** permission to broaden the plan.

## Login/auth/licensing boundary

The clone is not recreating LWBridge's original login/account/licensing system.

During any Loop campaign:

- do not implement login/account/licensing UX;
- do not reverse engineer credential/token/license/purchase protocols;
- do not bypass authentication or entitlement;
- if login blocks runtime observation, document that boundary and continue only with independently authorized static/offline UI work;
- a dependency investigation is allowed only if a future project-lead work item explicitly names the exact downstream auth-produced state to recover.

## Desktop rules

Desktop control is currently allowed for assigned UI-parity observation/reproduction.

That does not imply permission to:

- launch/control Last War;
- perform gameplay tests;
- bypass login/auth/entitlement;
- press unrelated function/action controls;
- expand UI capture into backend/gameplay reverse engineering.

The active work item/campaign decides what desktop actions are permitted.

## Evidence rule

Never promote an inference into fact.

Use the project evidence states:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

Every important fact must name the exact 0.3.17 source/observation.

## Checkpoint rule

Even in a long Loop campaign, preserve a coherent checkpoint roughly every 20–30 minutes or at each completed stage, whichever comes first.

Before a stage commit:

- finish its durable evidence/report;
- run applicable validation;
- run `git diff --check`;
- review `git status --short`;
- commit coherently;
- push `research/offline-controller`;
- verify the push when practical.

## Stop behavior

If no work item/campaign is authorized:

- do not change project files;
- do not invent a new task;
- do not start reverse engineering;
- report exactly:

`WAITING_FOR_PROJECT_LEAD`

For a timed campaign, obey its elapsed-time shutdown rule before opening another stage.

## Prohibited Loop behavior

Loop mode must never become:

- “keep reverse engineering anything useful”;
- “keep improving the UI”;
- “continue until the whole project is done”;
- “fix whatever you notice”;
- “figure out login so you can get past it”.

Those prompts destroy evidence boundaries and project-lead control.

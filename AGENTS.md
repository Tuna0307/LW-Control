# Mandatory rules for every AI and contributor

Read this file before doing any work.

Owner direction, 2026-10-07 (current): feature recovery continues offline while
the owner uses this computer. All Last War/live-function testing is ON HOLD until
the owner explicitly resumes it after arranging a second screen and availability.
An older live work item, tool installation, or previous authorization does not
override this hold. Do not launch/control the game, instrument its installation,
capture the owner's desktop, send input, change focus, or interrupt owner activity.
Use static artifacts and isolated/inert providers, processes and test roots.
Keep live-dependent behavior UNKNOWN or BLOCKED; never substitute fixture results
for live proof. Source/local UIUX acceptance and completed recovery fixes remain.

Other owner-relayed worker AIs must work alone: no subagents, delegation to other
AI chats, or use of GPT Work or Codex to perform their assignment. This supersedes
the 2026-10-05 worker subagent permission and conflicting older work items. The
restriction applies to the other worker AIs, not this Codex project-lead chat.
Use sequential medium milestones, durable checkpoints and manual owner relay.

For authorized desktop work, use Remote Desktop Commander and Windows-MCP as
complementary tools. Inspect actual available tools/schemas; if one lacks a needed
capability, check the other before declaring a tooling blocker. Record exact
commands, errors and unavailable capabilities when both cannot perform the step.
Do not invent tool availability or success. Windows-MCP is machine-local under
`C:\Users\chimw\OneDrive\Desktop\Github\LW-Control\Windows-MCP`;
see `docs/WINDOWS_MCP_SETUP.md`. Installation does not lift the current desktop/
live hold or authorize protected-service access. Headless startup/tool-list checks
are permitted; actual screenshots/input wait for a specifically authorized task.

Owner direction, 2026-10-02: use manual relay between AI chats. The project lead
writes self-contained task prompts for the owner to forward and independently
reviews returned work. The Chat On Steroids collaboration policy is retired;
its mandatory delegation, model-selection and two-checkpoint requirements no
longer apply. Follow the existing project evidence and review rules below.

Historical owner direction, 2026-10-05 (superseded by 2026-10-07): assigned worker AIs may now use subagents within
their project-lead-assigned scope. This supersedes the earlier worker subagent
ban and conflicting instructions in older work items. Keep one coordinating
worker responsible for task ownership, integration, validation and delivery.
Give each subagent a concrete bounded task, necessary context, acceptance checks
and exclusive ownership of any files it may edit. Do not allow concurrent edits
to the same file or concurrent control of the same browser tab/process. The
coordinating worker must inspect and verify subagent output before accepting it;
only that worker commits/pushes the integrated milestone. Use medium-sized
milestones and durable checkpoints. Manual owner relay between independent
worker and project-lead chats remains in force. Subagent permission does not
expand product scope, authorize native/gameplay work, or delegate final project
acceptance away from the lead.

Historical owner direction, 2026-10-03 (superseded above): use one AI per assigned worker chat. Do not spawn
subagents or delegate to other agents. This supersedes earlier work-item
permission to use subagents. Complete larger assignments through sequential,
coherent milestones; the owner continues to relay between worker and lead chats.

Historical owner clarification, 2026-10-03 (superseded above): the no-subagent rule applies to assigned worker
chats (the other AI). The project lead may use subagents as needed and remains
responsible for reviewing their work. This does not expand worker scopes or
authorize concurrent edits to a worker's assigned files.

## 1. Current target

The active product/research target is:

`C:\Users\chimw\OneDrive\Desktop\Github\LW\lwbridge-0.3.17.exe`

SHA-256:

`4E9C3113DEDFD7E1A752404C6936AAB304E67D7FFDB0952A5003C2EC948D6783`

The old 0.3.1 research remains historical evidence only unless revalidated against 0.3.17.

## 2. Product goal

The goal is one-for-one reproduction of the **in-scope post-auth LWBridge 0.3.17 product experience** as observed/recovered, while keeping the final implementation compatible with the current Last War client.

The clone must not include the original login/account/licensing UI or commercial account system. Auth-related dependency research is allowed when required for an in-scope working feature, under section 6.

Use one canonical production implementation. Do not introduce an automatic or user-selectable legacy UI fallback. Preserve historical source/evidence for research; it is not a product recovery path. The existing `--legacy-ui` option is pending retirement in a separately assigned host task, not already removed.

Do not redesign in-scope workflows, labels, defaults, tabs, states, timing or behavior merely because another design seems better.

Owner clarification, 2026-10-07: **A -> A behavioural parity is mandatory.**
If the original performs A for a given supported input/state, the clone must
perform A for the equivalent input/state. Rewriting in another language or adapting
to the current client does not authorize B. This includes observable selection,
ordering, validation, defaults, limits, timing, retry/cancel/failure behaviour,
state/persistence transitions, UI and result meanings. Internal mechanisms may
differ only with evidence that they preserve the recovered observable contract;
label adaptations separately from exact-original facts. Passing tests written
solely against the clone, compilation or a plausible design is not equivalence
proof. Unknown original behaviour remains UNKNOWN/incomplete; never replace it
with a guessed policy and call the feature recovered. An unavailable safety fence
is a truthful incomplete feature, not successful functional parity. Document and
correct demonstrated differences, including stricter validation. Existing explicit
owner exclusions and live/session/access boundaries remain in force. See
`docs/owner-directions/2026-10-07-A-TO-A-PARITY.md`.

## 3. Work order

Current order:

1. prepare/clean the research project;
2. reproduce the in-scope 0.3.17 post-auth UI one-for-one;
3. reverse engineer each in-scope function;
4. map recovered behavior to the current game;
5. live-prove parity.

Do not begin a different phase unless the project lead assigns it.

## 4. Evidence first

Never invent command names, IDs, fields, limits, timing, defaults, formulas, offsets, request schemas, success criteria or UI behavior.

Use these states:

- `EXACT_BYTES`
- `EXACT_CONTRACT`
- `IMPLEMENTED_NOT_VALIDATED`
- `LIVE_PROVEN`
- `UNKNOWN`
- `BLOCKED`
- `OUT_OF_SCOPE`

Every recovered fact must name its source and locator.

## 5. 0.3.1 evidence policy

Do not delete old reviews/evidence.

A 0.3.1 finding may be used as a hypothesis or shortcut, but it must not be reported as 0.3.17 fact until checked against the 0.3.17 executable/assets/runtime.

## 6. Login UI exclusion and required dependencies

Owner clarification, 2026-10-01: absence of a login page does not prohibit
research into auth-related dependencies required to make the in-scope clone work.
Trace relevant local code, state producers/consumers and contracts from supplied
artifacts and authorized access. Record the feature dependency and source locator;
do not stop solely because a required path touches auth-related code.

Do not add login/account/purchase/subscription/licensing UI as clone features.
The clone uses its own local runtime/profile state rather than pretending to hold
a valid original-service entitlement. Do not obtain another person's credentials
or circumvent authentication/entitlement controls on the original service or
protected original program. Needed state-contract recovery does not establish
permission to access a third-party service.

Worker scopes remain bounded by their assigned work item. Required UI state
dependencies can be inspected within a UI task; unrelated protocol/service
research and native integration are separate assignments. Earlier broad bans
on auth dependency research in historical documents are superseded by this rule.

## 7. Desktop-control rule

As of 2026-09-29, the owner has explicitly re-enabled desktop-control tooling
for the UI-parity phase.

Approved desktop tooling may be used for
bounded LWBridge 0.3.17 UI observation tasks assigned by the project lead.

This permission does **not** automatically authorize gameplay/live-function
testing. Do not launch or control Last War unless the assigned work item
explicitly requires it.

For UI capture work:

- observe before acting;
- use the exact 0.3.17 reference executable;
- do not bypass login/auth/entitlement merely to reach another screen;
- do not press gameplay/function controls outside the assigned scope;
- preserve screenshots/state evidence under the 0.3.17 evidence tree.

## 8. Live-session safety

Before any future live test:

- inspect existing Last War/LWBridge processes;
- treat unexplained existing game sessions as owner activity;
- do not close or repurpose an owner session;
- prefer assistant-owned sessions for disruptive tests;
- fail closed if session ownership is ambiguous.

## 9. Milestone checkpoints

Owner clarification, 2026-10-01: no fixed 20-minute work block or minute-17/18
stop rule. Continue the assigned scope until its acceptance criteria are met or
a concrete blocker prevents further progress.

Preserve useful state, update documentation and commit coherent milestones
frequently. Report the exact continuation point if interrupted or blocked.
Elapsed time alone is not a reason to stop. This does not authorize changing
scope or starting an unrelated campaign.

## 10. Documentation rule

A useful finding is not complete until it is written into the repository.

For each completed work item:

- update the relevant current master document;
- create a dated finding/review when detailed evidence is needed;
- update the parity matrix/feature ledger if status changed;
- update the handoff if the continuation point changed.

## 11. Git rule

Work on `research/offline-controller` unless the project lead explicitly assigns another branch.

For coherent checkpoints:

- review the diff;
- run applicable checks;
- commit;
- push to `origin/research/offline-controller`;
- verify the remote revision.

Never force-push or discard unrelated work.

## 12. Tooling

Project-related tool discovery/installation is pre-authorized.

Prefer existing tools first. Record tool/version/source when a new tool materially affects a recovery finding.

PowerShell 7 is available as `pwsh` (7.6.6).

## 13. Worker-AI discipline

Worker AIs must follow `docs/AI_WORK_PROTOCOL.md`.

Loop-mode workers must additionally follow `docs/LOOP_WORKER_PROTOCOL.md` and the active project-lead campaign/queue.

A worker does not redefine product scope, evidence standards, phase order or master status. Return findings to the project lead through durable files/checkpoints.

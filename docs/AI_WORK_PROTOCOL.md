# AI work protocol

This project is expected to use multiple AIs. The main project lead controls scope and integrates results.

Owner direction, 2026-10-02: the owner relays prompts and replies between AI chats.
The lead supplies one self-contained, bounded prompt per assignment, assuming a
fresh worker chat, then verifies the returned files, evidence and checks before
acceptance. Chat On Steroids collaboration requirements are retired; no connector
setup or repair is required to continue. Existing scope and review rules apply.

Owner direction, 2026-10-05: workers may use subagents within their assigned
scope. This supersedes the earlier no-subagent rule, including conflicting old
work-item text. One coordinating worker remains responsible for integration,
verification and owner-relayed delivery. See the coordination rules below.

Historical owner direction, 2026-10-03 (superseded above): each worker works alone. Do not spawn subagents or
delegate to another agent, including when an older assignment allowed it.
Use sequential milestones and durable checkpoints for larger assignments.

Historical owner clarification, 2026-10-03 (superseded above): that restriction applies to worker chats only.
The project lead may use subagents, review their evidence and integrate results.
Keep their edits separate from an active worker's owned scope.

## Roles

Owner direction, 2026-10-04: prefer one small-to-medium bounded assignment per
worker dispatch. Larger remaining campaigns are split into separately reviewed
units; preserve quality and durable checkpoints rather than expanding a unit.

Owner exception, 2026-10-04: explicitly assign VISUAL-FINAL-CAMPAIGN-001 as one
large worker campaign covering all four remaining visual groups, through sequential
units A–J and durable commits. This exception supersedes the size preference only
for that campaign. Coordinating-worker/evidence/lead-acceptance rules remain in force.

### Project lead

Owns:

- phase order;
- task assignment;
- master status;
- backlog;
- parity matrix;
- evidence standards;
- conflict resolution;
- final integration decisions.

### Worker AI

Owns one assigned bounded work item.

A worker must not independently broaden scope, change the target version, redesign the product or rewrite master project policy.

## Work-item format

Every assignment should contain:

- **ID** — e.g. `LWB317-UI-001`
- **Goal**
- **Allowed scope**
- **Explicit non-goals**
- **Inputs/reference artifacts**
- **Required outputs**
- **Acceptance checks**
- **Completion boundary and milestone checkpoint plan** — no fixed elapsed-time
  stop under the owner's 2026-10-01 clarification in `AGENTS.md` section 9

## Worker start checklist

1. Read `AGENTS.md`.
2. Read `task.md`.
3. Read this file.
4. Read the exact current master doc for the assigned workstream.
5. Check `git status --short`.
6. Confirm target SHA-256/version if touching reference-derived evidence.

## Evidence rule

A worker should create a focused review/finding file for meaningful recovered facts.

Do not dump raw reasoning. Record:

- source identity;
- locator;
- tool/command;
- observation;
- interpretation;
- limits;
- implementation impact.

## Master-doc ownership

Workers may propose updates, but broad status changes should be conservative.

For ambiguous findings, mark `UNKNOWN` rather than upgrading the master matrix.

## Git

Default branch is `research/offline-controller`.

Use one coherent commit per completed work item where practical.

Do not force-push.

## Handoff template

At the end of a worker task report:

```text
Work item:
Status:
Reference:
Files changed:
Evidence:
Facts established:
Still unknown:
Checks:
Commit:
Recommended next task:
```

## Coordinating worker and subagents

Keep one coordinating worker for each owner-relayed assignment. Subagents may
perform bounded research, implementation or review within that assignment;
they do not create new project work or broaden its scope. Independent worker
chats and the project lead continue to communicate through the owner's relay.

Before dispatching a subagent, provide the goal, exact scope, relevant sources,
intentional design decisions, behaviors to preserve, evidence requirements,
acceptance checks, and allowed file paths. Record responsibilities in the task's
coordination/continuation files so interrupted work remains recoverable.

Each editable file has one owner at a time. Reserve shared components, master
documents and integration-sensitive files for the coordinator unless ownership
is explicitly transferred. Give subagents separate evidence output directories
and owned browser contexts/listeners where needed. Do not concurrently control
the same tab/process or share mutable browser storage without coordination.

The coordinator reviews each proposed diff and finding, checks its source proof,
independently runs applicable verification, and resolves conflicts before
integration. A subagent's PASS or completion message is not acceptance evidence.
Only the coordinator commits/pushes integrated milestones and verifies the
remote revision; subagents must not manipulate the shared Git index/history.

Prefer a small initial group and add agents only for independent work the
coordinator can review. Finish coherent medium milestones and persist progress
frequently. If a child stalls, inspect its saved work, recover the useful
checkpoint, and complete or reassign that bounded scope without losing progress.
Project-lead final acceptance and all existing scope/evidence rules remain intact.

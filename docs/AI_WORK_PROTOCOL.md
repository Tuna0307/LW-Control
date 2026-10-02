# AI work protocol

This project is expected to use multiple AIs. The main project lead controls scope and integrates results.

Owner's collaboration policy: read and follow
[Chat On Steroids Collaboration](CHAT_ON_STEROIDS_COLLABORATION.md).
Use 5.6 Thinking / High effort for delegated collaborators. Meaningful code
changes require a hypothesis review before editing and a complete-diff review
before submission. Apply the policy's documentation exceptions and connector
rules. The lead owns dispatch/integration; workers must not spawn nested workers
or grant themselves independent acceptance. Every assigned scope still applies.
A collaborator's self-check does not replace independent review.

## Roles

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

## Parallelism rule

Avoid two workers editing the same master file at the same time.

Prefer splitting by evidence/workstream and letting the project lead integrate master status.
